"""Dealbreak Gear and Trick icons: painted item art -> punk-poster screen print.

Each source is cut to its silhouette and reprinted in the game palette: cream paper body,
brick where the painting is warm and saturated, ink shading as solid fills and halftone dots,
ink linework, a thick ink keyline and a misregistered brick shadow. The background stays
transparent so the icon sits on the slot's own frame. Seeded noise only: a rerun reproduces
the files exactly.

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

INK = np.array([14, 17, 19], np.float32)
PAPER = np.array([240, 230, 207], np.float32)
BRICK = np.array([168, 69, 56], np.float32)
SLATE = np.array([74, 92, 105], np.float32)
CHARCOAL = (27, 33, 40)

WORK = 512               # processing canvas; the object fits inside FIT
FIT = 404
OUT = 256                # written texture size

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
    "Gear/DetourPin": "Drone/5",
    "Gear/DiamondDriver": "Energy/46",
    "Gear/EchoDriver": "Energy/20",
    "Gear/FieldTablet": "Drone/29",
    "Gear/FirstAidKit": "Medicine/16",
    "Gear/FlipSwitch": "Drone/45",
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
    """A paper disc with an ink rim and the suit printed in its colour, bottom right."""
    h, w = a.shape
    cx, cy, r = w - 78, h - 78, 66
    disc, pip = Image.new("L", (w, h), 0), Image.new("L", (w, h), 0)
    ImageDraw.Draw(disc).ellipse([cx - r, cy - r, cx + r, cy + r], fill=255)
    draw_suit(ImageDraw.Draw(pip), suit, cx, cy + 4, r * 1.15)
    disc, pip = np.asarray(disc) > 127, np.asarray(pip) > 127
    rim = grow(disc, 5) & ~disc
    out[shift(grow(disc, 5), 8, 7)] = INK
    out[rim] = INK
    out[disc] = PAPER
    out[pip] = BRICK if suit in "hd" else INK
    a[grow(disc, 5) | shift(grow(disc, 5), 8, 7)] = 1


def treat(path, seed, suit=None):
    rng = np.random.default_rng(seed)
    rgb, alpha = fit(path)
    body = alpha > 0.5
    h, w = body.shape

    # Tone: luma normalised over the object only.
    L = rgb @ np.array([0.299, 0.587, 0.114], np.float32)
    lo, hi = np.percentile(L[body], [4, 96])
    L = np.clip((L - lo) / max(hi - lo, 1e-3), 0, 1)
    L = blur(L, 1.2)

    # Spot colour: warm, saturated paint prints brick; everything else prints on paper.
    mx, mn = rgb.max(2), rgb.min(2)
    sat = np.where(mx > 1e-3, (mx - mn) / np.maximum(mx, 1e-3), 0)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    hue = np.degrees(np.arctan2(np.sqrt(3) * (g - b), 2 * r - g - b)) % 360
    warm = ((hue < 48) | (hue > 300)) & (sat > 0.42) & (mx > 0.18)
    warm = blur(warm.astype(np.float32), 2.0) > 0.5

    # Ink: solid shadows, halftone mid-tones, XDoG linework. Deep reds keep their brick.
    solid = L < np.where(warm, 0.08, 0.24) + (fbm(h, w, rng, 20) - 0.5) * 0.05
    dots = halftone((0.70 - L) / 0.46, 9, 45) & ~solid
    dog = blur(L, 1.6) - blur(L, 5.0)
    lines = dog < -0.045
    ink = (solid | lines | dots) & body

    # Sticker edge: a hairline of ink, a paper border, then a misregistered brick shadow.
    contour = grow(body, 3) & ~body
    keyline = grow(body, 11)
    shadow = shift(keyline, 14, 12) & ~keyline

    out = np.zeros((h, w, 3), np.float32)
    out[:] = PAPER
    out[warm] = BRICK
    out[wear(ink, rng, 0.08)] = INK
    out[contour] = INK
    out[shadow] = BRICK
    a = (keyline | wear(shadow, rng, 0.10)).astype(np.float32)

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
