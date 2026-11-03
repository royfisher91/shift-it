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


def make_cover(src, dst, anchor_y=0.5, anchor_x=0.5, fit=False, trim_bottom=0.0,
               erase=None):
    """
    fit=False crops a 630x500 window out of the frame. That suits a gameplay shot but destroys a
    title screen: the cover is 1.26:1 against the game's 1.78:1, so a centre crop throws away about
    280 pixels from each side - which took the S off SHIFT, the end off IT! and half the SETTINGS
    button with it.

    fit=True scales the WHOLE frame to the cover width and extends the top and bottom edge rows to
    fill the remaining height. The game's sky is a horizontal gradient, so stretching its edge rows
    continues it rather than showing letterbox bars.
    """
    im = Image.open(src).convert("RGB")
    # Cut interface chrome off the bottom before fitting. The title screen carries a SETTINGS
    # button in its corner, which belongs in the game and not on the store page.
    if trim_bottom > 0:
        im = im.crop((0, 0, im.width, int(round(im.height * (1.0 - trim_bottom)))))
    if fit:
        scaled = im.resize((COVER_W, int(round(im.height * COVER_W / im.width))), Image.LANCZOS)
        card = Image.new("RGB", (COVER_W, COVER_H))
        top = (COVER_H - scaled.height) // 2
        if top > 0:
            card.paste(scaled.crop((0, 0, COVER_W, 1)).resize((COVER_W, top), Image.NEAREST), (0, 0))
            bot = COVER_H - top - scaled.height
            if bot > 0:
                card.paste(
                    scaled.crop((0, scaled.height - 1, COVER_W, scaled.height))
                          .resize((COVER_W, bot), Image.NEAREST),
                    (0, top + scaled.height))
        card.paste(scaled, (0, max(0, top)))
        im = card
    else:
        im = crop_to_aspect(im, COVER_W / COVER_H, anchor_y, anchor_x)
        im = im.resize((COVER_W, COVER_H), Image.LANCZOS)
    # Paint out a rectangle of interface chrome by extending the pixels immediately to its left.
    # The backdrop there is a smooth horizontal gradient, so this is seamless - and it is a far
    # safer way to lose a button than trimming the frame, which cut through the player robot and
    # smeared it into a column the moment the edge-extension reached live art.
    if erase:
        ex, ey, ew, eh = erase
        x0, y0 = int(ex * im.width), int(ey * im.height)
        x1, y1 = x0 + int(ew * im.width), y0 + int(eh * im.height)
        x0, y0 = max(0, x0), max(0, y0)
        x1, y1 = min(im.width, x1), min(im.height, y1)
        if x0 > 0 and x1 > x0 and y1 > y0:
            strip = im.crop((x0 - 1, y0, x0, y1)).resize((x1 - x0, y1 - y0), Image.NEAREST)
            im.paste(strip, (x0, y0))

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
    ap.add_argument("--cover-trim-bottom", type=float, default=0.0,
                    help="fraction of the source height to cut off the bottom first")
    ap.add_argument("--cover-erase", default=None,
                    help="x,y,w,h as fractions of the cover - chrome to paint out")
    ap.add_argument("--cover-fit", action="store_true",
                    help="fit the whole frame and extend the edges instead of cropping")
    ap.add_argument("--shots", nargs="*", default=None)
    args = ap.parse_args()

    os.makedirs(args.out, exist_ok=True)
    R = args.review

    cover_src = args.cover_src or os.path.join(R, "sheet_ui", "ui_title_modeB_only.png")
    if not os.path.exists(cover_src):
        print("MISSING cover source: " + cover_src)
        return 2
    cover = make_cover(cover_src, os.path.join(args.out, "cover_630x500.png"),
                       args.cover_y, args.cover_x, args.cover_fit, args.cover_trim_bottom,
                       tuple(float(v) for v in args.cover_erase.split(',')) if args.cover_erase else None)
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
