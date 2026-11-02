"""Build a GIF from a folder of numbered frames captured by CaptureTests.

usage: python make_gif.py <frame_dir> <out.gif> [fps] [width]

Refuses to write a GIF from fewer than 10 frames rather than producing a one-frame
"animation" that looks like a successful capture.
"""
import glob
import os
import sys

from PIL import Image


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        return 2
    src, out = sys.argv[1], sys.argv[2]
    fps = float(sys.argv[3]) if len(sys.argv) > 3 else 20.0
    width = int(sys.argv[4]) if len(sys.argv) > 4 else 480

    files = sorted(glob.glob(os.path.join(src, "f*.png")))
    if len(files) < 10:
        print("GIF NOT WRITTEN: only %d frame(s) found in %s" % (len(files), src))
        return 1

    frames = []
    for f in files:
        im = Image.open(f).convert("RGB")
        if im.width != width:
            im = im.resize((width, int(width * im.height / im.width)), Image.LANCZOS)
        # Adaptive palette per frame keeps the flat cartoon colours clean in 256 colours.
        frames.append(im.quantize(colors=200, method=Image.MEDIANCUT))

    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    frames[0].save(
        out,
        save_all=True,
        append_images=frames[1:],
        duration=int(1000.0 / fps),
        loop=0,
        optimize=True,
        disposal=2,
    )
    mb = os.path.getsize(out) / (1024.0 * 1024.0)
    print("gif written: %s  (%d frames, %dpx wide, %.2f MB)" % (out, len(frames), width, mb))
    if mb > 3.0:
        print("  NOTE over 3 MB - itch.io prefers smaller; drop the width or the frame count.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
