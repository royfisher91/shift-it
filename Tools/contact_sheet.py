"""Contact sheets for the visual review rule.

Adapted from KT_AnimalSims/Docs/contact_sheet.py (quality lesson 15: reuse KT's tools rather
than writing a second one). Changes over the original: a title bar, per-image captions taken
from an optional sidecar .txt, a configurable thumb width, and it REFUSES to produce a sheet
from zero images instead of writing an empty one - a blank sheet that looks like a sheet is
the "check that did not run" failure wearing a picture.

usage:
  python contact_sheet.py OUT.png TITLE COLS THUMBW "img1[@x,y,w,h][|caption]" [img2 ...]

A caption comes from <image>.txt if that file exists, otherwise from the file stem.

An image may carry a CROP, appended as @x,y,w,h in FRACTIONS of the image (0-1), so a sheet can be
framed tight on the thing it is about rather than showing four fifths of empty backdrop:

  shot.png@0.30,0.10,0.40,0.45      # the middle-left of the frame

A CAPTION may be appended after a pipe. That matters when several crops come from the SAME source
image: without it they all inherit one sidecar caption and the sheet says the same wrong thing
three times.

  shot.png@0,0,0.26,0.2|Score and combo, top left
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


def split_spec(spec):
    """"path[@x,y,w,h][|caption]" -> (path, crop or None, caption or None)."""
    caption = None
    if "|" in spec:
        spec, _, caption = spec.partition("|")
    if "@" not in spec:
        return spec, None, caption
    path, _, rect = spec.rpartition("@")
    try:
        x, y, w, h = (float(v) for v in rect.split(","))
    except ValueError:
        return spec, None, caption
    return path, (x, y, w, h), caption


def fit(draw, text, font_obj, max_px):
    """Trim text until it fits the column, so captions cannot run into their neighbours."""
    if draw.textlength(text, font=font_obj) <= max_px:
        return text
    while text and draw.textlength(text + "...", font=font_obj) > max_px:
        text = text[:-1]
    return text.rstrip() + "..."


def apply_crop(im, crop):
    if not crop:
        return im
    x, y, w, h = crop
    box = (int(x * im.width), int(y * im.height),
           int((x + w) * im.width), int((y + h) * im.height))
    box = (max(0, box[0]), max(0, box[1]), min(im.width, box[2]), min(im.height, box[3]))
    if box[2] <= box[0] or box[3] <= box[1]:
        return im
    return im.crop(box)


def main():
    if len(sys.argv) < 6:
        print(__doc__)
        return 2

    out, title, cols, tw = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4])
    specs = [split_spec(a) for a in sys.argv[5:]]
    files = [(p, c, cap) for p, c, cap in specs if os.path.isfile(p)]
    missing = [p for p, _, _ in specs if not os.path.isfile(p)]

    if not files:
        print("CONTACT SHEET NOT WRITTEN: none of the %d requested images exist."
              % len(specs))
        for f in missing:
            print("  missing: %s" % f)
        return 1

    ims = []
    for f, crop, cap in files:
        try:
            ims.append((f, apply_crop(Image.open(f).convert("RGB"), crop), cap))
        except Exception as e:                      # a corrupt png must be loud, not skipped
            print("  UNREADABLE: %s (%s)" % (f, e))

    if not ims:
        print("CONTACT SHEET NOT WRITTEN: every image failed to open.")
        return 1

    # Crops have different aspect ratios, so the row height is the tallest thumbnail in the sheet
    # rather than the first one's - otherwise a tall crop is silently clipped.
    th = max(int(tw * im.height / im.width) for _, im, _ in ims)
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
    for i, (path, im, cap) in enumerate(ims):
        cx = pad + (i % cols) * (tw + pad)
        cy = title_h + pad + (i // cols) * (th + cap_h + pad)
        ih = int(tw * im.height / im.width)
        sheet.paste(im.resize((tw, ih), Image.LANCZOS), (cx, cy))
        d.rectangle([cx, cy, cx + tw, cy + ih], outline=(70, 74, 84))

        if cap:
            caption = cap
        else:
            side = os.path.splitext(path)[0] + ".txt"
            if os.path.isfile(side):
                with open(side, encoding="utf-8") as fh:
                    caption = fh.read().strip().splitlines()[0]
            else:
                caption = os.path.splitext(os.path.basename(path))[0].replace("_", " ")
        d.text((cx + 3, cy + th + 5), fit(d, caption, f_cap, tw - 8),
               fill=(226, 230, 238), font=f_cap)

    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    sheet.save(out)
    print("sheet written: %s  (%dx%d, %d images)" % (out, w, h, len(ims)))
    for f in missing:
        print("  NOTE missing input: %s" % f)
    return 0


if __name__ == "__main__":
    sys.exit(main())
