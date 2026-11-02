using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// SHIFT IT! is played entirely with the Shift keys, which collides with two Windows accessibility
    /// shortcuts: Sticky Keys (Shift x5) and Filter Keys (hold Right Shift 8s). This class switches those
    /// SHORTCUTS off while the game has focus and puts the player's exact original settings back when the
    /// game loses focus or quits. Only active in a Windows standalone player: the editor, WebGL and every
    /// other platform get no-op stubs, so a test run can never change the developer's machine settings.
    /// </summary>
    public static class WindowsAccessibility
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const uint SPI_GETSTICKYKEYS = 0x003A;
        private const uint SPI_SETSTICKYKEYS = 0x003B;
        private const uint SPI_GETFILTERKEYS = 0x0032;
        private const uint SPI_SETFILTERKEYS = 0x0033;

        // Bit 0x1 (STICKYKEYSON / FILTERKEYSON) is the feature itself; bit 0x4 is the activation hotkey.
        private const uint SKF_HOTKEYACTIVE = 0x00000004;
        private const uint FKF_HOTKEYACTIVE = 0x00000004;

        [StructLayout(LayoutKind.Sequential)]
        private struct STICKYKEYS
        {
            public uint cbSize;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILTERKEYS
        {
            public uint cbSize;
            public uint iWaitMSec;
            public uint iDelayMSec;
            public uint iRepeatMSec;
            public uint iBounceMSec;
            public uint dwFlags;
        }

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref STICKYKEYS pvParam, uint fWinIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref FILTERKEYS pvParam, uint fWinIni);

        private static STICKYKEYS s_origSticky;
        private static FILTERKEYS s_origFilter;
        private static bool s_applied;          // our suppressed settings are currently live
        private static bool s_hooked;
        private static bool s_warnedSuppress;
        private static bool s_warnedRestore;
        private static string s_status = "not yet run";

        // Crash recovery. SystemParametersInfo is called with fWinIni = 0, so nothing is written to
        // the user profile and a reboot clears it - but between a crash and the next reboot the
        // player is left with their Shift shortcuts switched off, and this game is for people who
        // may well need them. Roy's brief says ALWAYS restore, so the original flags are written to
        // disk at the moment they are changed and repaired on the next launch if we never got to
        // put them back. Only dwFlags is ever modified, so only dwFlags needs saving.
        private const string PrefDirty  = "shiftit.a11y.dirty";
        private const string PrefSticky = "shiftit.a11y.sticky";
        private const string PrefFilter = "shiftit.a11y.filter";

        /// <summary>
        /// If a previous run changed the shortcuts and never restored them - a crash, or a killed
        /// process - put the saved originals back before doing anything else. Safe to call always.
        /// </summary>
        public static void RecoverFromPreviousRun()
        {
            try
            {
                if (PlayerPrefs.GetInt(PrefDirty, 0) != 1) return;

                STICKYKEYS sk = new STICKYKEYS { cbSize = (uint)Marshal.SizeOf(typeof(STICKYKEYS)) };
                FILTERKEYS fk = new FILTERKEYS { cbSize = (uint)Marshal.SizeOf(typeof(FILTERKEYS)) };
                if (SystemParametersInfo(SPI_GETSTICKYKEYS, sk.cbSize, ref sk, 0) &&
                    SystemParametersInfo(SPI_GETFILTERKEYS, fk.cbSize, ref fk, 0))
                {
                    sk.dwFlags = (uint)PlayerPrefs.GetInt(PrefSticky, (int)sk.dwFlags);
                    fk.dwFlags = (uint)PlayerPrefs.GetInt(PrefFilter, (int)fk.dwFlags);
                    SystemParametersInfo(SPI_SETSTICKYKEYS, sk.cbSize, ref sk, 0);
                    SystemParametersInfo(SPI_SETFILTERKEYS, fk.cbSize, ref fk, 0);
                    Debug.LogWarning("[SHIFTIT] a previous run did not restore the Sticky/Filter Keys " +
                                     "shortcuts; the saved originals have been put back.");
                }
                PlayerPrefs.SetInt(PrefDirty, 0);
                PlayerPrefs.Save();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SHIFTIT] accessibility crash-recovery failed: " + e.Message);
            }
        }

        private static void MarkDirty(uint stickyFlags, uint filterFlags)
        {
            try
            {
                PlayerPrefs.SetInt(PrefSticky, (int)stickyFlags);
                PlayerPrefs.SetInt(PrefFilter, (int)filterFlags);
                PlayerPrefs.SetInt(PrefDirty, 1);
                PlayerPrefs.Save();
            }
            catch { /* a failure here must never stop the game starting */ }
        }

        private static void MarkClean()
        {
            try { PlayerPrefs.SetInt(PrefDirty, 0); PlayerPrefs.Save(); }
            catch { }
        }

        /// <summary>True while the Sticky/Filter Keys shortcuts are suppressed by this game.</summary>
        public static bool IsSuppressed { get { return s_applied; } }

        /// <summary>Short human-readable result, intended for the Stage 1 test report.</summary>
        public static string StatusLine { get { return s_status; } }

        /// <summary>
        /// Reads the player's current Sticky Keys and Filter Keys settings, remembers them, then writes back
        /// copies with ONLY the hotkey bit cleared. We deliberately leave the "feature on" bit alone: a player
        /// who relies on Sticky Keys or Filter Keys as an accessibility aid must keep it. We only suppress the
        /// activation shortcut that Shift mashing would otherwise trigger. Changes are not persisted to the
        /// user profile (fWinIni = 0), so a hard crash cannot leave them permanently altered after a reboot.
        /// Never throws.
        /// </summary>
        public static void Suppress()
        {
            if (s_applied) return;
            try
            {
                STICKYKEYS sk = new STICKYKEYS { cbSize = (uint)Marshal.SizeOf(typeof(STICKYKEYS)) };
                FILTERKEYS fk = new FILTERKEYS { cbSize = (uint)Marshal.SizeOf(typeof(FILTERKEYS)) };
                if (!SystemParametersInfo(SPI_GETSTICKYKEYS, sk.cbSize, ref sk, 0) ||
                    !SystemParametersInfo(SPI_GETFILTERKEYS, fk.cbSize, ref fk, 0))
                {
                    s_status = "read failed - left untouched";
                    WarnOnce(ref s_warnedSuppress, "WindowsAccessibility: could not read Sticky/Filter Keys settings.");
                    return;
                }

                s_origSticky = sk;
                s_origFilter = fk;
                MarkDirty(sk.dwFlags, fk.dwFlags);
                bool stickyWasOn = (sk.dwFlags & SKF_HOTKEYACTIVE) != 0;
                bool filterWasOn = (fk.dwFlags & FKF_HOTKEYACTIVE) != 0;

                STICKYKEYS newSk = sk;
                newSk.dwFlags &= ~SKF_HOTKEYACTIVE;
                FILTERKEYS newFk = fk;
                newFk.dwFlags &= ~FKF_HOTKEYACTIVE;

                // Mark applied BEFORE writing so a partial write is still rolled back by Restore().
                s_applied = true;
                bool okS = !stickyWasOn || SystemParametersInfo(SPI_SETSTICKYKEYS, newSk.cbSize, ref newSk, 0);
                bool okF = !filterWasOn || SystemParametersInfo(SPI_SETFILTERKEYS, newFk.cbSize, ref newFk, 0);

                if (!okS || !okF)
                {
                    RestoreInternal();
                    s_status = "write failed - restored originals";
                    WarnOnce(ref s_warnedSuppress, "WindowsAccessibility: could not write Sticky/Filter Keys settings.");
                    return;
                }

                s_status = "sticky=suppressed(" + (stickyWasOn ? "was hotkey on" : "hotkey already off") + ") " +
                           "filter=suppressed(" + (filterWasOn ? "was hotkey on" : "hotkey already off") + ")";
            }
            catch (Exception e)
            {
                s_status = "error - " + e.GetType().Name;
                WarnOnce(ref s_warnedSuppress, "WindowsAccessibility.Suppress failed: " + e.Message);
            }
        }

        /// <summary>
        /// Writes the stored original settings back byte for byte. Safe to call repeatedly; does nothing if
        /// <see cref="Suppress"/> never succeeded or the settings are already restored. Never throws.
        /// </summary>
        public static void Restore()
        {
            if (!s_applied) return;
            try
            {
                RestoreInternal();
            }
            catch (Exception e)
            {
                WarnOnce(ref s_warnedRestore, "WindowsAccessibility.Restore failed: " + e.Message);
            }
        }

        /// <summary>Restore immediately; call from a quit button before Application.Quit().</summary>
        public static void RestoreNow()
        {
            Restore();
        }

        private static void RestoreInternal()
        {
            STICKYKEYS sk = s_origSticky;
            FILTERKEYS fk = s_origFilter;
            bool okS = SystemParametersInfo(SPI_SETSTICKYKEYS, sk.cbSize, ref sk, 0);
            bool okF = SystemParametersInfo(SPI_SETFILTERKEYS, fk.cbSize, ref fk, 0);
            if (okS && okF)
            {
                s_applied = false;
                MarkClean();
                s_status = "restored originals";
            }
            else
            {
                // Keep s_applied true so a later Restore() retries.
                WarnOnce(ref s_warnedRestore, "WindowsAccessibility: could not restore Sticky/Filter Keys settings.");
            }
        }

        private static void WarnOnce(ref bool flag, string message)
        {
            if (flag) return;
            flag = true;
            Debug.LogWarning(message);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoStart()
        {
            RecoverFromPreviousRun();
            try
            {
                if (!s_hooked)
                {
                    s_hooked = true;
                    Application.quitting += Restore;
                    Application.focusChanged += OnFocusChanged;
                }
                Suppress();
            }
            catch (Exception e)
            {
                Debug.LogWarning("WindowsAccessibility init failed: " + e.Message);
            }
        }

        private static void OnFocusChanged(bool focused)
        {
            if (focused) Suppress(); else Restore();
        }
#else
        /// <summary>Always false on non-Windows players and in the editor.</summary>
        public static bool IsSuppressed { get { return false; } }

        /// <summary>Short human-readable result, intended for the Stage 1 test report.</summary>
        public static string StatusLine
        {
            get
            {
#if UNITY_EDITOR
                return "editor - skipped (machine settings never touched)";
#else
                return "not Windows - skipped";
#endif
            }
        }

        /// <summary>No-op on this platform.</summary>
        public static void Suppress() { }

        /// <summary>No-op on this platform.</summary>
        public static void Restore() { }

        /// <summary>No-op on this platform; safe to call from a quit button.</summary>
        public static void RestoreNow() { }
        /// <summary>No-op off Windows: nothing was ever changed.</summary>
        public static void RecoverFromPreviousRun() { }
#endif
    }
}
