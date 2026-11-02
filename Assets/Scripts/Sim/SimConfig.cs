using System;

namespace KT.ShiftIt.Sim
{
    /// <summary>
    /// Every balance number in the game. Brief rule 6: no balance number in code.
    /// Loaded from Resources/shiftit_balance.json at boot; these field values are the
    /// fallback defaults AND the shipped values, kept in sync by a test.
    ///
    /// Plain [Serializable] fields only, so Unity's JsonUtility can read it without this
    /// assembly ever referencing UnityEngine.
    /// </summary>
    [Serializable]
    public sealed class SimConfig
    {
        // ---- Fuse (Roy's change request, 2 Nov 2026) --------------------------------
        public float fuseStart = 3.0f;
        public float fusePerPass = 0.06f;
        public float fuseMin = 0.95f;

        // ---- Receiver return --------------------------------------------------------
        public float returnDelayStart = 0.55f;
        public float returnDelayPerPass = 0.01f;
        public float returnDelayMin = 0.20f;

        /// <summary>Bomb flight time one way. "Return throw speeds up too."</summary>
        public float flightStart = 0.26f;
        public float flightPerPass = 0.0025f;
        public float flightMin = 0.14f;

        // ---- PERFECT band: fraction of the fuse REMAINING at the moment of release --
        public float bandLow = 0.18f;
        public float bandHighStart = 0.42f;
        public float bandHighEnd = 0.30f;
        /// <summary>Passes over which bandHigh narrows from Start to End.</summary>
        public float bandNarrowOverPasses = 40f;

        /// <summary>
        /// Throws inside this fraction of the fuse elapsed score nothing ("RUSHED").
        /// 0 = disabled, which is how Stage 1 ships. See DECISIONS_FOR_ROY.md D-009:
        /// this is the lever if panic-throwing turns out to be a dominant safe strategy.
        /// </summary>
        public float rushedBelowElapsedFraction = 0f;

        // ---- Scoring ----------------------------------------------------------------
        public int perfectMaxPoints = 6;
        public int hotScoreMultiplier = 2;
        public int dudFizzlePoints = 2;

        // ---- Zones ------------------------------------------------------------------
        public int zoneEveryPasses = 15;
        public int zoneCount = 4;

        // ---- Mode A: hold & release -------------------------------------------------
        public float sweepStartDegPerSec = 140f;
        public float sweepPerPass = 5f;
        public float sweepMaxDegPerSec = 340f;
        public float holdFuseMultiplier = 1.5f;
        public float maxHoldSeconds = 5.0f;
        public int modeASlots = 5;
        /// <summary>Half-angle of the whole arc the arrow sweeps, degrees either side of straight up.</summary>
        public float modeAArcHalfDeg = 70f;
        /// <summary>Half-width of one robot's catch window. Gaps are what is left over.</summary>
        public float modeASlotHalfWidthDeg = 12f;
        /// <summary>
        /// MODE A ONLY. When greater than 0, a PERFECT is earned by releasing within this many
        /// degrees of a robot's CENTRE instead of by the fuse band.
        ///
        /// Why the option exists: as specified, Mode A asks the player to satisfy two independent
        /// constraints at the same instant - be on a robot AND have the ring's tip in the green.
        /// Those multiply, so PERFECT is rare and the mode reads as unfair rather than hard. Judging
        /// Mode A's PERFECT on aim accuracy instead gives it ONE skill to master, and it is the
        /// skill the mechanic is actually about. 0 = disabled, which is Roy's original rule.
        /// See DECISIONS_FOR_ROY D-025; measured both ways in BalanceTests.
        /// </summary>
        public float modeAPerfectWithinDeg = 0f;

        // ---- Blocker and twist unlocks, by passes completed -------------------------
        public int unlockShield = 5;
        public int unlockAsleep = 10;
        public int unlockOnBreak = 10;
        public int unlockHot = 12;
        public int unlockMirrored = 18;
        public int unlockDud = 25;
        public int unlockBothBlocked = 30;
        public int unlockSwap = 40;
        /// <summary>Mode A shows two blocked robots at once from here.</summary>
        public int unlockTwoBlockedModeA = 30;

        // ---- Twist chances ----------------------------------------------------------
        public float chanceHot = 0.20f;
        public float chanceDud = 0.18f;
        public float chanceMirrored = 0.25f;
        public float chanceBothBlocked = 0.25f;
        public float chanceSwap = 0.35f;

        /// <summary>
        /// Probability that a normal turn has a blocker at all, ramped between the shield
        /// unlock and blockerRampEndPass. This is the main difficulty dial and the one number
        /// the 20-run balance sweep tunes. Roy's spec did not fix it.
        /// </summary>
        public float blockerChanceAtUnlock = 0.35f;
        public float blockerChanceAtEnd = 0.85f;
        public int blockerRampEndPass = 40;
        /// <summary>
        /// The pass at which blockers start appearing at all. Separate from unlockShield on
        /// purpose: tying the two together meant a test could not force one blocker KIND without
        /// also switching the whole ramp off.
        /// </summary>
        public int blockerRampStartPass = 5;

        /// <summary>Fuse fraction remaining when a both-blocked turn opens one side.</summary>
        public float bothBlockedOpenAtMin = 0.40f;
        public float bothBlockedOpenAtMax = 0.60f;
        /// <summary>Fuse fraction remaining when a blocked side swaps.</summary>
        public float swapAtMin = 0.45f;
        public float swapAtMax = 0.70f;

        // ---- Accessibility / input safety -------------------------------------------
        /// <summary>Game over ignores input this long so panic presses cannot skip it.</summary>
        public float retryIgnoreSeconds = 0.4f;
        /// <summary>
        /// Warn the player if a Shift key is held this long. Windows opens Filter Keys at
        /// 8 s of held Right Shift, so nothing in this game may approach it.
        /// </summary>
        public float holdWarningSeconds = 3.5f;
        /// <summary>Explosion dwell before the game-over screen takes over.</summary>
        public float explodeSeconds = 0.9f;

        // ---- Derived values ---------------------------------------------------------
        public float FuseFor(int passes) => Max(fuseMin, fuseStart - fusePerPass * passes);
        public float ReturnDelayFor(int passes) => Max(returnDelayMin, returnDelayStart - returnDelayPerPass * passes);
        public float FlightFor(int passes) => Max(flightMin, flightStart - flightPerPass * passes);
        public float SweepFor(int passes) => Min(sweepMaxDegPerSec, sweepStartDegPerSec + sweepPerPass * passes);

        public float BandHighFor(int passes)
        {
            float t = bandNarrowOverPasses <= 0f ? 1f : Clamp01(passes / bandNarrowOverPasses);
            return bandHighStart + (bandHighEnd - bandHighStart) * t;
        }

        public float BlockerChanceFor(int passes)
        {
            if (passes < blockerRampStartPass) return 0f;
            float span = blockerRampEndPass - blockerRampStartPass;
            float t = span <= 0f ? 1f : Clamp01((passes - blockerRampStartPass) / span);
            return blockerChanceAtUnlock + (blockerChanceAtEnd - blockerChanceAtUnlock) * t;
        }

        public int ZoneFor(int passes)
        {
            if (zoneEveryPasses <= 0) return 0;
            int z = passes / zoneEveryPasses;
            return z >= zoneCount ? zoneCount - 1 : z;
        }

        static float Max(float a, float b) => a > b ? a : b;
        static float Min(float a, float b) => a < b ? a : b;
        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public SimConfig Clone() => (SimConfig)MemberwiseClone();
    }
}
