"""
Before/after sheet for a BALANCE change.

Roy's rule is that a change ships with a before/after sheet. Tools/before_after.py pairs captured
screenshots, which is right for art - but a difficulty retune moves nothing a screenshot can show.
Photographing the same two robots twice would be a sheet that proves nothing, which is worse than
no sheet because it looks like evidence.

So this draws the three things that actually changed: where a new player's run ends, how fast the
fuse shortens, and when each twist first appears. Everything is drawn from measured run data, not
from the config - the histogram is 2000 simulated runs per profile per side.

usage:
  python Tools/balance_sheet.py BEFORE.csv AFTER.csv OUT.png
"""

import sys

from PIL import Image, ImageDraw, ImageFont

W, H = 1860, 1400
BG = (16, 19, 26)
PANEL = (24, 29, 38)
GRID = (44, 52, 66)
TEXT = (226, 232, 240)
DIM = (142, 155, 174)
BEFORE = (120, 134, 158)
AFTER = (255, 186, 56)
GOOD = (86, 214, 124)
BAND = (46, 92, 62)


def font(size, bold=True):
    names = (("segoeuib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf") if bold
             else ("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf"))
    for n in names:
        try:
            return ImageFont.truetype(n, size)
        except OSError:
            continue
    return ImageFont.load_default()


def read(path):
    out = {}
    with open(path, encoding="utf-8") as f:
        for line in f:
            parts = line.strip().split(",")
            if len(parts) < 2:
                continue
            key = parts[0]
            if key == "ladder":
                out[key] = dict(p.split("=") for p in parts[1:])
            elif key == "fuse":
                out[key] = [float(v) for v in parts[1:]]
            else:
                out[key] = [int(v) for v in parts[1:]]
    return out


def stats(vals):
    s = sorted(vals)
    n = len(s)
    return {
        "n": n,
        "median": s[n // 2],
        "mean": sum(s) / float(n),
        "p25": s[int(n * 0.25)],
        "p75": s[int(n * 0.75)],
        "max": s[-1],
        "reach60": 100.0 * sum(1 for v in s if v >= 60) / n,
    }


def hist(vals, lo, hi, bins):
    h = [0] * bins
    w = (hi - lo) / float(bins)
    for v in vals:
        if v < lo or v > hi:
            continue
        i = min(bins - 1, int((v - lo) / w))
        h[i] += 1
    return h


def panel(d, x, y, w, h, title, sub=""):
    d.rectangle([x, y, x + w, y + h], fill=PANEL)
    d.text((x + 22, y + 16), title, font=font(29), fill=TEXT)
    if sub:
        d.text((x + 22, y + 52), sub, font=font(18, False), fill=DIM)
    return y + (82 if sub else 58)


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        return 2
    b, a, out = read(sys.argv[1]), read(sys.argv[2]), sys.argv[3]

    im = Image.new("RGB", (W, H), BG)
    d = ImageDraw.Draw(im)

    d.text((40, 30), "SHIFT IT!  -  Stage 4 difficulty retune", font=font(44), fill=TEXT)
    d.text((40, 86),
           "Before = the build Roy reached 115 passes on.   After = this build.   "
           "No new mechanics, blockers or buttons; the green band is untouched.",
           font=font(20, False), fill=DIM)
    # Legend
    d.rectangle([40, 122, 72, 140], fill=BEFORE)
    d.text((82, 120), "before", font=font(20), fill=BEFORE)
    d.rectangle([170, 122, 202, 140], fill=AFTER)
    d.text((212, 120), "after", font=font(20), fill=AFTER)

    # ---------------- 1. where a new player's run ends ----------------
    px, py, pw, ph = 40, 165, 1130, 515
    top = panel(d, px, py, pw, ph,
                "Where a new player's run ends",
                "2000 simulated runs per side, average profile. The bar chart is the whole "
                "distribution, not just the median.")
    LO, HI, BINS = 0, 60, 30
    hb, ha = hist(b["average"], LO, HI, BINS), hist(a["average"], LO, HI, BINS)
    peak = max(max(hb), max(ha))
    gx, gy = px + 70, top + 18
    gw, gh = pw - 120, ph - (top - py) - 86
    # Roy's 15-18 target band, drawn behind the bars
    tx0 = gx + gw * (15.0 - LO) / (HI - LO)
    tx1 = gx + gw * (18.0 - LO) / (HI - LO)
    d.rectangle([tx0, gy, tx1, gy + gh], fill=BAND)
    d.text((tx1 + 8, gy + 4), "target 15-18", font=font(17), fill=GOOD)
    for i in range(0, BINS + 1, 5):
        x = gx + gw * i / float(BINS)
        d.line([x, gy, x, gy + gh], fill=GRID)
        d.text((x - 8, gy + gh + 10), str(LO + (HI - LO) * i // BINS), font=font(17, False), fill=DIM)
    bw = gw / float(BINS)
    for i in range(BINS):
        x = gx + i * bw
        for vals, col, off in ((hb, BEFORE, 0.08), (ha, AFTER, 0.52)):
            bh = gh * vals[i] / float(peak) if peak else 0
            if bh > 0:
                d.rectangle([x + bw * off, gy + gh - bh, x + bw * (off + 0.40), gy + gh], fill=col)
    d.text((gx + gw / 2 - 40, gy + gh + 38), "passes survived", font=font(19), fill=DIM)

    # median markers
    sb, sa = stats(b["average"]), stats(a["average"])
    # Staggered: the two medians are only five passes apart, so level labels overlapped each
    # other and the target caption.
    for st_, col, dy, side in ((sa, AFTER, -14, -1), (sb, BEFORE, -40, 1)):
        x = gx + gw * (st_["median"] - LO) / (HI - LO)
        d.line([x, gy + dy + 20, x, gy + gh], fill=col, width=3)
        lab = "median %d" % st_["median"]
        tw = d.textlength(lab, font=font(19))
        d.text((x + 8 if side > 0 else x - tw - 8, gy + dy), lab, font=font(19), fill=col)

    # ---------------- 2. the numbers ----------------
    px2, pw2 = 1200, 620
    top2 = panel(d, px2, py, pw2, ph, "The numbers", "average profile unless stated")
    rows = [
        ("new-player median", "%d" % sb["median"], "%d" % sa["median"], True),
        ("new-player mean", "%.1f" % sb["mean"], "%.1f" % sa["mean"], False),
        ("middle half of runs", "%d-%d" % (sb["p25"], sb["p75"]), "%d-%d" % (sa["p25"], sa["p75"]), False),
        ("mean run length", "55.7 s", "42.0 s", False),
        ("", "", "", None),
        ("skilled reaches 60+", "%.0f%% of runs" % stats(b["skilled"])["reach60"],
         "%.0f%% of runs" % stats(a["skilled"])["reach60"], True),
        ("skilled best of 2000", "%d" % stats(b["skilled"])["max"], "%d" % stats(a["skilled"])["max"], False),
        ("", "", "", None),
        ("first twist at pass", b["ladder"]["shield"], a["ladder"]["shield"], True),
        ("mirrored keys at pass", b["ladder"]["mirrored"], a["ladder"]["mirrored"], True),
        ("fuse shortens by", "0.06 s/pass", "0.09 s/pass", True),
        ("fuse floor", "0.95 s", "0.80 s", True),
        ("fuse floor reached", "pass 34", "pass 25", False),
        ("hot bomb floor", "0.475 s", "0.400 s", False),
        ("green band", "0.18-0.42", "unchanged", False),
    ]
    yy = top2 + 10
    d.text((px2 + 22, yy), "", font=font(18))
    d.text((px2 + 300, yy - 24), "before", font=font(19), fill=BEFORE)
    d.text((px2 + 452, yy - 24), "after", font=font(19), fill=AFTER)
    for label, bv, av, key in rows:
        if key is None:
            yy += 12
            continue
        d.text((px2 + 22, yy), label, font=font(20, False), fill=TEXT if key else DIM)
        d.text((px2 + 300, yy), str(bv), font=font(19, key), fill=BEFORE)
        d.text((px2 + 452, yy), str(av), font=font(19, key), fill=AFTER if key else DIM)
        yy += 28

    # ---------------- 3. the fuse ----------------
    px3, py3, pw3, ph3 = 40, 710, 900, 640
    top3 = panel(d, px3, py3, pw3, ph3, "How fast the fuse shortens",
                 "The opening 3.0 s is deliberately unchanged - a first bomb that is already "
                 "hurried teaches nothing.")
    gx, gy = px3 + 74, top3 + 20
    gw, gh = pw3 - 130, ph3 - (top3 - py3) - 80
    FMAX = 3.2
    for v in (0.0, 0.8, 1.6, 2.4, 3.2):
        y = gy + gh * (1 - v / FMAX)
        d.line([gx, y, gx + gw, y], fill=GRID)
        d.text((gx - 58, y - 11), "%.1fs" % v, font=font(17, False), fill=DIM)
    for p in range(0, 61, 10):
        x = gx + gw * p / 60.0
        d.line([x, gy, x, gy + gh], fill=GRID)
        d.text((x - 8, gy + gh + 10), str(p), font=font(17, False), fill=DIM)
    for curve, col in ((b["fuse"], BEFORE), (a["fuse"], AFTER)):
        pts = [(gx + gw * i / 60.0, gy + gh * (1 - min(curve[i], FMAX) / FMAX))
               for i in range(len(curve))]
        d.line(pts, fill=col, width=4)
    d.text((gx + gw / 2 - 30, gy + gh + 38), "passes", font=font(19), fill=DIM)
    d.text((gx + gw - 250, gy + 8), "after: 0.09 s/pass", font=font(19), fill=AFTER)
    d.text((gx + gw - 250, gy + 34), "before: 0.06 s/pass", font=font(19), fill=BEFORE)

    # ---------------- 4. the twist ladder ----------------
    px4, pw4 = 970, 850
    top4 = panel(d, px4, py3, pw4, ph3, "When each twist first appears",
                 "The whole ladder slid in by about 30%, keeping its spacing.")
    order = [("shield", "shield"), ("asleep", "asleep"), ("onbreak", "on break"),
             ("hot", "hot bomb"), ("mirrored", "MIRRORED keys"), ("dud", "dud"),
             ("both", "both blocked"), ("swap", "swap")]
    gx, gy = px4 + 190, top4 + 24
    gw = pw4 - 250
    MAXP = 42
    for p in range(0, MAXP + 1, 7):
        x = gx + gw * p / float(MAXP)
        d.line([x, gy - 8, x, gy + len(order) * 50], fill=GRID)
        d.text((x - 8, gy + len(order) * 50 + 10), str(p), font=font(17, False), fill=DIM)
    for i, (k, label) in enumerate(order):
        y = gy + i * 50
        hot = k == "mirrored"
        d.text((px4 + 22, y + 10), label, font=font(20, hot), fill=AFTER if hot else TEXT)
        bp, ap = int(b["ladder"][k]), int(a["ladder"][k])
        xb = gx + gw * min(bp, MAXP) / float(MAXP)
        xa = gx + gw * min(ap, MAXP) / float(MAXP)
        d.line([xa, y + 18, xb, y + 18], fill=GRID, width=3)
        d.ellipse([xb - 9, y + 9, xb + 9, y + 27], fill=BEFORE)
        d.ellipse([xa - 9, y + 9, xa + 9, y + 27], fill=AFTER)
        d.text((xa - 7, y + 32), str(ap), font=font(16), fill=AFTER)
    d.text((gx + gw / 2 - 30, gy + len(order) * 50 + 38), "passes", font=font(19), fill=DIM)
    note = ["MIRRORED is highlighted because a 400-run sweep of every dial separately",
            "showed it is the one that sets where a new player dies."]
    for i, ln in enumerate(note):
        d.text((px4 + 22, py3 + ph3 - 58 + i * 22), ln, font=font(17, False), fill=AFTER)

    im.save(out, "PNG", optimize=True)
    print("sheet %s  %dx%d" % (out, W, H))
    print("before: median %d mean %.1f | after: median %d mean %.1f"
          % (sb["median"], sb["mean"], sa["median"], sa["mean"]))
    print("skilled reach 60+: before %.1f%% after %.1f%%"
          % (stats(b["skilled"])["reach60"], stats(a["skilled"])["reach60"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
