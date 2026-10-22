"""Dealbreak Gear and Trick icons: painted item art -> neo-punk halftone cut-outs.

Each source is cut to its silhouette and reprinted in its own colours: the painting is separated
into a few flat spot inks (muted toward the off-white; greys print as off-white, grey or charcoal),
with ink shading as solid fills and halftone dots and ink linework, then outlined by a thin
off-white cut line like a collage cut-out so dark items still read on the dark slots. No paper
sticker edge, no shadow. The background stays transparent so the icon sits on the slot's own
frame. Seeded noise only: a rerun reproduces the files exactly.

    pip install -r Tools/PrintArt/requirements.txt
    python Tools/PrintArt/make_item_icons.py                    # writes Assets/Art/Items/**
    python Tools/PrintArt/make_item_icons.py --sheet out.png    # preview sheet only
"""
import argparse
import colorsys
import zlib
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

from make_cards import draw_suit

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "Tools/PrintArt/item_sources"
ITEMS = ROOT / "Assets/Art/Items"

# The game palette (DealbreakUITheme): near-black, off-white, signal red, neutral grey, charcoal.
INK = np.array([11, 11, 12], np.float32)
PAPER = np.array([236, 234, 228], np.float32)
BRICK = np.array([232, 50, 43], np.float32)
SLATE = np.array([58, 58, 64], np.float32)
CHARCOAL = (22, 22, 26)
CHARCOAL_INK = np.array(CHARCOAL, np.float32)

WORK = 512               # processing canvas; the object fits inside FIT
FIT = 404
OUT = 256                # written texture size
SPOTS = 4                # flat colour inks per icon

# Output file (Assets/Art/Items/<path>.png) -> source painting (Tools/PrintArt/item_sources/<pack>/<n>.png).
# One icon per Gear family: every tier of a family shares it.
ICONS = {
    # Gear
    "Gear/AceProtocol": "Artifacts/39",
    "Gear/AutoInjector": "Medicine/47",
    "Gear/BountyHologram": "Drone/21",
    "Gear/CarbonSleeves": "Cloak and Gloves/38",
    "Gear/CargoCase": "Energy/13",
    "Gear/CashbackCard": "Drone/27",
    "Gear/CityBoots": "Radiation/19",
    "Gear/ClubAmplifier": "Artifacts/45",
    "Gear/CounterpunchGloves": "Cloak and Gloves/36",
    "Gear/Dealbreaker": "Artifacts/8",
    "Gear/DetourPin": "Drone/5",
    "Gear/DiamondDriver": "Energy/46",
    "Gear/EchoDriver": "Energy/20",
    "Gear/FieldTablet": "Drone/29",
    "Gear/FirstAidKit": "Medicine/16",
    "Gear/FlipSwitch": "Drone/45",
    "Gear/GhostLens": "Artifacts/9",
    "Gear/GlassShard": "Drone/12",
    "Gear/GoldPistol": "Weapon/36",
    "Gear/GrapplingHook": "Drone/14",
    "Gear/GuardDrone": "Drone/7",
    "Gear/HeadhunterRifle": "Weapon/48",
    "Gear/HeartMeds": "Medicine/42",
    "Gear/Lighter": "Medicine/44",
    "Gear/Lowball": "Drone/11",
    "Gear/LoyaltyCard": "Energy/40",
    "Gear/LoyaltyGift": "Artifacts/2",
    "Gear/MatchedCells": "Energy/7",
    "Gear/MemoryCard": "Drone/42",
    "Gear/ModBench": "Energy/21",
    "Gear/ModCase": "Medicine/38",
    "Gear/ModdersKit": "Drone/33",
    "Gear/NotchedPistol": "Weapon/11",
    "Gear/NuclearOption": "Radiation/47",
    "Gear/OverflowCanister": "Energy/17",
    "Gear/PayoutTracker": "Radiation/16",
    "Gear/PowerBaton": "Energy/5",
    "Gear/RecoveryPills": "Medicine/10",
    "Gear/RiotHelmet": "Radiation/36",
    "Gear/SavingsSafe": "Energy/12",
    "Gear/Scalpel": "Medicine/28",
    "Gear/Scanner": "Radiation/9",
    "Gear/SecondWind": "Energy/11",
    "Gear/SpadeShield": "Artifacts/22",
    "Gear/SpotterEye": "Drone/8",
    "Gear/StreetJacket": "Cloak and Gloves/21",
    "Gear/SupplyCrate": "Medicine/43",
    "Gear/SurvivorsBadge": "Artifacts/37",
    "Gear/TacticalPlating": "Energy/33",
    "Gear/VarsityJacket": "Cloak and Gloves/25",
    # Tricks
    "Tricks/AlloyPlate": "Radiation/6",
    "Tricks/BackupDrone": "Drone/6",
    "Tricks/Bandage": "Medicine/25",
    "Tricks/BloodBag": "Medicine/19",
    "Tricks/BoosterShot": "Radiation/44",
    "Tricks/CloneVial": "Medicine/30",
    "Tricks/ClubCrystal": "Radiation/2",
    "Tricks/DiamondCrystal": "Drone/13",
    "Tricks/DoubleTap": "Weapon/26",
    "Tricks/EnergyBar": "Medicine/45",
    "Tricks/FeralCapsule": "Medicine/22",
    "Tricks/Firecracker": "Radiation/13",
    "Tricks/HeartCrystal": "Artifacts/11",
    "Tricks/LaserScope": "Drone/18",
    "Tricks/PokerChip": "Artifacts/13",
    "Tricks/PrismCube": "Artifacts/35",
    "Tricks/QuickMarker": "Energy/23",
    "Tricks/Shears": "Medicine/27",
    "Tricks/SpadeCrystal": "Artifacts/42",
    "Tricks/ThrowingStar": "Energy/9",
    # HUD
    "Hud/Credits": "Medicine/12",
}

# Suit items carry a printed suit badge: the reprint drops the painting's colour coding.
BADGES = {
    "Gear/ClubAmplifier": "c", "Gear/DiamondDriver": "d", "Gear/HeartMeds": "h", "Gear/SpadeShield": "s",
    "Tricks/ClubCrystal": "c", "Tricks/DiamondCrystal": "d", "Tricks/HeartCrystal": "h", "Tricks/SpadeCrystal": "s",
}


# ---------- image helpers ----------

def box_blur(a, r):
    if r < 1:
        return a
    k = 2 * r + 1
    for axis in (0, 1):
        p = np.pad(a, [(r + 1, r) if i == axis else (0, 0) for i in range(2)], mode="edge")
        c = np.cumsum(p, axis=axis)
        a = (np.take(c, range(k, c.shape[axis]), axis=axis) - np.take(c, range(0, c.shape[axis] - k), axis=axis)) / k
    return a


def blur(a, sigma):
    # Three box passes approximate a gaussian.
    r = max(1, int(round(sigma * 0.85)))
    for _ in range(3):
        a = box_blur(a, r)
    return a


def fbm(h, w, rng, base, octaves=3):
    out = np.zeros((h, w), np.float32)
    amp, total = 1.0, 0.0
    for o in range(octaves):
        s = max(2, base >> o)
        small = rng.random((h // s + 2, w // s + 2))
        img = Image.fromarray((small * 255).astype(np.uint8)).resize((w + s * 2, h + s * 2), Image.BICUBIC)
        out += amp * np.asarray(img, np.float32)[:h, :w] / 255
        total += amp
        amp *= 0.5
    return out / total


def shift(mask, dx, dy):
    """Offset a mask without wrapping round the edges."""
    out = np.zeros_like(mask)
    h, w = mask.shape
    out[max(dy, 0):h + min(dy, 0), max(dx, 0):w + min(dx, 0)] = mask[max(-dy, 0):h - max(dy, 0), max(-dx, 0):w - max(dx, 0)]
    return out


def grow(mask, px):
    img = Image.fromarray(mask.astype(np.uint8) * 255)
    while px > 0:
        step = min(px, 6)
        img = img.filter(ImageFilter.MaxFilter(step * 2 + 1))
        px -= step
    return np.asarray(img) > 127


def halftone(dark, cell, angle_deg):
    h, w = dark.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = np.radians(angle_deg)
    u = (xx * np.cos(t) + yy * np.sin(t)) / cell
    v = (-xx * np.sin(t) + yy * np.cos(t)) / cell
    d = np.hypot(u - np.floor(u) - 0.5, v - np.floor(v) - 0.5)
    return d < np.sqrt(np.clip(dark, 0, 1) / np.pi)


def wear(mask, rng, strength):
    """Speckled dropouts, like a worn screen print."""
    return mask & (fbm(*mask.shape, rng, 5, 2) > strength)


def fit(path):
    """The painting cropped to its alpha and centred on the work canvas."""
    src = Image.open(path).convert("RGBA")
    src = src.crop(src.getchannel("A").point(lambda a: 255 if a > 24 else 0).getbbox())
    s = FIT / max(src.size)
    src = src.resize((max(1, round(src.width * s)), max(1, round(src.height * s))), Image.LANCZOS)
    canvas = Image.new("RGBA", (WORK, WORK), (0, 0, 0, 0))
    canvas.alpha_composite(src, ((WORK - src.width) // 2, (WORK - src.height) // 2))
    a = np.asarray(canvas, np.float32) / 255
    return a[..., :3], a[..., 3]


# ---------- treatment ----------

def badge(out, a, suit):
    """An off-white disc with an ink rim and the suit printed in its colour, bottom right."""
    h, w = a.shape
    cx, cy, r = w - 78, h - 78, 66
    disc, pip = Image.new("L", (w, h), 0), Image.new("L", (w, h), 0)
    ImageDraw.Draw(disc).ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    draw_suit(ImageDraw.Draw(pip), suit, cx, cy + 4, r * 1.15)
    disc, pip = np.asarray(disc) > 127, np.asarray(pip) > 127
    rim = grow(disc, 5) & ~disc
    out[rim] = INK
    out[disc] = PAPER
    out[pip] = BRICK if suit in "hd" else INK
    a[grow(disc, 5)] = 1


def spot_fill(rgb, body, rng):
    """The object's flat colour layer: its paint clustered into SPOTS inks by hue, each printed at
    its lit tone (the ink halftone adds the shading) and muted toward the paper. Near-grey inks print
    as paper or slate so neutral metal and cloth stay in the house palette."""
    h, w = body.shape
    L = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    # Cluster on chroma with a little luminance, so shade changes don't split one colour in two.
    feat = np.dstack([(rgb - L[..., None]) * 2.2, L[..., None] * 0.6])
    px = feat[body]
    sample = px[rng.choice(len(px), min(6000, len(px)), replace=False)]
    centers = [sample[rng.integers(len(sample))]]
    for _ in range(SPOTS - 1):  # k-means++ seeding
        d = np.min([((sample - c) ** 2).sum(1) for c in centers], axis=0)
        centers.append(sample[rng.choice(len(sample), p=d / d.sum())])
    centers = np.array(centers)
    for _ in range(15):
        lab = ((sample[:, None] - centers[None]) ** 2).sum(2).argmin(1)
        centers = np.array([sample[lab == k].mean(0) if (lab == k).any() else centers[k] for k in range(SPOTS)])
    label = ((feat.reshape(-1, 1, 4) - centers[None]) ** 2).sum(2).argmin(1).reshape(h, w)
    # Clean flat shapes, like a cut stencil.
    label = np.asarray(Image.fromarray(label.astype(np.uint8)).filter(ImageFilter.ModeFilter(7)))

    out = np.zeros((h, w, 3), np.float32)
    out[:] = PAPER
    tone = np.zeros((h, w), np.float32)
    for k in range(SPOTS):
        m = body & (label == k)
        if m.sum() < 40:
            continue
        bright = 0.5 * L[m] + 0.5 * rgb[m].max(1)
        lo, hi = np.percentile(bright, [5, 95])
        tone[m] = np.clip((bright - lo) / max(hi - lo, 1e-3), 0, 1)
        # Hue and saturation from the whole area (highlights are white or shifted); tone from its lit half.
        hh, s, _ = colorsys.rgb_to_hsv(*rgb[m].mean(0))
        v = colorsys.rgb_to_hsv(*rgb[m][L[m] >= np.percentile(L[m], 50)].mean(0))[2]
        if s < 0.18:
            ink = PAPER if v > 0.55 else SLATE if v > 0.32 else CHARCOAL_INK
        else:
            r, g, b = colorsys.hsv_to_rgb(hh, min(s * 1.1, 0.8) * 0.92, np.clip(v, 0.48, 0.90))
            ink = np.array([r, g, b], np.float32) * 255 * 0.86 + PAPER * 0.14
        out[m] = ink
    return out, tone


def treat(path, seed, suit=None):
    rng = np.random.default_rng(seed)
    rgb, alpha = fit(path)
    body = alpha > 0.5
    h, w = body.shape

    # Tone: brightness between luma and the brightest channel, so a saturated red is not read as
    # a shadow; normalised over the object only.
    L = 0.5 * (rgb @ np.array([0.299, 0.587, 0.114], np.float32)) + 0.5 * rgb.max(2)
    lo, hi = np.percentile(L[body], [4, 96])
    L = np.clip((L - lo) / max(hi - lo, 1e-3), 0, 1)
    # Spot colours: the painting's own colours, separated into a few flat inks. Shading reads half
    # against the whole object and half against each colour's own range, so a dark red stays red.
    fill, local = spot_fill(rgb, body, rng)
    L = blur(0.5 * L + 0.5 * local, 1.2)

    # Ink: solid shadows, halftone mid-tones, XDoG linework. Kept lighter than a one-colour print
    # so the spot colours carry the object.
    solid = L < 0.10 + (fbm(h, w, rng, 20) - 0.5) * 0.04
    dots = halftone((0.42 - L) / 0.34, 9, 45) & ~solid
    dog = blur(L, 1.6) - blur(L, 5.0)
    lines = dog < -0.045
    ink = (solid | lines | dots) & body

    # A thin off-white cut line, like a collage cut-out: a hairline of ink, then the cut edge.
    contour = grow(body, 2) & ~body
    cut = grow(body, 6) & ~grow(body, 2)

    out = fill
    out[wear(ink, rng, 0.08)] = INK
    out[contour] = INK
    out[cut] = PAPER
    a = grow(body, 6).astype(np.float32)

    # Paper flecks keep the flat fills from reading as vector art.
    flecks = (fbm(h, w, rng, 3, 2) > 0.82) & body & ~ink
    out[flecks] = out[flecks] * 0.85 + PAPER * 0.15
    if suit:
        badge(out, a, suit)

    img = Image.fromarray(np.dstack([out, a * 255]).clip(0, 255).astype(np.uint8), "RGBA")
    return img.resize((OUT, OUT), Image.LANCZOS)


def seed_for(name):
    return zlib.crc32(name.encode())


def sheet(path, names, tile=160, cols=8):
    font = ImageFont.truetype("arialbd.ttf", 13)
    rows = (len(names) + cols - 1) // cols
    img = Image.new("RGB", (cols * tile, rows * (tile + 18)), CHARCOAL)
    d = ImageDraw.Draw(img)
    for i, name in enumerate(names):
        icon = treat(SOURCES / f"{ICONS[name]}.png", seed_for(name), BADGES.get(name)).resize((tile - 16, tile - 16), Image.LANCZOS)
        x, y = (i % cols) * tile, (i // cols) * (tile + 18)
        d.rectangle([x + 4, y + 4, x + tile - 4, y + tile - 4], fill=tuple(SLATE.astype(int)))
        img.paste(icon, (x + 8, y + 8), icon)
        d.text((x + 6, y + tile), name.split("/")[1], fill=(240, 230, 207), font=font)
    img.save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet", help="write a preview sheet here instead of the icons")
    ap.add_argument("--only", help="comma-separated output names, e.g. Gear/Scalpel")
    args = ap.parse_args()
    names = args.only.split(",") if args.only else list(ICONS)
    if args.sheet:
        sheet(args.sheet, names)
        return
    for name in names:
        out = ITEMS / f"{name}.png"
        out.parent.mkdir(parents=True, exist_ok=True)
        treat(SOURCES / f"{ICONS[name]}.png", seed_for(name), BADGES.get(name)).save(out)
    print(f"wrote {len(names)} icons under {ITEMS}")


if __name__ == "__main__":
    main()
