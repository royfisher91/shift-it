"""
Packs the WebGL build into the zip itch.io expects.

itch requires index.html at the ROOT of the archive, not inside a folder. Zipping the build
directory itself produces WebGL/index.html, which itch accepts as an upload and then serves as a
blank page - a failure that looks like a game bug and costs an upload cycle to find. So this walks
the build and writes paths relative to it, then reads the finished archive back and refuses to
report success unless index.html really is at the root.

It also refuses to pack a build older than the newest gameplay source file. The whole point of a
rebuild is that the zip carries the change; a zip quietly made from yesterday's build is the
"configured but not switched on" failure wearing a different hat.

usage:
  python Tools/make_itch_zip.py
  python Tools/make_itch_zip.py --build D:/KT/Builds/ShiftIt/WebGL --out D:/KT/ShiftIt/itch/shiftit-webgl.zip
"""

import argparse
import os
import sys
import time
import zipfile

SRC_DIRS = [
    "Assets/Scripts/Sim",
    "Assets/Scripts/Game",
    "Assets/Resources",
    "Assets/Plugins/WebGL",
]


def newest_source(repo):
    """The most recently touched gameplay source file, so staleness can be proved rather than hoped."""
    newest, where = 0.0, None
    for d in SRC_DIRS:
        root = os.path.join(repo, d)
        for dirpath, _dirs, files in os.walk(root):
            for f in files:
                if f.endswith(".meta"):
                    continue
                p = os.path.join(dirpath, f)
                try:
                    m = os.path.getmtime(p)
                except OSError:
                    continue
                if m > newest:
                    newest, where = m, p
    return newest, where


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--build", default="D:/KT/Builds/ShiftIt/WebGL")
    ap.add_argument("--out", default="D:/KT/ShiftIt/itch/shiftit-webgl.zip")
    ap.add_argument("--repo", default="C:/c/Projects/KT_ShiftIt")
    ap.add_argument("--allow-stale", action="store_true",
                    help="pack even if the build predates the newest source file")
    args = ap.parse_args()

    index = os.path.join(args.build, "index.html")
    if not os.path.isfile(index):
        print("NO BUILD: " + index + " does not exist")
        return 2

    # ---- staleness gate ----
    src_time, src_file = newest_source(args.repo)
    # The loader is written last in a Unity WebGL build, so it dates the build as a whole.
    build_time = os.path.getmtime(os.path.join(args.build, "Build", "WebGL.loader.js"))
    fmt = "%Y-%m-%d %H:%M:%S"
    print("newest source  " + time.strftime(fmt, time.localtime(src_time)) +
          "  " + os.path.relpath(src_file, args.repo).replace("\\", "/"))
    print("build          " + time.strftime(fmt, time.localtime(build_time)))
    if build_time < src_time:
        print("")
        print("STALE BUILD: the build is older than the newest gameplay source by %.0f s."
              % (src_time - build_time))
        print("Rebuild first, or pass --allow-stale if you really mean to ship this one.")
        if not args.allow_stale:
            return 3
        print("--allow-stale given; packing anyway.")

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    n = 0
    with zipfile.ZipFile(args.out, "w", zipfile.ZIP_DEFLATED) as z:
        for dirpath, _dirs, files in os.walk(args.build):
            for f in sorted(files):
                full = os.path.join(dirpath, f)
                # Relative to the build root, so index.html lands at the archive root.
                rel = os.path.relpath(full, args.build).replace("\\", "/")
                z.write(full, rel)
                n += 1

    # ---- read the finished archive back; do not trust the writing of it ----
    with zipfile.ZipFile(args.out) as z:
        names = z.namelist()
        bad = z.testzip()
    if bad is not None:
        print("CORRUPT ENTRY in the finished zip: " + bad)
        return 4
    if "index.html" not in names:
        print("index.html IS NOT AT THE ROOT of the zip - itch would serve a blank page.")
        print("root entries: " + ", ".join(sorted(set(x.split("/")[0] for x in names))))
        return 5

    mb = os.path.getsize(args.out) / (1024.0 * 1024.0)
    print("")
    print("zip  %s" % args.out)
    print("RESULT files=%d size=%.1fMB index.html at root=yes" % (n, mb))
    if mb > 1000:
        print("WARNING over itch's 1 GB per-file limit")
    return 0


if __name__ == "__main__":
    sys.exit(main())
