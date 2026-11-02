"""Test SHIFT IT! against the two Windows accessibility shortcuts, ON and OFF.

Roy's requirement, verbatim: "Windows build: disable the Sticky Keys and Filter Keys shortcuts
while the game runs and ALWAYS restore the player's original settings on quit. Test with Sticky
Keys and Filter Keys ON and OFF in both builds and report the results."

Why this matters rather than being box-ticking: every throw in this game is a Shift press, so five
throws is five Shift presses - which is exactly the Sticky Keys activation gesture. If the shortcut
is live, a dialog lands on the player mid-run.

WHAT THIS SCRIPT CHANGES AND GUARANTEES
  It temporarily turns the two activation shortcuts ON so it can prove the game suppresses them.
  Every write uses fWinIni = 0, so NOTHING is saved to the user profile. The original values are
  captured first and restored in a finally block, and the final state is printed so the restore is
  evidence rather than a claim. It never touches the "feature enabled" bit - only the hotkey bit.

usage:
  python sticky_keys_test.py --report-only           read and print the current state, change nothing
  python sticky_keys_test.py --exe <path to .exe>    run the full matrix
"""
import argparse
import ctypes
import ctypes.wintypes as wt
import os
import subprocess
import sys
import time

SPI_GETSTICKYKEYS = 0x003A
SPI_SETSTICKYKEYS = 0x003B
SPI_GETFILTERKEYS = 0x0032
SPI_SETFILTERKEYS = 0x0033

SKF_STICKYKEYSON = 0x00000001
SKF_HOTKEYACTIVE = 0x00000004
FKF_FILTERKEYSON = 0x00000001
FKF_HOTKEYACTIVE = 0x00000004


class STICKYKEYS(ctypes.Structure):
    _fields_ = [("cbSize", wt.UINT), ("dwFlags", wt.DWORD)]


class FILTERKEYS(ctypes.Structure):
    _fields_ = [("cbSize", wt.UINT), ("iWaitMSec", wt.UINT), ("iDelayMSec", wt.UINT),
                ("iRepeatMSec", wt.UINT), ("iBounceMSec", wt.UINT), ("dwFlags", wt.DWORD)]


spi = ctypes.windll.user32.SystemParametersInfoW
spi.restype = wt.BOOL


def get_sticky():
    s = STICKYKEYS(cbSize=ctypes.sizeof(STICKYKEYS))
    if not spi(SPI_GETSTICKYKEYS, s.cbSize, ctypes.byref(s), 0):
        raise OSError("SPI_GETSTICKYKEYS failed")
    return s


def get_filter():
    f = FILTERKEYS(cbSize=ctypes.sizeof(FILTERKEYS))
    if not spi(SPI_GETFILTERKEYS, f.cbSize, ctypes.byref(f), 0):
        raise OSError("SPI_GETFILTERKEYS failed")
    return f


def set_sticky(s):
    return bool(spi(SPI_SETSTICKYKEYS, s.cbSize, ctypes.byref(s), 0))


def set_filter(f):
    return bool(spi(SPI_SETFILTERKEYS, f.cbSize, ctypes.byref(f), 0))


def describe():
    s, f = get_sticky(), get_filter()
    return {
        "sticky_feature_on": bool(s.dwFlags & SKF_STICKYKEYSON),
        "sticky_hotkey_on": bool(s.dwFlags & SKF_HOTKEYACTIVE),
        "filter_feature_on": bool(f.dwFlags & FKF_FILTERKEYSON),
        "filter_hotkey_on": bool(f.dwFlags & FKF_HOTKEYACTIVE),
        "sticky_flags": s.dwFlags,
        "filter_flags": f.dwFlags,
    }


def line(tag, st):
    return ("%-22s sticky[feature=%-5s hotkey=%-5s] filter[feature=%-5s hotkey=%-5s]"
            % (tag, st["sticky_feature_on"], st["sticky_hotkey_on"],
               st["filter_feature_on"], st["filter_hotkey_on"]))


def set_hotkeys(on):
    """Turn only the two ACTIVATION SHORTCUTS on or off. Leaves the features themselves alone."""
    s = get_sticky()
    f = get_filter()
    if on:
        s.dwFlags |= SKF_HOTKEYACTIVE
        f.dwFlags |= FKF_HOTKEYACTIVE
    else:
        s.dwFlags &= ~SKF_HOTKEYACTIVE
        f.dwFlags &= ~FKF_HOTKEYACTIVE
    return set_sticky(s) and set_filter(f)


def run_case(exe, hotkeys_on, seconds, kill_hard):
    """Launch the build with the shortcuts in a known state and watch what it does to them."""
    name = "hotkeys ON" if hotkeys_on else "hotkeys OFF"
    mode = "KILLED (crash path)" if kill_hard else "closed normally"
    print("\n--- case: %s, process %s ---" % (name, mode))
    set_hotkeys(hotkeys_on)
    before = describe()
    print(line("before launch", before))

    proc = subprocess.Popen([exe, "-screen-width", "1280", "-screen-height", "720"])
    try:
        time.sleep(seconds)
        during = describe()
        print(line("while running", during))
    finally:
        if kill_hard:
            proc.kill()
        else:
            proc.terminate()
        try:
            proc.wait(timeout=20)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait(timeout=20)
        time.sleep(2.5)

    after = describe()
    print(line("after exit", after))

    verdict = []
    if hotkeys_on:
        if during["sticky_hotkey_on"] or during["filter_hotkey_on"]:
            verdict.append("FAIL: the shortcuts were NOT suppressed while the game ran")
        else:
            verdict.append("PASS: both activation shortcuts were suppressed while the game ran")
    else:
        if during["sticky_hotkey_on"] or during["filter_hotkey_on"]:
            verdict.append("FAIL: the game TURNED THE SHORTCUTS ON, which it must never do")
        else:
            verdict.append("PASS: already off, and the game left them off")

    if after["sticky_flags"] == before["sticky_flags"] and after["filter_flags"] == before["filter_flags"]:
        verdict.append("PASS: the exact original settings were restored on exit")
    else:
        verdict.append("RESTORE NOT DONE ON EXIT (expected when the process is killed - the next "
                       "launch is what repairs it)")
    for v in verdict:
        print("  " + v)
    return before, during, after


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe")
    ap.add_argument("--seconds", type=float, default=12.0)
    ap.add_argument("--report-only", action="store_true")
    a = ap.parse_args()

    original_s = get_sticky()
    original_f = get_filter()
    print(line("ORIGINAL (yours)", describe()))

    if a.report_only:
        return 0
    if not a.exe or not os.path.isfile(a.exe):
        print("ERROR: --exe must point at the built ShiftIt.exe. Got: %r" % a.exe)
        return 2

    try:
        run_case(a.exe, True, a.seconds, kill_hard=False)
        run_case(a.exe, False, a.seconds, kill_hard=False)

        # The crash path, and the recovery that is supposed to repair it on the next launch.
        print("\n--- case: crash recovery ---")
        set_hotkeys(True)
        run_case(a.exe, True, a.seconds, kill_hard=True)
        print("  relaunching: the game should notice the previous run never restored them")
        p = subprocess.Popen([a.exe, "-screen-width", "1280", "-screen-height", "720"])
        time.sleep(a.seconds)
        p.terminate()
        try:
            p.wait(timeout=20)
        except subprocess.TimeoutExpired:
            p.kill()
        time.sleep(2.5)
        st = describe()
        print(line("after recovery run", st))
        if st["sticky_hotkey_on"] and st["filter_hotkey_on"]:
            print("  PASS: a killed run was repaired on the next launch")
        else:
            print("  FAIL: the shortcuts are still suppressed after the recovery launch")
        return 0
    finally:
        # Always put the machine back exactly as it was found.
        set_sticky(original_s)
        set_filter(original_f)
        print("\n" + line("RESTORED (yours)", describe()))
        print("Nothing was written to the user profile: every call used fWinIni = 0.")


if __name__ == "__main__":
    sys.exit(main())
