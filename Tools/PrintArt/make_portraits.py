"""Dealbreak character portraits: painted portraits -> the same halftone cut-outs as the item icons.

Each playable character's portrait (neutral expression from the painted "Tricks & Gears sample"
character pack) is reprinted by make_item_icons.treat and written to Assets/Art/Characters.
Seeded noise only: a rerun reproduces the files exactly.

    python Tools/PrintArt/make_portraits.py                    # writes Assets/Art/Characters/*.png
    python Tools/PrintArt/make_portraits.py --sheet out.png    # preview sheet only
"""
import argparse
from pathlib import Path

from PIL import Image

import make_item_icons as icons

CHARACTERS = Path(__file__).resolve().parents[2] / "Assets/Art/Characters"
OUT = 384  # portraits draw larger than item icons (the character select tiles)

# Output name (Assets/Art/Characters/<name>.png) -> source painting (item_sources/<pack>/<n>.png).
PORTRAITS = {
    "Rook": "Character/1",
    "Dex": "Character/7",
    "Nova": "Character/13",
    "Scarlet": "Character 2/1",
    "Glitch": "Character 2/7",
    "Velvet": "Character 2/13",
}


def portrait(name):
    icons.OUT = OUT
    return icons.treat(icons.SOURCES / f"{PORTRAITS[name]}.png", icons.seed_for(f"Characters/{name}"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet", help="write a preview sheet here instead of the portraits")
    args = ap.parse_args()
    if args.sheet:
        tile = 300
        img = Image.new("RGB", (tile * len(PORTRAITS), tile), icons.CHARCOAL)
        for i, name in enumerate(PORTRAITS):
            face = portrait(name).resize((tile - 20, tile - 20), Image.LANCZOS)
            img.paste(face, (i * tile + 10, 10), face)
        img.save(args.sheet)
        return
    CHARACTERS.mkdir(parents=True, exist_ok=True)
    for name in PORTRAITS:
        portrait(name).save(CHARACTERS / f"{name}.png")
    print(f"wrote {len(PORTRAITS)} portraits under {CHARACTERS}")


if __name__ == "__main__":
    main()
