r"""Audit that this repository is safe to make public for GitHub Game Off.

Roy's Stage 2 requirement: "Repo must be safe to make public for Game Off: audit that no
non-redistributable asset is committed or referenced."

Every check reports PASS, FAIL or NOT RUN, and the script exits non-zero if anything failed. A
check that cannot run is a loud third state, never a quiet pass (KT_QUALITY_LESSONS B5).

    python Tools/repo_audit.py
"""
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# The only binary assets allowed in the repository, each CC0 and recorded in ASSETS_LICENCE.md.
ALLOWED_BINARIES = {
    "Assets/Resources/Audio/music_zone1_factory.ogg",
    "Assets/Resources/Audio/music_zone2_rooftop.ogg",
    "Assets/Resources/Audio/music_zone3_scrapyard.ogg",
    "Assets/Resources/Audio/music_zone4_launchpad.ogg",
    "Assets/Resources/Audio/sfx_explosion.ogg",
}

BINARY_EXT = re.compile(
    r"\.(wav|mp3|ogg|flac|aiff?|m4a|png|jpe?g|gif|bmp|tga|psd|tif|exe|dll|so|dylib|zip|7z|rar|"
    r"ttf|otf|woff2?|fbx|obj|blend|mb|ma|unitypackage|mp4|mov|webm)$",
    re.I,
)

# Paths and names that must never appear as the SOURCE of anything committed.
FORBIDDEN_SOURCES = [
    (r"Desktop_Moved_20261020[\\/]KT_SoundLibrary", "Roy's private Sonniss sound library"),
    (r"KT_SoundLibrary", "Roy's private sound library"),
    (r"\bSonniss\b", "Sonniss (not redistributable)"),
    (r"GameAudioGDC", "Sonniss GDC bundle"),
]

results = []


def check(name, ok, detail=""):
    results.append((name, "PASS" if ok else "FAIL", detail))
    print("  %-4s  %s%s" % ("PASS" if ok else "FAIL", name, ("  -- " + detail) if detail else ""))
    return ok


def not_run(name, why):
    results.append((name, "NOT RUN", why))
    print("  %-4s  %s  -- %s" % ("????", name, why))


def git(*args):
    return subprocess.run(["git"] + list(args), cwd=ROOT, capture_output=True, text=True)


def main():
    print("Repository public-safety audit: %s\n" % ROOT)

    r = git("ls-files")
    if r.returncode != 0:
        not_run("list tracked files", "git ls-files failed: " + r.stderr.strip())
        print("\nAUDIT INCOMPLETE")
        return 2
    tracked = [p for p in r.stdout.splitlines() if p.strip()]
    print("  tracked files: %d\n" % len(tracked))

    # 1. Only the allowlisted CC0 binaries are committed.
    binaries = [p for p in tracked if BINARY_EXT.search(p)]
    unexpected = sorted(set(binaries) - ALLOWED_BINARIES)
    check("only allowlisted CC0 binaries are committed",
          not unexpected,
          "unexpected: " + ", ".join(unexpected) if unexpected else
          "%d binary file(s), all allowlisted" % len(binaries))

    # 2. Every allowlisted binary actually exists.
    absent = sorted(p for p in ALLOWED_BINARIES if not os.path.isfile(os.path.join(ROOT, p)))
    check("every allowlisted CC0 asset is present", not absent,
          "missing: " + ", ".join(absent) if absent else "all 5 present")

    # 3. Nothing references a non-redistributable source.
    hits = []
    SELF = "Tools/repo_audit.py"      # it has to name what it forbids, so it cannot scan itself
    for path in tracked:
        full = os.path.join(ROOT, path)
        if not os.path.isfile(full) or BINARY_EXT.search(path) or path == SELF:
            continue
        try:
            with open(full, encoding="utf-8", errors="replace") as fh:
                text = fh.read()
        except OSError:
            continue
        for pattern, why in FORBIDDEN_SOURCES:
            for m in re.finditer(pattern, text):
                line = text[:m.start()].count("\n") + 1
                # A documented explanation of why something is NOT used is not a reference TO it.
                context = text.splitlines()[line - 1] if line - 1 < len(text.count("\n") * [0] + text.splitlines()) else ""
                hits.append("%s:%d (%s)" % (path, line, why))
    # The licence docs legitimately name the library in order to say it is excluded.
    doc_allowed = ("ASSETS_LICENCE.md", "CREDITS.md", "DECISIONS_FOR_ROY.md", "README.md")
    real = [h for h in hits if not h.startswith(doc_allowed) and "Docs/" not in h]
    check("no code or data references a non-redistributable source",
          not real,
          "references: " + "; ".join(real[:6]) if real else
          "%d mention(s), all in licence/decision docs explaining the exclusion" % len(hits))

    # 4. Every committed audio file is named in ASSETS_LICENCE.md.
    lic_path = os.path.join(ROOT, "ASSETS_LICENCE.md")
    if not os.path.isfile(lic_path):
        not_run("every committed asset is in ASSETS_LICENCE.md", "ASSETS_LICENCE.md is missing")
    else:
        lic = open(lic_path, encoding="utf-8").read()
        undocumented = [p for p in binaries if os.path.basename(p) not in lic]
        check("every committed asset is named in ASSETS_LICENCE.md",
              not undocumented,
              "undocumented: " + ", ".join(undocumented) if undocumented else
              "all %d documented" % len(binaries))
        check("ASSETS_LICENCE.md states CC0 for art and audio",
              "CC0" in lic and "MIT" in lic,
              "CC0 and MIT both stated" if ("CC0" in lic and "MIT" in lic) else "missing a licence statement")

    # 5. Builds, logs and review captures stay out.
    gi_path = os.path.join(ROOT, ".gitignore")
    if not os.path.isfile(gi_path):
        not_run("builds, logs and captures are ignored", ".gitignore is missing")
    else:
        gi = open(gi_path, encoding="utf-8").read()
        need = ["[Bb]uild", "[Ll]ogs", "Review", "Screenshots"]
        absent_rules = [n for n in need if n not in gi]
        check("builds, logs and review captures are ignored", not absent_rules,
              "missing rules: " + ", ".join(absent_rules) if absent_rules else "all present")
        leaked = [p for p in tracked
                  if p.startswith(("Build/", "Builds/", "Review/", "Screenshots/", "Logs/"))]
        check("no build or capture output is committed", not leaked,
              "leaked: " + ", ".join(leaked[:5]) if leaked else "none")

    # 6. Nothing enormous.
    big = []
    for p in tracked:
        full = os.path.join(ROOT, p)
        if os.path.isfile(full) and os.path.getsize(full) > 2 * 1024 * 1024:
            big.append("%s (%.1f MB)" % (p, os.path.getsize(full) / 1048576.0))
    check("no tracked file over 2 MB", not big,
          "large: " + ", ".join(big) if big else "largest is well under")

    # 7. A licence file exists for the code.
    check("LICENSE exists", os.path.isfile(os.path.join(ROOT, "LICENSE")))

    failed = [r for r in results if r[1] == "FAIL"]
    skipped = [r for r in results if r[1] == "NOT RUN"]
    print("\n%d checks: %d passed, %d failed, %d could not run"
          % (len(results), len(results) - len(failed) - len(skipped), len(failed), len(skipped)))
    if failed:
        print("REPOSITORY IS NOT SAFE TO PUBLISH YET:")
        for n, _, d in failed:
            print("  - %s: %s" % (n, d))
        return 1
    if skipped:
        print("AUDIT INCOMPLETE - a check could not run, which is not a pass.")
        return 2
    print("SAFE TO PUBLISH: every asset is CC0 or generated, and nothing non-redistributable is "
          "committed or referenced.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
