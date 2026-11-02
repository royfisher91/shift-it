r"""Confirm the WebGL build works in a real browser, including LEFT vs RIGHT Shift.

Roy's brief, verbatim: "Detect them separately (Unity Input System leftShiftKey / rightShiftKey)
and confirm it works in WebGL in Chrome and Edge."

An editor test cannot answer that. The Input System's keyboard mapping in a browser comes from
KeyboardEvent.code, which is a different code path from the editor's, and Unity's WebGL player
also has to ship the built-in font - if it were stripped, every piece of text would render blank
while all the editor tests still passed. So this drives the ACTUAL build in an ACTUAL browser.

    python -I Tools/webgl_browser_check.py [--browser edge] [--out <dir>]

How it drives the game: through the OS, with keybd_event and the real VK_LSHIFT / VK_RSHIFT
virtual keys, exactly as a player's keyboard does. The focus/tap/guard pattern is lifted from
Spider's Docs/store_shots.py rather than reinvented (quality lesson 15), including the part that
matters most: guard() REFUSES to send a keystroke unless the browser really is the foreground
window, so this can never type Shift into something else.

It captures a labelled sequence of screenshots and prints what it did. The screenshots are the
evidence - a human (or Claude) still has to look at them, which is the point.
"""
import argparse
import ctypes
import ctypes.wintypes
import functools
import http.server
import os
import socketserver
import subprocess
import sys
import threading
import time

from PIL import Image, ImageGrab

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

VK_LSHIFT = 0xA0
VK_RSHIFT = 0xA1
VK_TAB = 0x09

proc = None
shots = []


class Abort(Exception):
    pass


# ---------------------------------------------------------------- local server
class _Quiet(http.server.SimpleHTTPRequestHandler):
    """SimpleHTTPRequestHandler without the per-request logging noise."""

    def log_message(self, *a):
        pass


def serve(root, port=0):
    # functools.partial returns an object, not a class, so the handler cannot be subclassed from
    # it - the class is declared above and the directory is bound here instead.
    handler = functools.partial(_Quiet, directory=root)
    socketserver.TCPServer.allow_reuse_address = True
    httpd = socketserver.TCPServer(("127.0.0.1", port), handler)
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    return httpd, httpd.server_address[1]


# ---------------------------------------------------------------- window / input
def fg_pid():
    p = ctypes.wintypes.DWORD()
    user32.GetWindowThreadProcessId(user32.GetForegroundWindow(), ctypes.byref(p))
    return p.value


def windows_of(pid):
    found = []

    @ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.wintypes.HWND, ctypes.wintypes.LPARAM)
    def cb(h, l):
        p = ctypes.wintypes.DWORD()
        user32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(h):
            found.append(h)
        return True

    user32.EnumWindows(cb, 0)
    return found


def focus(pid):
    """Bring the browser forward. Edge spawns helper processes, so try the whole tree."""
    for _ in range(40):
        time.sleep(0.5)
        hs = windows_of(pid)
        if not hs:
            # Edge's visible window often belongs to a child process.
            for child in child_pids(pid):
                hs = windows_of(child)
                if hs:
                    break
        if hs:
            user32.keybd_event(0x12, 0, 0, 0)
            user32.keybd_event(0x12, 0, 2, 0)      # an Alt tap lets SetForegroundWindow through
            user32.ShowWindow(hs[0], 3)            # maximise
            user32.SetForegroundWindow(hs[0])
            time.sleep(1.0)
            return True
    return False


def child_pids(pid):
    try:
        out = subprocess.run(
            ["powershell", "-NoProfile", "-Command",
             "Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq %d } | "
             "Select-Object -ExpandProperty ProcessId" % pid],
            capture_output=True, text=True, timeout=30).stdout
        return [int(x) for x in out.split() if x.strip().isdigit()]
    except Exception:
        return []


def is_browser_foreground(pids):
    return fg_pid() in pids


def guard(pids):
    """Never send a keystroke unless the browser is genuinely in front."""
    if not is_browser_foreground(pids):
        raise Abort("the browser is not the foreground window: refusing to send Shift elsewhere")


def tap(pids, vk, hold=0.09, after=0.35):
    guard(pids)
    scan = user32.MapVirtualKeyW(vk, 0)
    user32.keybd_event(vk, scan, 0, 0)
    time.sleep(hold)
    user32.keybd_event(vk, scan, 2, 0)
    time.sleep(after)


def shot(out, name, caption):
    path = os.path.join(out, name + ".png")
    im = ImageGrab.grab()
    im.save(path)
    with open(os.path.splitext(path)[0] + ".txt", "w", encoding="utf-8") as fh:
        fh.write(caption)
    shots.append((name, path))
    print("    shot %-28s %s" % (name, path), flush=True)
    return path


def contrast(path):
    """Luminance standard deviation, as a blunt 'is anything even on screen' check."""
    im = Image.open(path).convert("L").resize((480, 270))
    px = list(im.getdata())
    n = len(px)
    mean = sum(px) / n
    var = sum((p - mean) ** 2 for p in px) / n
    return (var ** 0.5) / 255.0


# ---------------------------------------------------------------- main
def main():
    global proc
    ap = argparse.ArgumentParser()
    ap.add_argument("--build", default=r"D:\KT\Builds\ShiftIt\WebGL")
    ap.add_argument("--out", default=r"D:\KT\ShiftIt\Review\webgl")
    ap.add_argument("--browser", default="edge", choices=["edge", "chrome"])
    ap.add_argument("--load-seconds", type=float, default=22.0)
    a = ap.parse_args()

    exe = {
        "edge": r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        "chrome": r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    }[a.browser]
    if not os.path.isfile(exe):
        print("BROWSER NOT INSTALLED: %s" % exe)
        print("  This is NOT a pass and NOT a failure - the check did not run.")
        return 4
    if not os.path.isfile(os.path.join(a.build, "index.html")):
        print("NO WEBGL BUILD at %s" % a.build)
        return 2

    os.makedirs(a.out, exist_ok=True)
    httpd, port = serve(a.build)
    url = "http://127.0.0.1:%d/index.html" % port
    print("serving %s at %s" % (a.build, url), flush=True)

    profile = os.path.join(a.out, "_profile_" + a.browser)
    proc = subprocess.Popen([
        exe, url,
        "--user-data-dir=" + profile,     # a throwaway profile: never touches Roy's browser
        "--no-first-run", "--no-default-browser-check",
        "--new-window", "--start-maximized",
        "--disable-features=Translate,PrivacySandboxSettings4",
    ])
    pids = [proc.pid]
    try:
        if not focus(proc.pid):
            raise Abort("could not bring the browser to the foreground")
        pids = [proc.pid] + child_pids(proc.pid)
        print("  browser pid %s (+%d children), foreground=%s"
              % (proc.pid, len(pids) - 1, is_browser_foreground(pids)), flush=True)

        print("  waiting %.0fs for the Unity WebGL player to load..." % a.load_seconds, flush=True)
        time.sleep(a.load_seconds)

        p = shot(a.out, "01_title",
                 "TITLE in %s - if the logo, tagline and PRESS SHIFT TO START are readable, the "
                 "built-in font survived the WebGL build." % a.browser)
        c = contrast(p)
        print("    frame contrast %.4f  %s" % (c, "OK" if c > 0.02 else "*** LOOKS BLANK ***"), flush=True)

        # Does Shift start the game at all?
        tap(pids, VK_LSHIFT)
        time.sleep(0.6)
        shot(a.out, "02_after_left_shift_start",
             "After one LEFT SHIFT - the title should be gone and a bomb in the player's hands.")

        # LEFT vs RIGHT must go to different robots. Two catches, two different keys.
        time.sleep(1.2)
        tap(pids, VK_LSHIFT)
        time.sleep(0.25)
        shot(a.out, "03_threw_LEFT",
             "LEFT SHIFT thrown: the bomb should be heading to the LEFT robot.")

        time.sleep(1.8)
        tap(pids, VK_RSHIFT)
        time.sleep(0.25)
        shot(a.out, "04_threw_RIGHT",
             "RIGHT SHIFT thrown: the bomb should be heading to the RIGHT robot. If 03 and 04 "
             "differ, the two Shift keys are being told apart in the browser.")

        # And prove a held key is one throw, not a stream (Sticky Keys safety) in the browser too.
        time.sleep(1.8)
        guard(pids)
        scan = user32.MapVirtualKeyW(VK_LSHIFT, 0)
        user32.keybd_event(VK_LSHIFT, scan, 0, 0)
        time.sleep(1.6)                     # hold it down
        user32.keybd_event(VK_LSHIFT, scan, 2, 0)
        time.sleep(0.3)
        shot(a.out, "05_held_left_shift",
             "LEFT SHIFT held down for 1.6 s: the score should have gone up by ONE throw, not a "
             "stream of them.")

        time.sleep(2.0)
        shot(a.out, "06_later",
             "A few seconds later - the run should still be going, or showing the BOOM! card.")

        print("\nSCREENSHOTS WRITTEN: %d  ->  %s" % (len(shots), a.out))
        print("Look at them. 03 vs 04 is the left/right Shift evidence; 01 is the font evidence.")
        return 0
    except Abort as e:
        print("ABORTED: %s" % e)
        print("  This is NOT a pass and NOT a failure - the check did not complete.")
        return 3
    finally:
        try:
            proc.terminate()
            proc.wait(timeout=15)
        except Exception:
            try:
                proc.kill()
            except Exception:
                pass
        httpd.shutdown()


if __name__ == "__main__":
    sys.exit(main())
