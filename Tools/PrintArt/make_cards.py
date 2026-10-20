"""Dealbreak playing cards in the punk-poster print style.

Writes all 52 faces and both backs over Assets/Art/Cards/**.png, keeping each
file's GUID so Card.prefab needs no rewiring, and sets each .meta sprite rect
to the full image. Seeded noise only: a rerun reproduces the files exactly.

    pip install -r Tools/PrintArt/requirements.txt
    python Tools/PrintArt/make_cards.py            # writes the cards
    python Tools/PrintArt/make_cards.py --sheet out.png   # preview sheet only
"""
import argparse
import math
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
CARDS = ROOT / "Assets/Art/Cards"
ANTON = ROOT / "Assets/Art/Fonts/Anton-Regular.ttf"

W, H = 533, 764          # existing card texture size
SS = 3                   # supersampling for anti-aliased shapes and type
INK = (14, 17, 19)
PAPER = (240, 230, 207)
BRICK = (168, 69, 56)
SLATE = (74, 92, 105)
CHARCOAL = (27, 33, 40)

SUITS = [("Hearts", "h", True), ("Diamonds", "d", True), ("Clubs", "c", False), ("Spades", "s", False)]
RANKS = ["A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K"]
# Run order is J (Runner), Q (Operator), K (Kingpin).
ROLES = {"J": "RUNNER", "Q": "OPERATOR", "K": "KINGPIN"}

# Pip centres as (x, y) fractions of the pip field; pips below the middle are drawn upside down.
L, M, R = 0.0, 0.5, 1.0
PIPS = {
    2: [(M, 0), (M, 1)],
    3: [(M, 0), (M, .5), (M, 1)],
    4: [(L, 0), (R, 0), (L, 1), (R, 1)],
    5: [(L, 0), (R, 0), (M, .5), (L, 1), (R, 1)],
    6: [(L, 0), (R, 0), (L, .5), (R, .5), (L, 1), (R, 1)],
    7: [(L, 0), (R, 0), (M, .25), (L, .5), (R, .5), (L, 1), (R, 1)],
    8: [(L, 0), (R, 0), (M, .25), (L, .5), (R, .5), (M, .75), (L, 1), (R, 1)],
    9: [(L, 0), (R, 0), (L, 1 / 3), (R, 1 / 3), (M, .5), (L, 2 / 3), (R, 2 / 3), (L, 1), (R, 1)],
    10: [(L, 0), (R, 0), (M, 1 / 6), (L, 1 / 3), (R, 1 / 3), (L, 2 / 3), (R, 2 / 3), (M, 5 / 6), (L, 1), (R, 1)],
}


# ---------- noise ----------

def value_noise(h, w, cell, seed):
    rng = np.random.default_rng(seed)
    small = rng.random((h // cell + 3, w // cell + 3)).astype(np.float32)
    img = Image.fromarray((small * 255).astype(np.uint8)).resize(((w // cell + 3) * cell, (h // cell + 3) * cell), Image.BICUBIC)
    return np.asarray(img, np.float32)[:h, :w] / 255


def fbm(h, w, cell, seed, octaves=3):
    total, amp, out = 0.0, 1.0, np.zeros((h, w), np.float32)
    for o in range(octaves):
        out += amp * value_noise(h, w, max(1, cell >> o), seed * 31 + o)
        total += amp
        amp *= .5
    return out / total


def halftone(h, w, cell, angle, darkness):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = math.radians(angle)
    u = (xx * math.cos(t) + yy * math.sin(t)) / cell
    v = (-xx * math.sin(t) + yy * math.cos(t)) / cell
    d = np.hypot(u - np.floor(u) - .5, v - np.floor(v) - .5)
    return (d < np.sqrt(np.clip(darkness, 0, 1) / math.pi)).astype(np.float32)


# ---------- shapes (drawn at SS scale into an L mask) ----------

def heart_points(cx, cy, size, flip=False):
    pts = []
    for i in range(160):
        t = 2 * math.pi * i / 160
        x = 16 * math.sin(t) ** 3
        y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        y = -y if not flip else y
        pts.append((cx + x / 34 * size, cy + (y + (-2.5 if not flip else 2.5)) / 34 * size))
    return pts


def draw_suit(d, suit, cx, cy, size, fill=255):
    """size is the suit's overall height."""
    if suit == "h":
        d.polygon(heart_points(cx, cy, size * 1.06), fill=fill)
    elif suit == "d":
        pts = []
        for i in range(4):
            a0, a1 = i * math.pi / 2 - math.pi / 2, (i + 1) * math.pi / 2 - math.pi / 2
            p0 = (cx + math.cos(a0) * size * .36, cy + math.sin(a0) * size * .5)
            p1 = (cx + math.cos(a1) * size * .36, cy + math.sin(a1) * size * .5)
            for k in range(24):    # slightly concave sides
                s = k / 24
                bow = math.sin(s * math.pi) * size * .045
                mx, my = p0[0] + (p1[0] - p0[0]) * s, p0[1] + (p1[1] - p0[1]) * s
                pts.append((mx + (cx - mx) / max(1, math.hypot(cx - mx, cy - my)) * bow,
                            my + (cy - my) / max(1, math.hypot(cx - mx, cy - my)) * bow))
        d.polygon(pts, fill=fill)
    elif suit == "s":
        d.polygon(heart_points(cx, cy - size * .08, size * .98, flip=True), fill=fill)
        stem(d, cx, cy + size * .1, size, fill)
    elif suit == "c":
        r = size * .2
        for ox, oy in ((0, -.26), (-.23, .04), (.23, .04)):
            d.ellipse([cx + ox * size - r, cy + oy * size - r, cx + ox * size + r, cy + oy * size + r], fill=fill)
        d.polygon([(cx, cy - size * .2), (cx - size * .16, cy + size * .06), (cx + size * .16, cy + size * .06)], fill=fill)
        stem(d, cx, cy + size * .06, size, fill)


def stem(d, cx, top, size, fill):
    bottom = top + size * .36
    d.polygon([(cx - size * .05, top), (cx + size * .05, top), (cx + size * .2, bottom), (cx - size * .2, bottom)], fill=fill)


def crown(d, cx, cy, width, fill=255):
    h = width * .55
    pts = [(cx - width / 2, cy + h / 2), (cx - width / 2, cy - h * .25), (cx - width * .25, cy + h * .05),
           (cx, cy - h / 2), (cx + width * .25, cy + h * .05), (cx + width / 2, cy - h * .25), (cx + width / 2, cy + h / 2)]
    d.polygon(pts, fill=fill)


def text_mask(text, cap_px, letter_spacing=0):
    """Anton text whose capitals are cap_px tall (at SS scale), cropped to its ink."""
    size = int(cap_px / 0.86)
    font = ImageFont.truetype(str(ANTON), size)
    glyphs = []
    for ch in text:
        l, t, r, b = font.getbbox(ch)
        g = Image.new("L", (r - l + 4, size * 2), 0)
        ImageDraw.Draw(g).text((-l + 2, 0), ch, font=font, fill=255)
        glyphs.append(g)
    w = sum(g.width for g in glyphs) + letter_spacing * (len(glyphs) - 1)
    img = Image.new("L", (max(1, w), size * 2), 0)
    x = 0
    for g in glyphs:
        img.paste(g, (x, 0), g)
        x += g.width + letter_spacing
    return img.crop(img.getbbox())


def paste_center(canvas, mask, cx, cy, rotate=False):
    if rotate:
        mask = mask.rotate(180)
    canvas.paste(255, (int(cx - mask.width / 2), int(cy - mask.height / 2)), mask)


# ---------- card construction ----------

def card_shape():
    m = Image.new("L", (W * SS, H * SS), 0)
    ImageDraw.Draw(m).rounded_rectangle([3 * SS, 3 * SS, (W - 4) * SS, (H - 4) * SS], radius=34 * SS, fill=255)
    return m


def down(mask):
    return np.asarray(mask.resize((W, H), Image.LANCZOS), np.float32) / 255


def compose(layers, seed):
    """layers: [(mask 0..1, rgb, wear)] painted back to front over grained paper."""
    grain = .7 * fbm(H, W, 3, seed) + .3 * fbm(H, W, 90, seed + 1)
    out = np.array(PAPER, np.float32) * (1 - .05 * (grain[..., None] - .5) * 2)
    wear_fine = fbm(H, W, 2, seed + 2, 2)
    wear_patch = fbm(H, W, 30, seed + 3, 2)
    for mask, rgb, wear in layers:
        a = mask.copy()
        if wear:
            a *= ~((wear_fine > 1 - wear * .2) | ((wear_patch > .8) & (wear_fine > .7)))
        out = out * (1 - a[..., None]) + np.array(rgb, np.float32) * a[..., None]
    return out


# The corner index is what reads in a hand of small cards, so it is sized for that, not for print.
INDEX_CAP, INDEX_MAX_W, INDEX_PIP, INDEX_X, INDEX_TOP = 110, 88, 56, 70, 36


def corner_index(rank, suit, red):
    m = Image.new("L", (W * SS, H * SS), 0)
    rank_img = text_mask(rank, INDEX_CAP * SS, letter_spacing=-4 * SS if rank == "10" else 0)
    if rank_img.width > INDEX_MAX_W * SS:   # "10" stays inside the corner column
        rank_img = rank_img.resize((INDEX_MAX_W * SS, int(rank_img.height * INDEX_MAX_W * SS / rank_img.width)), Image.LANCZOS)
    pip = Image.new("L", (int(INDEX_PIP * 1.3) * SS, int(INDEX_PIP * 1.3) * SS), 0)
    draw_suit(ImageDraw.Draw(pip), suit, pip.width / 2, pip.height / 2, INDEX_PIP * SS)
    cx = INDEX_X * SS
    for rotate in (False, True):
        block = Image.new("L", (W * SS, H * SS), 0)
        paste_center(block, rank_img, cx, INDEX_TOP * SS + rank_img.height / 2)
        paste_center(block, pip, cx, INDEX_TOP * SS + rank_img.height + (14 + INDEX_PIP / 2) * SS)
        if rotate:
            block = block.rotate(180)
        m.paste(255, (0, 0), block)
    return m


def number_card(rank, suit, red, seed):
    color = BRICK if red else INK
    shape = card_shape()
    border = Image.new("L", shape.size, 0)
    ImageDraw.Draw(border).rounded_rectangle([3 * SS, 3 * SS, (W - 4) * SS, (H - 4) * SS], radius=34 * SS, outline=255, width=9 * SS)
    marks = corner_index(rank, suit, red)
    pips = Image.new("L", shape.size, 0)
    shadow = Image.new("L", shape.size, 0)
    if rank == "A":
        size = 250 * SS
        draw_suit(ImageDraw.Draw(pips), suit, W * SS / 2, H * SS / 2, size)
        draw_suit(ImageDraw.Draw(shadow), suit, W * SS / 2 + 16 * SS, H * SS / 2 + 14 * SS, size)
    else:
        # Narrower than a print deck's pip field so the large corner indices stay clear of it.
        x0, x1, y0, y1 = 200, 333, 172, 592
        size = (80 if int(rank) <= 8 else 72) * SS
        for fx, fy in PIPS[int(rank)]:
            px, py = (x0 + (x1 - x0) * fx) * SS, (y0 + (y1 - y0) * fy) * SS
            pip = Image.new("L", (int(size * 1.2), int(size * 1.2)), 0)
            draw_suit(ImageDraw.Draw(pip), suit, pip.width / 2, pip.height / 2, size)
            paste_center(pips, pip, px, py, rotate=fy > .5)
    s, p, b, mk, sh = down(shape), down(pips), down(border), down(marks), down(shadow)
    layers = [(halftone(H, W, 7, 45, .55) * sh * s, INK if red else BRICK, .3)]
    layers += [(p * s, color, .25), (mk * s, color, .1), (b * s, INK, .4)]
    return finish(compose(layers, seed), s)


def face_card(rank, suit, red, seed):
    panel_color = BRICK if red else INK
    band_color = INK if red else BRICK
    shape = card_shape()
    border = Image.new("L", shape.size, 0)
    ImageDraw.Draw(border).rounded_rectangle([3 * SS, 3 * SS, (W - 4) * SS, (H - 4) * SS], radius=34 * SS, outline=255, width=9 * SS)
    # Poster panel with a clipped corner, the shape language of the Dealbreak frames.
    px0, py0, px1, py1, cut = 132, 128, 401, 636, 46   # inset to clear the corner indices
    panel = Image.new("L", shape.size, 0)
    ImageDraw.Draw(panel).polygon([(px0 * SS, py0 * SS), ((px1 - cut) * SS, py0 * SS), (px1 * SS, (py0 + cut) * SS),
                                   (px1 * SS, py1 * SS), ((px0 + cut) * SS, py1 * SS), (px0 * SS, (py1 - cut) * SS)], fill=255)
    letter = Image.new("L", shape.size, 0)
    paste_center(letter, text_mask(rank, 210 * SS), W * SS / 2, 352 * SS)
    if rank == "K":
        crown(ImageDraw.Draw(letter), W * SS / 2, 196 * SS, 110 * SS)
    suit_mark = Image.new("L", shape.size, 0)
    draw_suit(ImageDraw.Draw(suit_mark), suit, W * SS / 2, (196 if rank != "K" else 503) * SS, 64 * SS)
    if rank != "K":
        draw_suit(ImageDraw.Draw(suit_mark), suit, W * SS / 2, 503 * SS, 64 * SS)
    band = Image.new("L", shape.size, 0)
    ImageDraw.Draw(band).rectangle([px0 * SS, 556 * SS, px1 * SS, 600 * SS], fill=255)
    role = Image.new("L", shape.size, 0)
    paste_center(role, text_mask(ROLES[rank], 28 * SS, letter_spacing=3 * SS), W * SS / 2, 578 * SS)
    marks = corner_index(rank, suit, red)
    s, pn, lt, sm, bd, rl, mk, b = map(down, (shape, panel, letter, suit_mark, band, role, marks, border))
    tone = halftone(H, W, 8, 45, np.clip((np.mgrid[0:H, 0:W][0] / H - .25) * 1.2, 0, .5))
    layers = [
        (pn * s, panel_color, .2),
        (tone * pn * (1 - lt) * s, CHARCOAL if red else SLATE, .2),
        (np.roll(lt, (5, 5), (0, 1)) * pn * (1 - lt) * s, band_color, .2),   # misregistered print shadow
        (lt * s, PAPER, .15),
        (sm * s, PAPER, .1),
        (bd * pn * s, band_color, .2),
        (rl * bd * s, PAPER, 0),
        (mk * s, BRICK if red else INK, .1),
        (b * s, INK, .4),
    ]
    return finish(compose(layers, seed), s)


def back(primary, seed):
    shape = card_shape()
    inner = Image.new("L", shape.size, 0)
    ImageDraw.Draw(inner).rounded_rectangle([26 * SS, 26 * SS, (W - 27) * SS, (H - 27) * SS], radius=18 * SS, outline=255, width=5 * SS)
    emblem = Image.new("L", shape.size, 0)
    d = ImageDraw.Draw(emblem)
    cx, cy, r = W * SS / 2, H * SS / 2, 112 * SS
    d.polygon([(cx, cy - r * 1.25), (cx + r, cy), (cx, cy + r * 1.25), (cx - r, cy)], fill=255)
    letters = Image.new("L", shape.size, 0)
    paste_center(letters, text_mask("DB", 84 * SS, letter_spacing=4 * SS), cx, cy)
    s, inn, em, lt = map(down, (shape, inner, emblem, letters))
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    radial = np.hypot((xx - W / 2) / W, (yy - H / 2) / H)
    lattice = halftone(H, W, 11, 45, np.clip(.75 - radial * 1.1, 0, .6))
    stripes = ((xx + yy) // 18 % 2 == 0).astype(np.float32) * ((yy < 120) | (yy > H - 120))
    layers = [
        (s, INK, 0),
        (lattice * (1 - em) * s, primary, .35),
        (stripes * (1 - em) * s, CHARCOAL, .2),
        (inn * s, PAPER, .3),
        (em * s, PAPER, .2),
        (np.roll(lt, (4, 4), (0, 1)) * s, primary, 0),
        (lt * s, INK, .1),
    ]
    return finish(compose(layers, seed), s)


def finish(rgb, alpha):
    a = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(np.dstack([np.clip(rgb, 0, 255).astype(np.uint8), a]), "RGBA")


def all_cards():
    for si, (folder, letter, red) in enumerate(SUITS):
        for ri, rank in enumerate(RANKS):
            seed = 1000 + si * 13 + ri
            img = face_card(rank, letter, red, seed) if rank in ROLES else number_card(rank, letter, red, seed)
            yield CARDS / folder / f"{ri + 1}{letter}.png", img
    yield CARDS / "Backs/Back Red.png", back(BRICK, 7)
    yield CARDS / "Backs/Back Blue.png", back(SLATE, 8)


RECT = re.compile(r"(spriteSheet:\s*\n\s*serializedVersion: 2\s*\n\s*sprites:\s*\n\s*- serializedVersion: 2\s*\n\s*name: [^\n]*\n\s*rect:\s*\n\s*serializedVersion: 2\s*\n)"
                  r"(\s*)x: [-\d.]+\n\s*y: [-\d.]+\n\s*width: [-\d.]+\n\s*height: [-\d.]+\n")


def full_rect(meta_path):
    text = meta_path.read_text(encoding="utf-8")
    if text.count("- serializedVersion: 2\n      name:") > 1:
        raise SystemExit(f"{meta_path}: expected one sprite")
    new, n = RECT.subn(lambda m: f"{m.group(1)}{m.group(2)}x: 0\n{m.group(2)}y: 0\n{m.group(2)}width: {W}\n{m.group(2)}height: {H}\n", text)
    if n != 1:
        raise SystemExit(f"{meta_path}: sprite rect not found")
    with open(meta_path, "w", encoding="utf-8", newline="") as f:
        f.write(new)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet", help="write a preview sheet here instead of the card files")
    args = ap.parse_args()
    cards = list(all_cards())
    if args.sheet:
        cols, scale = 14, .35
        tw, th = int(W * scale), int(H * scale)
        sheet = Image.new("RGB", (cols * (tw + 8) + 8, 4 * (th + 8) + 8), (40, 46, 54))
        for i, (_, img) in enumerate(cards):
            sheet.paste(img.resize((tw, th), Image.LANCZOS), (8 + i % cols * (tw + 8), 8 + i // cols * (th + 8)), img.resize((tw, th), Image.LANCZOS))
        sheet.save(args.sheet)
        print("wrote", args.sheet)
        return
    for path, img in cards:
        if not path.exists():
            raise SystemExit(f"{path} is missing; this tool only replaces existing card files")
        img.save(path)
        full_rect(Path(str(path) + ".meta"))
    print(f"wrote {len(cards)} cards")


if __name__ == "__main__":
    main()
