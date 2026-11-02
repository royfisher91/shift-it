"""Before/after sheet, for the visual review rule.

Roy's rule is that any visual change ships with a before/after sheet, because the change you
made is not always the change you see (KT_QUALITY_LESSONS lesson 10: Mole's re-shot store
screenshots barely changed, and Chippy gained two green posts that hid the chips).

This pairs images by FILENAME across two capture folders and lays each pair side by side. It
also reports, per pair, how different the two images actually are - so a "fix" that changed
nothing on screen is visible as a number rather than having to be eyeballed.

usage: python before_after.py BEFORE_DIR AFTER_DIR OUT.png "Title" [thumbW]
"""
import os
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFont


def font(size):
    for name in ("segoeuib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def difference(a, b):
    """Mean absolute pixel difference as a percentage. 0 means identical."""
    if a.size != b.size:
        b = b.resize(a.size)
    diff = ImageChops.difference(a.convert("RGB"), b.convert("RGB"))
    hist = diff.convert("L").histogram()
    total = sum(i * n for i, n in enumerate(hist))
    count = sum(hist)
    return 100.0 * (total / max(1, count)) / 255.0


def main():
    if len(sys.argv) < 5:
        print(__doc__)
        return 2
    bdir, adir, out, title = sys.argv[1:5]
    tw = int(sys.argv[5]) if len(sys.argv) > 5 else 600

    if not os.path.isdir(bdir):
        print("NO BEFORE/AFTER SHEET: the before folder does not exist: %s" % bdir)
        print("  For a first delivery there is no 'before'. Say that in the report rather than")
        print("  shipping an empty sheet.")
        return 1
    if not os.path.isdir(adir):
        print("NO BEFORE/AFTER SHEET: the after folder does not exist: %s" % adir)
        return 1

    names = sorted(
        n for n in os.listdir(adir)
        if n.lower().endswith(".png") and os.path.isfile(os.path.join(bdir, n))
    )
    only_after = sorted(
        n for n in os.listdir(adir)
        if n.lower().endswith(".png") and not os.path.isfile(os.path.join(bdir, n))
    )
    if not names:
        print("NO BEFORE/AFTER SHEET: no filename appears in both folders.")
        for n in only_after:
            print("  new in after (no pair): %s" % n)
        return 1

    pairs = []
    for n in names:
        b = Image.open(os.path.join(bdir, n)).convert("RGB")
        a = Image.open(os.path.join(adir, n)).convert("RGB")
        pairs.append((n, b, a, difference(b, a)))

    th = int(tw * pairs[0][1].height / pairs[0][1].width)
    pad, cap, title_h = 8, 30, 64
    w = pad + 2 * (tw + pad)
    h = title_h + len(pairs) * (th + cap + pad) + pad
    sheet = Image.new("RGB", (w, h), (22, 23, 27))
    d = ImageDraw.Draw(sheet)
    d.rectangle([0, 0, w, title_h], fill=(32, 34, 40))
    d.text((pad + 4, 16), title, fill=(255, 212, 90), font=font(28))

    f_cap, f_tag = font(19), font(22)
    for i, (n, b, a, pct) in enumerate(pairs):
        y = title_h + pad + i * (th + cap + pad)
        for j, (im, tag) in enumerate(((b, "BEFORE"), (a, "AFTER"))):
            x = pad + j * (tw + pad)
            sheet.paste(im.resize((tw, th), Image.LANCZOS), (x, y))
            d.rectangle([x, y, x + tw, y + th], outline=(70, 74, 84))
            d.rectangle([x, y, x + 104, y + 28], fill=(32, 34, 40))
            d.text((x + 8, y + 4), tag, fill=(140, 200, 255) if j == 0 else (150, 240, 160), font=f_tag)
        label = "%s    difference %.2f%%" % (n.replace(".png", "").replace("_", " "), pct)
        if pct < 0.05:
            label += "   <-- IDENTICAL: this 'change' did not change the screen"
        d.text((pad + 3, y + th + 5), label,
               fill=(255, 140, 140) if pct < 0.05 else (226, 230, 238), font=f_cap)

    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    sheet.save(out)
    print("before/after sheet: %s  (%d pair(s))" % (out, len(pairs)))
    for n, _, _, pct in pairs:
        flag = "  IDENTICAL" if pct < 0.05 else ""
        print("  %-36s %6.2f%%%s" % (n, pct, flag))
    for n in only_after:
        print("  no pair (new this round): %s" % n)
    return 0


if __name__ == "__main__":
    sys.exit(main())
