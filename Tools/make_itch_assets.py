"""
Builds the itch.io upload set: the 630x500 cover image and the page screenshots.

The cover is cut from a REAL capture of the running game rather than drawn separately, for the same
reason every sprite in this project is generated: what the page promises should be what the game
shows. itch scales the cover down hard in listings, so this crops to the logo and the robots and
leaves the empty backdrop out, then sharpens slightly to survive that downscale.

usage:
  python Tools/make_itch_assets.py --review D:/KT/ShiftIt/Review --out D:/KT/ShiftIt/itch
"""

import argparse
import os
import sys

try:
    from PIL import Image, ImageFilter
except ImportError:
    print("PIL (Pillow) is required")
    sys.exit(2)

COVER_W, COVER_H = 630, 500
SHOT_W = 1280                      # itch shows screenshots at up to ~1280 wide


def crop_to_aspect(im, aspect, anchor_y=0.5, anchor_x=0.5):
    """Crop the largest region of `im` with the given width/height ratio."""
    w, h = im.size
    if w / h > aspect:
        nw, nh = int(round(h * aspect)), h
    else:
        nw, nh = w, int(round(w / aspect))
    x = int(round((w - nw) * anchor_x))
    y = int(round((h - nh) * anchor_y))
    return im.crop((x, y, x + nw, y + nh))


def make_cover(src, dst, anchor_y=0.5, anchor_x=0.5):
    im = Image.open(src).convert("RGB")
    im = crop_to_aspect(im, COVER_W / COVER_H, anchor_y, anchor_x)
    im = im.resize((COVER_W, COVER_H), Image.LANCZOS)
    # A touch of sharpening, because itch downscales covers again for the browse grid.
    im = im.filter(ImageFilter.UnsharpMask(radius=1.2, percent=55, threshold=3))
    im.save(dst, "PNG", optimize=True)
    return dst


def make_shot(src, dst):
    im = Image.open(src).convert("RGB")
    if im.width > SHOT_W:
        im = im.resize((SHOT_W, int(round(im.height * SHOT_W / im.width))), Image.LANCZOS)
    im.save(dst, "PNG", optimize=True)
    return dst


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--review", default="D:/KT/ShiftIt/Review")
    ap.add_argument("--out", default="D:/KT/ShiftIt/itch")
    ap.add_argument("--cover-src", default=None, help="image to cut the cover from")
    ap.add_argument("--cover-y", type=float, default=0.5)
    ap.add_argument("--cover-x", type=float, default=0.5)
    ap.add_argument("--shots", nargs="*", default=None)
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    R = args.review

    cover_src = args.cover_src or os.path.join(R, "sheet_ui", "ui_title_modeB_only.png")
    if not os.path.exists(cover_src):
        print("MISSING cover source: " + cover_src)
        return 2
    cover = make_cover(cover_src, os.path.join(args.out, "cover_630x500.png"),
                       args.cover_y, args.cover_x)
    print("cover   " + cover)

    shots = args.shots
    if not shots:
        shots = [
            os.path.join(R, "sheet_zones", "zone1_factory_floor.png"),
            os.path.join(R, "sheet_zones", "zone2_rooftop.png"),
            os.path.join(R, "sheet_zones", "zone3_scrapyard.png"),
            os.path.join(R, "sheet_zones", "zone4_launchpad.png"),
            os.path.join(R, "sheet_ui", "ui_settings.png"),
        ]
    n = 0
    missing = []
    for i, s in enumerate(shots, 1):
        if not os.path.exists(s):
            missing.append(s)
            continue
        out = os.path.join(args.out, "screenshot_%d.png" % i)
        make_shot(s, out)
        print("shot %d  %s" % (i, out))
        n += 1

    # A check that could not run must never count as a pass.
    if missing:
        print("")
        print("MISSING %d source image(s):" % len(missing))
        for m in missing:
            print("  " + m)
    print("")
    print("RESULT cover=1 screenshots=%d missing=%d" % (n, len(missing)))
    return 0 if (n >= 3 and not missing) else 1


if __name__ == "__main__":
    sys.exit(main())
