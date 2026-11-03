using System.Runtime.InteropServices;
using System.Text;
using KT.ShiftIt.Sim;
using UnityEngine;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// Publishes the live game state to the hosting page, so an automated session can play the real
    /// WebGL build the way a player does: by reading the fuse, the blockers and the bomb kind and
    /// deciding what to press.
    ///
    /// This exists because of a lesson this project has already learnt the hard way - a harness that
    /// cannot see the game does not test the game. A driver that mashes Shift on a timer dies in a
    /// few passes and its "ten runs" say nothing about whether the shipped build plays. With the
    /// bridge, the browser driver sees exactly what is on screen and nothing that is not.
    ///
    /// The bridge is WRITE-ONLY and one-directional: the page can read state, and cannot reach in.
    /// Input still arrives as real browser key events through the Input System, so the thing being
    /// tested is the actual input path, not a back door.
    ///
    /// Outside WebGL every method compiles to nothing.
    /// </summary>
    public static class WebBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void ShiftItReportState(string s);
        [DllImport("__Internal")] static extern void ShiftItReportRunEnd(int passes, int score, string reason);
        public const bool Available = true;
#else
        static void ShiftItReportState(string s) { }
        static void ShiftItReportRunEnd(int passes, int score, string reason) { }
        public const bool Available = false;
#endif

        static readonly StringBuilder _sb = new StringBuilder(160);

        /// <summary>
        /// One compact pipe-separated line, rebuilt each frame. Deliberately not JSON: this runs
        /// every frame in a browser, and a string the driver can split is cheaper than a parse on
        /// both sides.
        ///
        /// phase|passes|score|best|zone|fuse|fuseMax|bandLo|bandHi|bomb|slot0|slot1|mirrored|
        /// bothBlocked|openSlot|opened|canSwap|locked
        /// </summary>
        public static void Report(ShiftSim sim, bool inputLocked)
        {
            if (!Available || sim == null) return;

            var t = sim.Turn;
            _sb.Clear();
            _sb.Append((int)sim.Phase).Append('|')
               .Append(sim.Passes).Append('|')
               .Append(sim.Score).Append('|')
               .Append(sim.Best).Append('|')
               .Append(sim.Zone).Append('|')
               .Append(sim.FuseFraction.ToString("F4")).Append('|')
               .Append(sim.FuseMax.ToString("F3")).Append('|')
               .Append(sim.BandLow.ToString("F4")).Append('|')
               .Append(sim.BandHigh.ToString("F4")).Append('|')
               .Append((int)t.Bomb).Append('|')
               .Append(t.Slots.Length > 0 ? (int)t.Slots[0] : 0).Append('|')
               .Append(t.Slots.Length > 1 ? (int)t.Slots[1] : 0).Append('|')
               .Append(t.Mirrored ? 1 : 0).Append('|')
               .Append(t.BothBlocked ? 1 : 0).Append('|')
               .Append(t.OpenSlot).Append('|')
               .Append(t.Opened ? 1 : 0).Append('|')
               .Append(t.CanSwap ? 1 : 0).Append('|')
               .Append(inputLocked ? 1 : 0);
            ShiftItReportState(_sb.ToString());
        }

        /// <summary>Called once when a run ends, so the driver can tally without polling for it.</summary>
        public static void ReportRunEnd(ShiftSim sim)
        {
            if (!Available || sim == null) return;
            ShiftItReportRunEnd(sim.Passes, sim.Score, sim.Fail.ToString());
        }
    }
}
