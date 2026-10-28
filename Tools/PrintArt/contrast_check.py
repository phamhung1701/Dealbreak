"""WCAG contrast table for the Cut Sheet palette (visual-design §0).

Prints every pair §0 lists with its WCAG 2.x ratio and the floor it must hold. Override any token
to test a sampled value, for example after sample_palette.py:

    python Tools/PrintArt/contrast_check.py
    python Tools/PrintArt/contrast_check.py red=#B8382E teal=#2E9AA8 mustard=#E0A82E

Exits 1 if a gated pair falls below its floor.
"""
import sys

PALETTE = {
    "ink": "#121212",
    "cream": "#EFE4CC",
    "board": "#1C1814",
    "boardDefense": "#15110E",
    "red": "#A84538",
    "teal": "#4AA6B8",
    "mustard": "#E6AB55",
    "kraft": "#B8956A",
    "muted": "#8C8270",
    "disabledRed": "#722A22",
}

# (foreground, background, floor or None, note). Floors: 4.5 text, 3.0 large text or non-text.
# A floor of None is informational; "banned" pairs are listed to show they stay below 4.5.
PAIRS = [
    ("ink", "cream", 4.5, "type on stock"),
    ("cream", "board", 4.5, "type on the board"),
    ("cream", "boardDefense", 4.5, "type on the defense board"),
    ("cream", "red", 4.5, "INCOMING, ATTACK labels (3.0 where large)"),
    ("ink", "red", None, "informational"),
    ("red", "board", 3.0, "non-text only"),
    ("ink", "teal", 4.5, "type on selection"),
    ("cream", "teal", None, "banned"),
    ("teal", "board", 3.0, "non-text: crop marks"),
    ("ink", "mustard", 4.5, "Credits stamp"),
    ("mustard", "board", 3.0, "non-text: coin, chip"),
    ("ink", "kraft", 4.5, "Tricks heading"),
    ("cream", "kraft", None, "banned"),
    ("kraft", "board", 3.0, "non-text: strips"),
    ("ink", "muted", 4.5, "disabled labels"),
    ("cream", "muted", None, "glyphs need the ink outline"),
    ("cream", "disabledRed", 4.5, "disabled red label"),
]


def luminance(hex_value):
    hex_value = hex_value.lstrip("#")

    def linear(c):
        c = int(c, 16) / 255
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4

    r, g, b = (linear(hex_value[i:i + 2]) for i in (0, 2, 4))
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def ratio(a, b):
    la, lb = sorted((luminance(a), luminance(b)), reverse=True)
    return (la + 0.05) / (lb + 0.05)


def main(argv):
    palette = dict(PALETTE)
    for arg in argv:
        token, _, value = arg.partition("=")
        if token not in palette or not value:
            raise SystemExit(f"unknown override {arg!r}; tokens: {', '.join(palette)}")
        palette[token] = value if value.startswith("#") else "#" + value
    failed = 0
    print(f"{'pair':<28}{'ratio':>7}  {'floor':>5}  note")
    for fg, bg, floor, note in PAIRS:
        value = ratio(palette[fg], palette[bg])
        status = "" if floor is None else ("ok" if value >= floor else "FAIL")
        failed += status == "FAIL"
        label = f"{fg} {palette[fg]} on {bg}"
        print(f"{label:<28}{value:>7.2f}  {floor if floor else '-':>5}  {status:<4} {note}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main(sys.argv[1:])
