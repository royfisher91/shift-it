"""Contact sheets for the visual review rule.

Adapted from KT_AnimalSims/Docs/contact_sheet.py (quality lesson 15: reuse KT's tools rather
than writing a second one). Changes over the original: a title bar, per-image captions taken
from an optional sidecar .txt, a configurable thumb width, and it REFUSES to produce a sheet
from zero images instead of writing an empty one - a blank sheet that looks like a sheet is
the "check that did not run" failure wearing a picture.

usage:
  python contact_sheet.py OUT.png TITLE COLS THUMBW img1 [img2 ...]

A caption comes from <image>.txt if that file exists, otherwise from the file stem.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont


def font(size):
    for name in ("segoeuib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def main():
    if len(sys.argv) < 6:
        print(__doc__)
        return 2

    out, title, cols, tw = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
    files = [f for f in sys.argv[5:] if os.path.isfile(f)]
    missing = [f for f in sys.argv[5:] if not os.path.isfile(f)]

    if not files:
        print("CONTACT SHEET NOT WRITTEN: none of the %d requested images exist."
              % len(sys.argv[5:]))
        for f in missing:
            print("  missing: %s" % f)
        return 1

    ims = []
    for f in files:
        try:
            ims.append((f, Image.open(f).convert("RGB")))
        except Exception as e:                      # a corrupt png must be loud, not skipped
            print("  UNREADABLE: %s (%s)" % (f, e))

    if not ims:
        print("CONTACT SHEET NOT WRITTEN: every image failed to open.")
        return 1

    th = int(tw * ims[0][1].height / ims[0][1].width)
    cap_h = 30
    pad = 8
    title_h = 64
    rows = (len(ims) + cols - 1) // cols

    w = cols * (tw + pad) + pad
    h = title_h + rows * (th + cap_h + pad) + pad
    sheet = Image.new("RGB", (w, h), (22, 23, 27))
    d = ImageDraw.Draw(sheet)

    d.rectangle([0, 0, w, title_h], fill=(32, 34, 40))
    d.text((pad + 4, 16), title, fill=(255, 212, 90), font=font(30))
    d.text((w - 250, 24), "%d image(s)" % len(ims), fill=(150, 155, 165), font=font(18))

    f_cap = font(19)
    for i, (path, im) in enumerate(ims):
        cx = pad + (i % cols) * (tw + pad)
        cy = title_h + pad + (i // cols) * (th + cap_h + pad)
        sheet.paste(im.resize((tw, th), Image.LANCZOS), (cx, cy))
        d.rectangle([cx, cy, cx + tw, cy + th], outline=(70, 74, 84))

        side = os.path.splitext(path)[0] + ".txt"
        if os.path.isfile(side):
            with open(side, encoding="utf-8") as fh:
                caption = fh.read().strip().splitlines()[0]
        else:
            caption = os.path.splitext(os.path.basename(path))[0].replace("_", " ")
        d.text((cx + 3, cy + th + 5), caption[:90], fill=(226, 230, 238), font=f_cap)

    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    sheet.save(out)
    print("sheet written: %s  (%dx%d, %d images)" % (out, w, h, len(ims)))
    for f in missing:
        print("  NOTE missing input: %s" % f)
    return 0


if __name__ == "__main__":
    sys.exit(main())
