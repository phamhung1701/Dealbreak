"""Sample palette tokens from the card, portrait and item art (Cut Sheet pre-step, visual-design §0).

Each sample is the per-channel median of the pixels inside one or more boxes (file pixel space) that
pass the token's colour filter, so grain speckle, outlines and halftone rims don't skew it. Prints
the median RGB and hex per token, and the CIE76 delta-E to the palette value
(art bible section 4; the first run against the eyeballed hexes is recorded in visual-design §0).

    python Tools/PrintArt/sample_palette.py                  # the §0 table
    python Tools/PrintArt/sample_palette.py FILE X0,Y0,X1,Y1 ...
"""
import statistics
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "Assets" / "Art"

def any_pixel(r, g, b):
    return True


def reddish(r, g, b):
    return r - max(g, b) > 50 and r > 140  # halftone dot bodies; skips the darker anti-aliased rims


def tealish(r, g, b):
    return g > r + 40 and b > r + 40


def mid_teal(r, g, b):
    return tealish(r, g, b) and r < 100  # the shaded facet, not the pale tinted body


def gold(r, g, b):
    return r > 180 and g > 120 and r - b > 100


# token: (palette hex, file, [(x0, y0, x1, y1), ...], pixel filter). The §0 coordinates were read by
# eye and several miss the fill, so each box is searched for the pixels that match the filter.
SAMPLES = {
    "cream": ("#EFE4CC", "Cards/Clubs/1c.png", [(262, 142, 278, 158), (112, 592, 128, 608), (392, 642, 408, 658), (142, 292, 158, 308)], any_pixel),
    "ink (pip)": ("#121212", "Cards/Clubs/1c.png", [(266, 326, 274, 334)], any_pixel),
    "ink (border)": ("#121212", "Cards/Clubs/1c.png", [(200, 5, 340, 10)], any_pixel),
    "red": ("#A84538", "Cards/Clubs/1c.png", [(280, 250, 460, 520)], reddish),
    "teal (Rook)": ("#4AA6B8", "Characters/Rook.png", [(150, 30, 260, 120)], tealish),
    "teal (Scanner)": ("#4AA6B8", "Items/Gear/Scanner.png", [(0, 0, 256, 256)], mid_teal),
    "mustard": ("#E6AB55", "Items/Gear/GoldPistol.png", [(185, 31, 205, 51)], gold),
}

# Shown for comparison only: Rook's bandana is bluer (hue 203) than the Scanner teal the palette takes.
REFERENCE_ONLY = {"teal (Rook)"}


def box_pixels(image, box, keep):
    x0, y0, x1, y1 = box
    pixels = []
    for py in range(max(0, y0), min(image.height, y1)):
        for px in range(max(0, x0), min(image.width, x1)):
            r, g, b, a = image.getpixel((px, py))
            if a >= 250 and keep(r, g, b):  # skip transparent and edge pixels
                pixels.append((r, g, b))
    return pixels


def median_rgb(pixels):
    return tuple(int(round(statistics.median(p[i] for p in pixels))) for i in range(3))


def to_hex(rgb):
    return "#{:02X}{:02X}{:02X}".format(*rgb)


def from_hex(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def to_lab(rgb):
    def linear(c):
        c /= 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

    r, g, b = (linear(c) for c in rgb)
    x = (0.4124 * r + 0.3576 * g + 0.1805 * b) / 0.95047
    y = 0.2126 * r + 0.7152 * g + 0.0722 * b
    z = (0.0193 * r + 0.1192 * g + 0.9505 * b) / 1.08883

    def f(t):
        return t ** (1 / 3) if t > 216 / 24389 else (24389 / 27 * t + 16) / 116

    fx, fy, fz = f(x), f(y), f(z)
    return 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)


def delta_e(a, b):
    return sum((p - q) ** 2 for p, q in zip(to_lab(a), to_lab(b))) ** 0.5


def sample(path, boxes, keep=any_pixel):
    image = Image.open(path).convert("RGBA")
    pixels = [p for box in boxes for p in box_pixels(image, box, keep)]
    if not pixels:
        raise SystemExit(f"{path}: no matching opaque pixels in {boxes}")
    return median_rgb(pixels), len(pixels)


def main(argv):
    if argv:
        boxes = [tuple(int(v) for v in arg.split(",")) for arg in argv[1:]]
        rgb, count = sample(Path(argv[0]), boxes)
        print(f"{to_hex(rgb)}  rgb{rgb}  n={count}")
        return
    print(f"{'token':<16}{'palette':<10}{'sample':<10}{'dE76':>6}  verdict")
    for token, (palette, file, boxes, keep) in SAMPLES.items():
        rgb, count = sample(ART / file, boxes, keep)
        de = delta_e(rgb, from_hex(palette))
        verdict = "reference" if token in REFERENCE_ONLY else "accept" if de <= 6 else "adopt sample?"
        print(f"{token:<16}{palette:<10}{to_hex(rgb):<10}{de:>6.1f}  {verdict}  (n={count})")


if __name__ == "__main__":
    main(sys.argv[1:])
