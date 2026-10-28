"""Dealbreak Cut Sheet UI sprites: the combat HUD's printed stock, tags, chips, marks and overlays.

Builds the nine Cut Sheet sprites (production/ui/hud/visual-design.md section 2a, plus the dashed
slot from 2b) and the Credits coin. Shapes are drawn at 4x and box-averaged to 1x on premultiplied
colour, so edges are anti-aliased without ringing. Tinted sprites are near-white with the ink border
baked in; transparent pixels carry their nearest neighbour's colour (no dark fringes when filtered).
Noise comes from fixed seeds and wraps around, so tiles are seamless and a rerun is byte-identical.
Import settings, 9-slice borders and the CutSheetUI atlas are applied in Unity by
Assets/Editor/CutSheetAssetAuthoring.cs (Dealbreak/Art/Apply Cut Sheet Import Settings).

    pip install -r Tools/PrintArt/requirements.txt
    python Tools/PrintArt/make_ui_sprites.py                    # writes Assets/Art/UI/CutSheet/*.png and the coin
    python Tools/PrintArt/make_ui_sprites.py --sheet out.png    # preview sheet only
"""
import argparse
import hashlib
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
CUT_SHEET = ROOT / "Assets/Art/UI/CutSheet"
SPRITES = ROOT / "Assets/Art/UI/Sprites"

# Cut Sheet palette (visual-design section 1b): inkPrint for baked borders, near-white stock, mustard for Credits.
INK = np.array([0x12, 0x12, 0x12], np.float32) / 255
WHITE = np.array([0xFA, 0xFA, 0xFA], np.float32) / 255
MUSTARD = np.array([0xE0, 0xA8, 0x2E], np.float32) / 255

SS = 4  # supersampling factor for drawn shapes


# ---- Shared primitives ----

def grid(w, h):
    """Sample centres of a w x h sprite at SS x SS per pixel, in 1x pixel units (y down)."""
    ys, xs = np.mgrid[0:h * SS, 0:w * SS].astype(np.float32)
    return (xs + .5) / SS, (ys + .5) / SS


def down(a, w, h):
    """Box-average a supersampled array (h*SS, w*SS[, c]) down to (h, w[, c])."""
    shape = (h, SS, w, SS) + a.shape[2:]
    return a.reshape(shape).mean(axis=(1, 3))


def layers(w, h, inks):
    """Composite supersampled coverage masks, back to front: inks = [(mask, rgb, alpha)].
    Returns an 8-bit straight-alpha RGBA image."""
    premul = np.zeros((h * SS, w * SS, 3), np.float32)
    alpha = np.zeros((h * SS, w * SS), np.float32)
    for mask, rgb, a in inks:
        m = mask.astype(np.float32) * a
        premul = premul * (1 - m[..., None]) + rgb * m[..., None]
        alpha = alpha * (1 - m) + m
    return finish(down(premul, w, h), down(alpha, w, h))


def finish(premul, alpha):
    """Un-premultiply, dilate colour into transparent pixels, quantise to RGBA8."""
    rgb = np.where(alpha[..., None] > 0, premul / np.maximum(alpha[..., None], 1e-6), 0)
    rgb = dilate(rgb, alpha > 0)
    out = np.dstack([np.clip(np.rint(rgb * 255), 0, 255), np.clip(np.rint(alpha * 255), 0, 255)])
    return Image.fromarray(out.astype(np.uint8), "RGBA")


def dilate(rgb, known):
    """Give every unknown pixel the mean colour of its known 8-neighbours, ring by ring."""
    rgb, known = rgb.copy(), known.copy()
    if not known.any():
        return rgb
    while not known.all():
        acc = np.zeros_like(rgb)
        cnt = np.zeros(known.shape, np.float32)
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                k = np.zeros_like(known)
                c = np.zeros_like(rgb)
                ys = slice(max(dy, 0), known.shape[0] + min(dy, 0))
                yd = slice(max(-dy, 0), known.shape[0] + min(-dy, 0))
                xs = slice(max(dx, 0), known.shape[1] + min(dx, 0))
                xd = slice(max(-dx, 0), known.shape[1] + min(-dx, 0))
                k[yd, xd] = known[ys, xs]
                c[yd, xd] = rgb[ys, xs]
                acc += c * k[..., None]
                cnt += k
        grow = ~known & (cnt > 0)
        rgb[grow] = acc[grow] / cnt[grow][:, None]
        known |= grow
    return rgb


def wrapped_noise(h, w, cell, seed, octaves=3):
    """Seamless fbm in [0, 1]: lattice values wrap at the tile edge (w and h must divide by cell)."""
    rng = np.random.RandomState(seed)
    total = np.zeros((h, w), np.float32)
    amp, norm = 1.0, 0.0
    for _ in range(octaves):
        gh, gw = h // cell, w // cell
        lattice = rng.rand(gh, gw).astype(np.float32)
        ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
        fy, fx = ys / cell, xs / cell
        iy, ix = np.floor(fy).astype(int), np.floor(fx).astype(int)
        ty, tx = fy - iy, fx - ix
        ty, tx = ty * ty * (3 - 2 * ty), tx * tx * (3 - 2 * tx)
        a = lattice[iy % gh, ix % gw]
        b = lattice[iy % gh, (ix + 1) % gw]
        c = lattice[(iy + 1) % gh, ix % gw]
        d = lattice[(iy + 1) % gh, (ix + 1) % gw]
        total += amp * ((a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty)
        norm += amp
        amp *= .5
        cell = max(cell // 2, 1)
    return total / norm


def rounded_rect_sdf(x, y, x0, y0, x1, y1, r):
    """Signed distance to a rectangle with corner radius r (negative inside)."""
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    hx, hy = (x1 - x0) / 2 - r, (y1 - y0) / 2 - r
    qx, qy = np.abs(x - cx) - hx, np.abs(y - cy) - hy
    outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    return outside + np.minimum(np.maximum(qx, qy), 0) - r


def rim_centres(count, radius, cx, cy, phase=0.0):
    t = (np.arange(count) + phase) * 2 * np.pi / count
    return [(cx + radius * np.cos(a), cy + radius * np.sin(a)) for a in t]


# ---- 1. Panel stock: 64x64, border 4. Flat ink strips; seamless grain in the 56x56 centre (Image Tiled) ----

def panel_stock():
    size, border, centre = 64, 4, 56
    grain = wrapped_noise(centre, centre, 8, 101) * .6 + np.random.RandomState(102).rand(centre, centre) * .4
    grain = (grain - grain.min()) / (grain.max() - grain.min()) * 2 - 1      # [-1, 1]
    v = np.full((size, size), WHITE[0], np.float32)
    v[border:-border, border:-border] = WHITE[0] + grain * .02             # +/-2% luminance
    ys, xs = np.mgrid[0:size, 0:size]
    edge = np.minimum(np.minimum(xs, ys), np.minimum(size - 1 - xs, size - 1 - ys))
    v = np.where(edge < 3, INK[0], v)                                       # 3px ink
    v = np.where(edge == 3, (INK[0] + WHITE[0]) / 2, v)                     # 1px AA, flat along each strip
    rgb = np.repeat(v[..., None], 3, axis=2)
    return finish(rgb, np.ones((size, size), np.float32))


# ---- 2. Tag notch: 64x40 ticket stub, 2px ink border, radius-6 scallops cut into the four corners ----

def tag_notch():
    w, h, r, stroke = 64, 40, 6, 2
    x, y = grid(w, h)
    corners = [(0, 0), (w, 0), (0, h), (w, h)]
    near = np.minimum.reduce([np.hypot(x - cx, y - cy) for cx, cy in corners])
    body = near > r
    fill = (near > r + stroke) & (x > stroke) & (x < w - stroke) & (y > stroke) & (y < h - stroke)
    return layers(w, h, [(body, INK, 1), (fill, WHITE, 1)])


# ---- 3. Chip stamp: 96x96 disc with 16 scallops on the rim, 2px ink border ----

def scalloped_sdf(x, y, cx, cy, base, bump_ring, bump_r, count):
    d = np.hypot(x - cx, y - cy) - base
    for bx, by in rim_centres(count, bump_ring, cx, cy):
        d = np.minimum(d, np.hypot(x - bx, y - by) - bump_r)
    return d


def chip_stamp():
    size = 96
    x, y = grid(size, size)
    d = scalloped_sdf(x, y, 48, 48, 42, 40.5, 6, 16)                       # outer edge at 46.5
    return layers(size, size, [(d < 0, INK, 1), (d < -2, WHITE, 1)])


# ---- 4. Sticker cut line: 64x64, a 2px near-white ring, outer corner radius 6, open centre ----

def sticker_cutline():
    size = 64
    x, y = grid(size, size)
    d = rounded_rect_sdf(x, y, 0, 0, size, size, 6)
    return layers(size, size, [((d < 0) & (d > -2), WHITE, 1)])


# ---- 5. Crop mark: 32x32 top-left bracket, arms 22, 3px near-white stroke, 2px ink outline each side ----

def mark_crop():
    size, corner, arm, stroke, outline = 32, 4, 22, 3, 2
    x, y = grid(size, size)

    def bracket(grow):
        lo, hi = corner - grow, corner + arm + grow
        horizontal = (x >= lo) & (x < hi) & (y >= lo) & (y < corner + stroke + grow)
        vertical = (y >= lo) & (y < hi) & (x >= lo) & (x < corner + stroke + grow)
        return horizontal | vertical

    return layers(size, size, [(bracket(outline), INK, 1), (bracket(0), WHITE, 1)])


# ---- 6. Grain overlay: 256x256 seamless grey noise, alpha at most 5%, mean about 2% ----

def overlay_grain():
    size = 256
    n = wrapped_noise(size, size, 16, 601) * .5 + np.random.RandomState(602).rand(size, size) * .5
    n = (n - n.min()) / (n.max() - n.min())
    lo, hi = .05, 20.0                                                      # gamma so the mean alpha is 0.02
    for _ in range(60):
        g = (lo + hi) / 2
        lo, hi = (g, hi) if (n ** g).mean() > .4 else (lo, g)
    alpha = np.rint(12 * n ** ((lo + hi) / 2)) / 255                        # 12/255 = 0.047 peak
    rgb = np.full((size, size, 3), .5, np.float32)
    out = np.dstack([np.rint(rgb * 255), np.rint(alpha * 255)])
    return Image.fromarray(out.astype(np.uint8), "RGBA")


# ---- 7. Halftone overlay: 16x16 white dots on a 45 degree lattice, radius 3.2, wrap-around ----

def overlay_halftone():
    size = 16
    x, y = grid(size, size)
    d = np.full(x.shape, np.inf, np.float32)
    for cx, cy in ((4, 4), (12, 12)):
        for ox in (-size, 0, size):
            for oy in (-size, 0, size):
                d = np.minimum(d, np.hypot(x - cx - ox, y - cy - oy))
    return layers(size, size, [(d < 3.2, np.ones(3, np.float32), 1)])


# ---- 8. Hatch overlay: 12x12, 45 degree ink stripes at 50% alpha, 2px wide, pitch 12 along x ----

def overlay_hatch():
    size = 12
    x, y = grid(size, size)
    stripe = np.mod(x + y, size) < 2 * np.sqrt(2)                           # 2px across the stripe
    return layers(size, size, [(stripe, INK, .5)])


# ---- 9. Dashed slot: 70x70, border 10, 2px near-white dashes 6 on 4 off (period 10 tiles the 50px strip) ----

def slot_dashed():
    size, inset, stroke, period, on = 70, 4, 2, 10, 6
    x, y = grid(size, size)
    lo, hi = inset, size - inset
    dash_x = np.mod(np.floor(x) - 2, period) < on
    dash_y = np.mod(np.floor(y) - 2, period) < on
    top = (y >= lo) & (y < lo + stroke) & (x >= lo) & (x < hi) & dash_x
    bottom = (y >= hi - stroke) & (y < hi) & (x >= lo) & (x < hi) & dash_x
    left = (x >= lo) & (x < lo + stroke) & (y >= lo) & (y < hi) & dash_y
    right = (x >= hi - stroke) & (x < hi) & (y >= lo) & (y < hi) & dash_y
    return layers(size, size, [(top | bottom | left | right, WHITE, 1)])


# ---- Credits coin: 64x64 mustard disc, 2px ink outline, 12 notches cut into the rim ----

def credits_coin():
    size = 64
    x, y = grid(size, size)
    d = np.hypot(x - 32, y - 32) - 30
    for nx, ny in rim_centres(12, 30.5, 32, 32, .5):
        d = np.maximum(d, 3.5 - np.hypot(x - nx, y - ny))                   # notch: subtract a small disc
    return layers(size, size, [(d < 0, INK, 1), (d < -2, MUSTARD, 1)])


# Output path -> builder. Order is the manifest order.
OUTPUTS = {
    CUT_SHEET / "ui_panel_stock_9s.png": panel_stock,
    CUT_SHEET / "ui_tag_notch_9s.png": tag_notch,
    CUT_SHEET / "ui_chip_stamp.png": chip_stamp,
    CUT_SHEET / "ui_sticker_cutline_9s.png": sticker_cutline,
    CUT_SHEET / "ui_mark_crop.png": mark_crop,
    CUT_SHEET / "ui_overlay_grain.png": overlay_grain,
    CUT_SHEET / "ui_overlay_halftone.png": overlay_halftone,
    CUT_SHEET / "ui_overlay_hatch.png": overlay_hatch,
    CUT_SHEET / "ui_slot_dashed_9s.png": slot_dashed,
    SPRITES / "Credits_Coin.png": credits_coin,
}


def pixel_hash(img):
    """SHA-256 of the RGBA bytes bottom row first: what Unity's Texture2D.GetPixels32 returns.
    CutSheetAssetsTests pins these, so the test is independent of PNG encoder versions."""
    return hashlib.sha256(np.flipud(np.asarray(img.convert("RGBA"))).tobytes()).hexdigest()


def sheet(path, scale=4, pad=8):
    imgs = [build() for build in OUTPUTS.values()]
    w = sum(i.width * scale + pad for i in imgs) + pad
    h = max(i.height for i in imgs) * scale + pad * 2
    out = Image.new("RGBA", (w, h), (0x1C, 0x18, 0x14, 255))
    x = pad
    for img in imgs:
        big = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
        out.alpha_composite(big, (x, pad))
        x += big.width + pad
    out.save(path)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet", help="write a preview sheet here instead of the sprites")
    args = ap.parse_args()
    if args.sheet:
        sheet(args.sheet)
        return
    for path, build in OUTPUTS.items():
        img = build()
        path.parent.mkdir(parents=True, exist_ok=True)
        img.save(path)
        print(f"{path.relative_to(ROOT).as_posix()}  {img.width}x{img.height}  {pixel_hash(img)}")
    print(f"wrote {len(OUTPUTS)} sprites")


if __name__ == "__main__":
    main()
