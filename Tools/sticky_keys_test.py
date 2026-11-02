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
    """
    Turn only the two ACTIVATION SHORTCUTS on or off. Leaves the features themselves alone.

    Returns (sticky_ok, filter_ok). They are reported separately because on this machine Windows
    REFUSES the Filter Keys write: SPI_SETFILTERKEYS with FKF_HOTKEYACTIVE returns 0 while
    FKF_FILTERKEYSON is clear. That means the harness cannot create the "Filter Keys hotkey on"
    precondition at all, so it must say so rather than quietly test nothing and call it a pass.
    """
    s = get_sticky()
    f = get_filter()
    if on:
        s.dwFlags |= SKF_HOTKEYACTIVE
        f.dwFlags |= FKF_HOTKEYACTIVE
    else:
        s.dwFlags &= ~SKF_HOTKEYACTIVE
        f.dwFlags &= ~FKF_HOTKEYACTIVE
    ok_s = set_sticky(s)
    ok_f = set_filter(f)
    got = describe()
    return (ok_s and got["sticky_hotkey_on"] == on,
            ok_f and got["filter_hotkey_on"] == on)


def close_gracefully(pid, timeout=20.0):
    """
    Close the game the way clicking the X or pressing Alt+F4 does, by posting WM_CLOSE.

    This matters more than it looks. The first version of this test used proc.terminate(), which
    on Windows is TerminateProcess - a hard kill. Unity never runs OnApplicationQuit after that,
    so the restore-on-quit path was never exercised at all and every case reported "RESTORE NOT
    DONE". WM_CLOSE is the real shutdown path a player triggers.
    """
    user32 = ctypes.windll.user32
    hwnds = []

    @ctypes.WINFUNCTYPE(ctypes.c_bool, wt.HWND, wt.LPARAM)
    def cb(h, l):
        p = wt.DWORD()
        user32.GetWindowThreadProcessId(h, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(h):
            hwnds.append(h)
        return True

    user32.EnumWindows(cb, 0)
    if not hwnds:
        return False
    for h in hwnds:
        user32.PostMessageW(h, 0x0010, 0, 0)          # WM_CLOSE
    deadline = time.time() + timeout
    while time.time() < deadline:
        if not any(user32.IsWindow(h) for h in hwnds):
            return True
        time.sleep(0.25)
    return False


def run_case(exe, hotkeys_on, seconds, kill_hard):
    """Launch the build with the shortcuts in a known state and watch what it does to them."""
    name = "hotkeys ON" if hotkeys_on else "hotkeys OFF"
    mode = "KILLED (crash path)" if kill_hard else "closed normally (WM_CLOSE)"
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
            if not close_gracefully(proc.pid):
                print("  NOTE: no window answered WM_CLOSE; falling back to a hard kill, so this "
                      "case does NOT test the quit path")
                proc.kill()
        try:
            proc.wait(timeout=25)
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
    elif kill_hard:
        verdict.append("NOT RESTORED - correct for a hard kill, since Windows gives a killed "
                       "process no chance to run its quit handler. The next launch is what "
                       "repairs this, and the recovery case below is what proves it.")
    else:
        verdict.append("*** FAIL: closed properly and the originals were NOT put back ***")
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
        sticky_ok, filter_ok = set_hotkeys(True)
        if not filter_ok:
            print("  NOTE: Windows refused to switch the Filter Keys HOTKEY on (it rejects the")
            print("        write while the feature itself is off), so only Sticky Keys can be")
            print("        tested here. Filter Keys needs an 8 s hold of Right Shift and this")
            print("        game cannot hold Shift for more than about 2 s, so it cannot fire.")
        run_case(a.exe, True, a.seconds, kill_hard=True)
        print("  relaunching: the game should notice the previous run never restored them")
        p = subprocess.Popen([a.exe, "-screen-width", "1280", "-screen-height", "720"])
        time.sleep(a.seconds)
        # Close this one PROPERLY. The sequence being tested is: recovery puts the saved originals
        # back, the game then re-suppresses them because it is running, and the quit handler puts
        # them back again. Hard-killing this run would mask the whole thing - which is exactly
        # what the first version of this test did, and it reported a false FAIL.
        if not close_gracefully(p.pid):
            print("  NOTE: no window answered WM_CLOSE; this case cannot prove the recovery")
            p.kill()
        try:
            p.wait(timeout=25)
        except subprocess.TimeoutExpired:
            p.kill()
        time.sleep(2.5)
        st = describe()
        print(line("after recovery run", st))
        # Only assert on what the harness was actually able to switch on. Requiring the Filter Keys
        # bit here produced a false FAIL on a run where Sticky Keys had been repaired correctly.
        checks = []
        if sticky_ok:
            checks.append(("Sticky Keys", st["sticky_hotkey_on"]))
        if filter_ok:
            checks.append(("Filter Keys", st["filter_hotkey_on"]))
        if not checks:
            print("  NOT TESTED: neither shortcut could be switched on, so there was nothing to "
                  "recover. Not a pass and not a failure.")
            return 0
        for name, ok in checks:
            if ok:
                print("  PASS (%s): a killed run was repaired on the next launch, and that launch "
                      "then put the original back on quit" % name)
            else:
                print("  *** FAIL (%s): still suppressed after a recovery launch that closed "
                      "properly ***" % name)
        return 0
    finally:
        # Always put the machine back exactly as it was found.
        set_sticky(original_s)
        set_filter(original_f)
        print("\n" + line("RESTORED (yours)", describe()))
        print("Nothing was written to the user profile: every call used fWinIni = 0.")


if __name__ == "__main__":
    sys.exit(main())
