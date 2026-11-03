"""
Turns a folder of captured frames into an animated GIF for the itch page.

The frames come from Tools/webgl_play_session.js driving the real WebGL build, so these GIFs show
the shipped game being played, not an editor reconstruction.

itch page GIFs want to be small enough to load on a phone, so this downscales, drops the palette to
a fixed adaptive one and skips frames to hit a target length rather than shipping a 70-frame
full-resolution animation nobody waits for.

usage:
  python Tools/make_gif.py FRAMEDIR OUT.gif [--width 640] [--fps 12] [--max-frames 48]
"""

import argparse
import glob
import os
import sys

try:
    from PIL import Image
except ImportError:
    print("PIL (Pillow) is required")
    sys.exit(2)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("frames")
    ap.add_argument("out")
    ap.add_argument("--width", type=int, default=640)
    ap.add_argument("--fps", type=int, default=12)
    ap.add_argument("--max-frames", type=int, default=48)
    args = ap.parse_args()

    files = sorted(glob.glob(os.path.join(args.frames, "*.png")))
    if not files:
        # A GIF that could not be made must never be reported as made.
        print("NO FRAMES in " + args.frames)
        return 1

    # Even stride, so the motion stays at a constant speed instead of stuttering.
    if len(files) > args.max_frames:
        step = len(files) / float(args.max_frames)
        files = [files[int(i * step)] for i in range(args.max_frames)]

    imgs = []
    for f in files:
        im = Image.open(f).convert("RGB")
        if im.width != args.width:
            im = im.resize((args.width, int(round(im.height * args.width / im.width))),
                           Image.LANCZOS)
        imgs.append(im.convert("P", palette=Image.ADAPTIVE, colors=128))

    os.makedirs(os.path.dirname(os.path.abspath(args.out)) or ".", exist_ok=True)
    imgs[0].save(args.out, save_all=True, append_images=imgs[1:],
                 duration=int(round(1000.0 / args.fps)), loop=0, optimize=True, disposal=2)

    kb = os.path.getsize(args.out) / 1024.0
    print("GIF %s  %d frames  %dpx  %.0f KB" % (args.out, len(imgs), args.width, kb))
    if kb > 3000:
        print("WARNING over 3 MB - itch will serve it slowly on a phone")
    return 0


if __name__ == "__main__":
    sys.exit(main())
