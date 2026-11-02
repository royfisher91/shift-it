using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using KT.ShiftIt.Sim;

namespace KT.ShiftIt.Tests
{
    /// <summary>
    /// Unit tests for the pure simulation. Every test forces its scenario through the real
    /// CONFIG rather than a test back door, so the code path under test is the shipping one.
    /// </summary>
    public class SimTests
    {
        const float DT = 1f / 120f;

        static SimConfig Cfg() => new SimConfig();

        /// <summary>A config where no twist of any kind can occur. The baseline for timing tests.</summary>
        static SimConfig Plain()
        {
            var c = new SimConfig();
            c.chanceDud = 0f; c.chanceHot = 0f; c.chanceMirrored = 0f;
            c.chanceBothBlocked = 0f; c.chanceSwap = 0f;
            c.blockerChanceAtUnlock = 0f; c.blockerChanceAtEnd = 0f;
            return c;
        }

        static ShiftSim NewSim(SimConfig c, ControlMode mode = ControlMode.LeftRight, int seed = 1234)
        {
            var s = new ShiftSim(c, seed, mode);
            var all = new Hint[(int)Hint.Count];
            for (int i = 0; i < all.Length; i++) all[i] = (Hint)i;
            s.RestoreShownHints(all);
            s.StartRun();
            return s;
        }

        static void Tap(ShiftSim s, Side side)
        {
            s.SetKey(side, true);
            s.SetKey(side, false);
        }

        /// <summary>Tick until the fuse drops to the given fraction of its maximum.</summary>
        static void TickToFuseFraction(ShiftSim s, float frac)
        {
            int guard = 0;
            while (s.FuseFraction > frac && s.Phase == Phase.PlayerHolding && guard++ < 100000)
                s.Tick(DT);
        }

        static void TickSeconds(ShiftSim s, float seconds)
        {
            int n = (int)Math.Round(seconds / DT);
            for (int i = 0; i < n; i++) s.Tick(DT);
        }

        static bool HasEvent(ShiftSim s, SimEventKind k)
        {
            foreach (var e in s.Events) if (e.Kind == k) return true;
            return false;
        }

        // =================================================================
        //  Difficulty ramp - Roy's change request numbers, 2 Nov 2026
        // =================================================================

        [Test]
        public void Fuse_RampMatchesSpec()
        {
            var c = Cfg();
            Assert.AreEqual(3.00f, c.FuseFor(0), 1e-4f, "fuse starts at 3.0 s");
            Assert.AreEqual(2.40f, c.FuseFor(10), 1e-4f, "0.06 s per pass");
            Assert.AreEqual(1.20f, c.FuseFor(30), 1e-4f);
            Assert.AreEqual(0.95f, c.FuseFor(60), 1e-4f, "clamped to the 0.95 s minimum");
            Assert.AreEqual(0.95f, c.FuseFor(500), 1e-4f, "never goes below the minimum");
        }

        [Test]
        public void ReturnDelay_RampMatchesSpec()
        {
            var c = Cfg();
            Assert.AreEqual(0.55f, c.ReturnDelayFor(0), 1e-4f);
            Assert.AreEqual(0.45f, c.ReturnDelayFor(10), 1e-4f, "0.01 s per pass");
            Assert.AreEqual(0.20f, c.ReturnDelayFor(40), 1e-4f, "clamped to 0.20 s");
            Assert.AreEqual(0.20f, c.ReturnDelayFor(200), 1e-4f);
        }

        [Test]
        public void Sweep_RampMatchesSpec()
        {
            var c = Cfg();
            Assert.AreEqual(140f, c.SweepFor(0), 1e-3f, "sweep starts at 140 deg/s");
            Assert.AreEqual(190f, c.SweepFor(10), 1e-3f, "+5 per pass");
            Assert.AreEqual(340f, c.SweepFor(40), 1e-3f, "capped at 340 deg/s");
            Assert.AreEqual(340f, c.SweepFor(400), 1e-3f);
        }

        [Test]
        public void PerfectBand_NarrowsWithPasses()
        {
            var c = Cfg();
            Assert.AreEqual(0.18f, c.bandLow, 1e-4f, "low edge stays at 18%");
            Assert.AreEqual(0.42f, c.BandHighFor(0), 1e-4f, "high edge starts at 42%");
            Assert.AreEqual(0.30f, c.BandHighFor(40), 1e-4f, "narrows to 30%");
            Assert.AreEqual(0.30f, c.BandHighFor(90), 1e-4f, "and stops there");
            Assert.Less(c.BandHighFor(20), c.BandHighFor(0), "monotonically narrowing");
        }

        [Test]
        public void Zones_ShiftEvery15Passes()
        {
            var c = Cfg();
            Assert.AreEqual(0, c.ZoneFor(0));
            Assert.AreEqual(0, c.ZoneFor(14));
            Assert.AreEqual(1, c.ZoneFor(15), "ROOFTOP at 15");
            Assert.AreEqual(2, c.ZoneFor(30), "SCRAPYARD at 30");
            Assert.AreEqual(3, c.ZoneFor(45), "LAUNCHPAD at 45");
            Assert.AreEqual(3, c.ZoneFor(999), "and there is no fifth zone");
        }

        // =================================================================
        //  Scoring
        // =================================================================

        [Test]
        public void NormalThrow_ScoresOne()
        {
            var s = NewSim(Plain());
            Tap(s, Side.Left);                        // thrown immediately: before the band
            Assert.AreEqual(1, s.Score);
            Assert.AreEqual(0, s.Combo, "an early throw breaks combo");
            Assert.AreEqual(1, s.Passes);
        }

        [Test]
        public void PerfectThrow_InsideBand_StartsComboAtTwo()
        {
            var s = NewSim(Plain());
            TickToFuseFraction(s, (s.BandLow + s.BandHigh) * 0.5f);
            Tap(s, Side.Left);
            Assert.AreEqual(1, s.Combo);
            Assert.AreEqual(2, s.Score, "first PERFECT is 1 + combo = 2");
        }

        [Test]
        public void PerfectCombo_RisesThenCapsAtSix()
        {
            var s = NewSim(Plain());
            var points = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                TickToFuseFraction(s, (s.BandLow + s.BandHigh) * 0.5f);
                int before = s.Score;
                s.Events.Clear();
                Tap(s, Side.Left);
                points.Add(s.Score - before);
                // fly out, receiver holds, fly back
                while (s.Phase != Phase.PlayerHolding && s.Phase != Phase.GameOver) s.Tick(DT);
                Assert.AreNotEqual(Phase.GameOver, s.Phase, "a perfect run must not end");
            }
            Assert.AreEqual(new[] { 2, 3, 4, 5, 6, 6, 6, 6 }, points.ToArray(),
                "1+combo, capped at the 6-point maximum");
        }

        [Test]
        public void EarlyThrow_ResetsCombo()
        {
            var s = NewSim(Plain());
            TickToFuseFraction(s, (s.BandLow + s.BandHigh) * 0.5f);
            Tap(s, Side.Left);
            Assert.AreEqual(1, s.Combo);
            while (s.Phase != Phase.PlayerHolding) s.Tick(DT);
            Tap(s, Side.Left);                        // immediate = normal
            Assert.AreEqual(0, s.Combo, "a normal throw resets the combo");
        }

        // =================================================================
        //  Bombs
        // =================================================================

        [Test]
        public void HotBomb_HasHalfFuseAndDoublePoints()
        {
            var c = Plain();
            c.unlockHot = 0; c.chanceHot = 1f;
            var s = NewSim(c);
            Assert.AreEqual(BombKind.Hot, s.Bomb);
            Assert.AreEqual(c.FuseFor(0) * 0.5f, s.FuseMax, 1e-4f, "half the fuse");
            Tap(s, Side.Left);
            Assert.AreEqual(2, s.Score, "a normal throw of a HOT bomb is 1 x 2");
        }

        [Test]
        public void DudBomb_HeldToTheEnd_FizzlesForTwo()
        {
            var c = Plain();
            c.unlockDud = 0; c.chanceDud = 1f;
            var s = NewSim(c);
            Assert.AreEqual(BombKind.Dud, s.Bomb);
            s.Events.Clear();
            TickSeconds(s, s.FuseMax + 0.1f);
            Assert.IsTrue(HasEvent(s, SimEventKind.Fizzle), "a held dud fizzles");
            Assert.AreEqual(c.dudFizzlePoints, s.Score, "a fizzle is worth 2");
            Assert.AreNotEqual(Phase.Failing, s.Phase, "and is NOT a failure");
            Assert.AreEqual(1, s.Passes, "it counts as a pass (D-006)");
        }

        [Test]
        public void DudBomb_Thrown_IsAFailure()
        {
            var c = Plain();
            c.unlockDud = 0; c.chanceDud = 1f;
            var s = NewSim(c);
            Tap(s, Side.Left);
            Assert.AreEqual(Phase.Failing, s.Phase);
            Assert.AreEqual(FailReason.ThrewADud, s.Fail);
        }

        [Test]
        public void DudTurn_NeverCarriesABlockerOrMirror()
        {
            // D-004: on a dud, "hold it" must be the only correct action.
            var c = Cfg();
            c.unlockDud = 0; c.chanceDud = 1f;
            c.unlockShield = 0; c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f;
            c.blockerRampStartPass = 0;
            c.unlockMirrored = 0; c.chanceMirrored = 1f;
            for (int seed = 0; seed < 200; seed++)
            {
                var s = NewSim(c, ControlMode.LeftRight, seed);
                Assert.AreEqual(BombKind.Dud, s.Bomb);
                Assert.IsFalse(s.Turn.AnyBlocked(), "seed " + seed + ": dud turn had a blocker");
                Assert.IsFalse(s.Turn.Mirrored, "seed " + seed + ": dud turn was mirrored");
            }
        }

        // =================================================================
        //  Blockers
        // =================================================================

        [TestCase(Blocker.Shield, FailReason.ThrewAtShield)]
        [TestCase(Blocker.Asleep, FailReason.ThrewAtSleeper)]
        [TestCase(Blocker.OnBreak, FailReason.ThrewAtEmpty)]
        public void ThrowingAtABlockedSide_FailsWithTheRightReason(Blocker kind, FailReason expected)
        {
            var c = Plain();
            // Force a blocker on every turn from pass 0, and leave exactly ONE kind unlocked so
            // the kind under test is the only one that can be rolled. blockerRampStartPass is
            // what keeps the frequency ramp alive while every unlock but one is pushed away.
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f;
            c.blockerRampStartPass = 0;
            c.unlockShield = kind == Blocker.Shield ? 0 : int.MaxValue;
            c.unlockAsleep = kind == Blocker.Asleep ? 0 : int.MaxValue;
            c.unlockOnBreak = kind == Blocker.OnBreak ? 0 : int.MaxValue;
            var s = NewSim(c);

            // Find the blocked slot and throw straight at it.
            int blocked = -1;
            for (int i = 0; i < s.Turn.SlotCount; i++)
                if (s.Turn.Slots[i] != Blocker.None) { blocked = i; break; }
            Assert.GreaterOrEqual(blocked, 0, "the forced blocker did not appear");
            Assert.AreEqual(kind, s.Turn.Slots[blocked], "a different blocker kind was rolled");

            Tap(s, blocked == 0 ? Side.Left : Side.Right);
            Assert.AreEqual(Phase.Failing, s.Phase);
            Assert.AreEqual(expected, s.Fail);
        }

        [Test]
        public void ThrowingAtTheOpenSide_Succeeds()
        {
            var c = Plain();
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f; c.blockerRampStartPass = 0;
            c.unlockShield = 0; c.unlockAsleep = int.MaxValue; c.unlockOnBreak = int.MaxValue;
            var s = NewSim(c);
            int open = s.Turn.FirstOpenSlot();
            Assert.GreaterOrEqual(open, 0, "there must always be a right answer");
            Tap(s, open == 0 ? Side.Left : Side.Right);
            Assert.AreNotEqual(Phase.Failing, s.Phase);
            Assert.AreEqual(1, s.Passes);
        }

        [Test]
        public void NoBlockerEverAppearsBeforeItsUnlock()
        {
            var c = Cfg();                            // shipping unlocks
            var seen = new Dictionary<Blocker, int>();
            for (int seed = 0; seed < 400; seed++)
            {
                var s = new ShiftSim(c, seed, ControlMode.LeftRight);
                s.StartRun();                         // pass 0
                for (int i = 0; i < s.Turn.SlotCount; i++)
                {
                    var b = s.Turn.Slots[i];
                    if (b != Blocker.None) seen[b] = seen.TryGetValue(b, out var n) ? n + 1 : 1;
                }
                Assert.AreNotEqual(BombKind.Dud, s.Bomb, "seed " + seed + ": dud before pass 25");
                Assert.AreNotEqual(BombKind.Hot, s.Bomb, "seed " + seed + ": hot before pass 12");
                Assert.IsFalse(s.Turn.Mirrored, "seed " + seed + ": mirrored before pass 18");
                Assert.IsFalse(s.Turn.BothBlocked, "seed " + seed + ": both-blocked before pass 30");
                Assert.IsFalse(s.Turn.CanSwap, "seed " + seed + ": swap before pass 40");
            }
            Assert.IsEmpty(seen, "no blocker may appear on pass 0 (shield unlocks at 5)");
        }

        [Test]
        public void AtLeastOneSlotIsAlwaysThrowable()
        {
            // The one invariant that would make the game unfair if broken. Checked across the
            // whole difficulty curve in both modes.
            var c = Cfg();
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f;   // worst case
            foreach (var mode in new[] { ControlMode.LeftRight, ControlMode.HoldRelease })
            {
                for (int seed = 0; seed < 60; seed++)
                {
                    var s = new ShiftSim(c, seed, mode);
                    s.StartRun();
                    for (int pass = 0; pass < 80; pass++)
                    {
                        bool dud = s.Bomb == BombKind.Dud;
                        bool bothBlockedNow = s.Turn.BothBlocked;
                        if (!dud && !bothBlockedNow)
                            Assert.GreaterOrEqual(s.Turn.FirstOpenSlot(), 0,
                                mode + " seed " + seed + " pass " + pass + ": every slot was blocked");
                        // advance one pass the cheap way
                        if (dud) { TickSeconds(s, s.FuseMax + 0.05f); }
                        else
                        {
                            if (bothBlockedNow) TickToFuseFraction(s, s.Turn.OpenAtFuseFraction - 0.01f);
                            int open = s.Turn.FirstOpenSlot();
                            if (open < 0) break;
                            if (mode == ControlMode.LeftRight)
                                Tap(s, s.Turn.Mirrored ? (open == 0 ? Side.Right : Side.Left)
                                                       : (open == 0 ? Side.Left : Side.Right));
                            else break;   // Mode A aiming is covered by its own tests
                        }
                        int guard = 0;
                        while (s.Phase != Phase.PlayerHolding && s.Phase != Phase.GameOver && guard++ < 10000)
                            s.Tick(DT);
                        if (s.Phase == Phase.GameOver) break;
                    }
                }
            }
        }

        // =================================================================
        //  Mirrored
        // =================================================================

        [Test]
        public void MirroredTurn_SwapsWhichKeyGoesWhere()
        {
            var c = Plain();
            c.unlockMirrored = 0; c.chanceMirrored = 1f;
            var s = NewSim(c);
            Assert.IsTrue(s.Turn.Mirrored);
            Assert.AreEqual(1, s.SlotForKey(Side.Left), "LEFT Shift throws RIGHT when mirrored");
            Assert.AreEqual(0, s.SlotForKey(Side.Right), "RIGHT Shift throws LEFT when mirrored");
        }

        [Test]
        public void MirroredTurn_PressingTheWrongKeyHitsTheBlockedSide()
        {
            var c = Plain();
            c.unlockMirrored = 0; c.chanceMirrored = 1f;
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f; c.blockerRampStartPass = 0;
            c.unlockShield = 0; c.unlockAsleep = int.MaxValue; c.unlockOnBreak = int.MaxValue;
            var s = NewSim(c);
            int blocked = s.Turn.Slots[0] != Blocker.None ? 0 : 1;
            // The key that LOOKS right is the one matching the open side's position.
            int open = 1 - blocked;
            Side looksRight = open == 0 ? Side.Left : Side.Right;
            Tap(s, looksRight);   // mirrored, so this actually goes to the blocked side
            Assert.AreEqual(Phase.Failing, s.Phase, "mirrored must punish the obvious key");
        }

        // =================================================================
        //  Both sides blocked, and swaps
        // =================================================================

        [Test]
        public void BothBlocked_OpensOneSidePartWayThrough()
        {
            var c = Plain();
            c.unlockBothBlocked = 0; c.chanceBothBlocked = 1f;
            var s = NewSim(c);
            Assert.IsTrue(s.Turn.BothBlocked);
            Assert.AreNotEqual(Blocker.None, s.Turn.Slots[0]);
            Assert.AreNotEqual(Blocker.None, s.Turn.Slots[1]);
            Assert.That(s.Turn.OpenAtFuseFraction, Is.InRange(c.bothBlockedOpenAtMin, c.bothBlockedOpenAtMax));

            s.Events.Clear();
            TickToFuseFraction(s, s.Turn.OpenAtFuseFraction - 0.02f);
            Assert.IsTrue(HasEvent(s, SimEventKind.Open), "one side must open");
            Assert.IsTrue(s.Turn.Opened);
            int opened = s.Turn.OpenSlot;
            Assert.AreEqual(Blocker.None, s.Turn.Slots[opened]);

            Tap(s, opened == 0 ? Side.Left : Side.Right);
            Assert.AreNotEqual(Phase.Failing, s.Phase, "throwing into the opened side must work");
        }

        [Test]
        public void BothBlocked_ThrowingBeforeItOpens_Fails()
        {
            var c = Plain();
            c.unlockBothBlocked = 0; c.chanceBothBlocked = 1f;
            var s = NewSim(c);
            Tap(s, Side.Left);
            Assert.AreEqual(Phase.Failing, s.Phase);
        }

        [Test]
        public void BothBlocked_IsNeverAlsoMirrored()
        {
            var c = Cfg();
            c.unlockBothBlocked = 0; c.chanceBothBlocked = 1f;
            c.unlockMirrored = 0; c.chanceMirrored = 1f;
            for (int seed = 0; seed < 200; seed++)
            {
                var s = NewSim(c, ControlMode.LeftRight, seed);
                if (s.Turn.BothBlocked)
                    Assert.IsFalse(s.Turn.Mirrored, "seed " + seed + ": both-blocked AND mirrored is unreadable");
            }
        }

        [Test]
        public void Swap_MovesTheBlockToTheOtherSide()
        {
            var c = Plain();
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f; c.blockerRampStartPass = 0;
            c.unlockShield = 0; c.unlockAsleep = int.MaxValue; c.unlockOnBreak = int.MaxValue;
            c.unlockSwap = 0; c.chanceSwap = 1f;
            var s = NewSim(c);
            Assert.IsTrue(s.Turn.CanSwap);
            int before = s.Turn.Slots[0] != Blocker.None ? 0 : 1;

            s.Events.Clear();
            TickToFuseFraction(s, s.Turn.SwapAtFuseFraction - 0.02f);
            Assert.IsTrue(HasEvent(s, SimEventKind.Swap), "the block must move");
            int after = s.Turn.Slots[0] != Blocker.None ? 0 : 1;
            Assert.AreNotEqual(before, after, "the blocked side must actually change");
            Assert.AreEqual(1, CountBlocked(s), "exactly one side stays blocked after a swap");
        }

        static int CountBlocked(ShiftSim s)
        {
            int n = 0;
            for (int i = 0; i < s.Turn.SlotCount; i++) if (s.Turn.Slots[i] != Blocker.None) n++;
            return n;
        }

        [Test]
        public void MidFuseEvents_SurviveOneEnormousFrame()
        {
            // A WebGL hitch must not let the OPEN or the SWAP be skipped entirely.
            var c = Plain();
            c.unlockBothBlocked = 0; c.chanceBothBlocked = 1f;
            var s = NewSim(c);
            s.Events.Clear();
            s.Tick(s.FuseMax * 0.95f);                 // one catastrophic frame
            Assert.IsTrue(HasEvent(s, SimEventKind.Open), "OPEN must still fire across a huge dt");
        }

        // =================================================================
        //  Failure, game over, retry
        // =================================================================

        [Test]
        public void FuseRunningOut_IsAFailureWithTheRightReason()
        {
            var s = NewSim(Plain());
            TickSeconds(s, s.FuseMax + 0.05f);
            Assert.AreEqual(Phase.Failing, s.Phase);
            Assert.AreEqual(FailReason.FuseRanOut, s.Fail);
        }

        [Test]
        public void GameOver_IgnoresInputForTheLockoutThenRetries()
        {
            var c = Plain();
            var s = NewSim(c);
            TickSeconds(s, s.FuseMax + 0.05f);
            TickSeconds(s, c.explodeSeconds + 0.05f);
            Assert.AreEqual(Phase.GameOver, s.Phase);

            Tap(s, Side.Left);
            Assert.AreEqual(Phase.GameOver, s.Phase, "a panic press must not skip the game-over screen");

            TickSeconds(s, c.retryIgnoreSeconds + 0.05f);
            Tap(s, Side.Left);
            Assert.AreEqual(Phase.PlayerHolding, s.Phase, "after the lockout, Shift retries");
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(0, s.Passes);
        }

        [Test]
        public void GameOver_RecordsPersonalBest()
        {
            var s = NewSim(Plain());
            TickToFuseFraction(s, (s.BandLow + s.BandHigh) * 0.5f);
            Tap(s, Side.Left);                        // +2
            while (s.Phase != Phase.PlayerHolding) s.Tick(DT);
            TickSeconds(s, s.FuseMax + 0.05f);        // die
            TickSeconds(s, s.Cfg.explodeSeconds + 0.05f);
            Assert.AreEqual(Phase.GameOver, s.Phase);
            Assert.AreEqual(2, s.Best);
        }

        // =================================================================
        //  Windows accessibility - the hard requirements
        // =================================================================

        [Test]
        public void HeldKey_ThrowsExactlyOnce()
        {
            // Sticky Keys can latch Shift down. A latched key must not machine-gun throws.
            var s = NewSim(Plain());
            s.SetKey(Side.Left, true);
            int scoreAfterFirst = s.Score;
            for (int i = 0; i < 600; i++) s.Tick(DT);   // 5 s with the key still down
            Assert.AreEqual(1, s.Passes, "one press is one throw, however long it is held");
            Assert.AreEqual(scoreAfterFirst, s.Score, "no extra points from holding");
        }

        [Test]
        public void RepeatedDownEventsWithoutARelease_ThrowOnlyOnce()
        {
            var s = NewSim(Plain());
            for (int i = 0; i < 20; i++) s.SetKey(Side.Left, true);
            Assert.AreEqual(1, s.Passes, "only the down EDGE counts");
        }

        [Test]
        public void HoldWarning_FiresWellBeforeWindowsFilterKeysAtEightSeconds()
        {
            var c = Plain();
            var s = NewSim(c);
            Assert.Less(c.holdWarningSeconds, 8f, "the warning must come before Windows acts");
            s.SetKey(Side.Left, true);
            s.Events.Clear();
            TickSeconds(s, c.holdWarningSeconds + 0.1f);
            Assert.IsTrue(HasEvent(s, SimEventKind.HoldWarning), "a long hold must be called out");
        }

        [Test]
        public void ModeA_MaximumHoldIsUnderEightSeconds()
        {
            var c = Cfg();
            Assert.LessOrEqual(c.maxHoldSeconds, 5f,
                "Windows opens Filter Keys at 8 s of held Right Shift; Mode A caps at 5 s");
        }

        [Test]
        public void ModeA_HoldingPastTheCap_Explodes()
        {
            var c = Plain();
            c.fuseStart = 20f; c.fuseMin = 20f;        // isolate the cap from the fuse
            c.holdFuseMultiplier = 1f;
            var s = NewSim(c, ControlMode.HoldRelease);
            s.SetKey(Side.Left, true);
            TickSeconds(s, c.maxHoldSeconds + 0.1f);
            Assert.AreEqual(FailReason.HeldTooLong, s.Fail);
        }

        // =================================================================
        //  Mode A aiming
        // =================================================================

        [Test]
        public void ModeA_AimMapsToFiveSlotsWithGapsBetweenThem()
        {
            var c = Cfg();
            var s = NewSim(Plain(), ControlMode.HoldRelease);
            Assert.AreEqual(5, s.SlotCount);
            for (int i = 0; i < 5; i++)
            {
                float centre = s.SlotCentreDeg(i);
                Assert.AreEqual(i, s.SlotForAim(centre), "the centre of a robot must hit it");
                Assert.AreEqual(i, s.SlotForAim(centre - c.modeASlotHalfWidthDeg + 0.5f));
                Assert.AreEqual(i, s.SlotForAim(centre + c.modeASlotHalfWidthDeg - 0.5f));
            }
            // Between robot 0 and robot 1 there must be a real gap that reads as the floor.
            float gapCentre = (s.SlotCentreDeg(0) + s.SlotCentreDeg(1)) * 0.5f;
            Assert.AreEqual(-1, s.SlotForAim(gapCentre), "the gap between robots is the floor");
        }

        [Test]
        public void ModeA_ReleasingInAGap_IsAFloorHit()
        {
            var c = Plain();
            var s = NewSim(c, ControlMode.HoldRelease);
            s.SetKey(Side.Left, true);
            // Sweep until the arrow is genuinely between two robots, the way a player reaches a gap.
            int guard = 0;
            while (s.SlotForAim(s.AimDeg) >= 0 && s.Phase == Phase.PlayerHolding && guard++ < 100000)
                s.Tick(DT);
            Assert.AreEqual(Phase.PlayerHolding, s.Phase, "the fuse ran out before a gap was reached");
            Assert.AreEqual(-1, s.SlotForAim(s.AimDeg), "the arrow should be in a gap");
            s.SetKey(Side.Left, false);
            Assert.AreEqual(FailReason.ReleasedBetween, s.Fail);
        }

        [Test]
        public void ModeA_AQuickTapThrowsAtTheFirstRobotRatherThanKilling()
        {
            // Feel: pressing and releasing in the same instant must not be fatal.
            var s = NewSim(Plain(), ControlMode.HoldRelease);
            s.SetKey(Side.Left, true);
            s.SetKey(Side.Left, false);
            Assert.AreNotEqual(FailReason.ReleasedBetween, s.Fail,
                "a quick tap threw the bomb at thin air");
            Assert.AreEqual(1, s.Passes, "a quick tap should throw at the first robot");
        }

        [Test]
        public void ModeA_FuseBurnsFasterWhileHolding()
        {
            var c = Plain();
            var s = NewSim(c, ControlMode.HoldRelease);
            float start = s.Fuse;
            TickSeconds(s, 0.25f);                    // not holding
            float idleBurn = start - s.Fuse;

            float beforeHold = s.Fuse;
            s.SetKey(Side.Left, true);
            TickSeconds(s, 0.25f);
            float holdBurn = beforeHold - s.Fuse;

            Assert.AreEqual(c.holdFuseMultiplier, holdBurn / idleBurn, 0.05f,
                "holding must burn the fuse 1.5x faster");
        }

        [Test]
        public void ModeA_SweepStaysInsideTheArc()
        {
            var c = Plain();
            var s = NewSim(c, ControlMode.HoldRelease);
            s.SetKey(Side.Left, true);
            for (int i = 0; i < 2000; i++)
            {
                s.Tick(DT);
                if (s.Phase != Phase.PlayerHolding) break;
                Assert.That(s.AimDeg, Is.InRange(-c.modeAArcHalfDeg - 0.01f, c.modeAArcHalfDeg + 0.01f),
                    "the aim arrow must never leave the arc");
            }
        }

        [Test]
        public void ModeA_SweepSurvivesAHugeFrameWithoutLeavingTheArc()
        {
            var c = Plain();
            c.sweepStartDegPerSec = 2000f;            // absurd, to force many reflections
            var s = NewSim(c, ControlMode.HoldRelease);
            s.SetKey(Side.Left, true);
            s.Tick(0.5f);
            Assert.That(s.AimDeg, Is.InRange(-c.modeAArcHalfDeg - 0.01f, c.modeAArcHalfDeg + 0.01f));
        }

        [Test]
        public void ModeA_HasNoOnBreakGapsBecauseItAlreadyHasRealGaps()
        {
            var c = Cfg();
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f;
            for (int seed = 0; seed < 150; seed++)
            {
                var s = new ShiftSim(c, seed, ControlMode.HoldRelease);
                s.StartRun();
                for (int i = 0; i < s.Turn.SlotCount; i++)
                    Assert.AreNotEqual(Blocker.OnBreak, s.Turn.Slots[i], "Mode A uses NOPE and asleep only");
            }
        }

        // =================================================================
        //  Events and hints
        // =================================================================

        [Test]
        public void EachHintIsEmittedOnceOnly()
        {
            var c = Cfg();
            c.unlockShield = 0; c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f;
            c.blockerRampStartPass = 0;
            c.unlockAsleep = int.MaxValue; c.unlockOnBreak = int.MaxValue;
            var s = new ShiftSim(c, 7, ControlMode.LeftRight);
            s.StartRun();
            int shieldHints = 0;
            for (int pass = 0; pass < 20; pass++)
            {
                foreach (var e in s.Events)
                    if (e.Kind == SimEventKind.HintShown && e.Hint == Hint.Shield) shieldHints++;
                s.Events.Clear();
                int open = s.Turn.FirstOpenSlot();
                if (open < 0) break;
                Tap(s, open == 0 ? Side.Left : Side.Right);
                int guard = 0;
                while (s.Phase != Phase.PlayerHolding && s.Phase != Phase.GameOver && guard++ < 10000) s.Tick(DT);
                if (s.Phase == Phase.GameOver) break;
            }
            Assert.LessOrEqual(shieldHints, 1, "a hint line is shown once only");
        }

        [Test]
        public void ZoneChangeEventFiresExactlyOnceAtEachBoundary()
        {
            var c = Plain();
            var s = NewSim(c);
            var zones = new List<int>();
            for (int pass = 0; pass < 50; pass++)
            {
                foreach (var e in s.Events) if (e.Kind == SimEventKind.ZoneChange) zones.Add(e.IntValue);
                s.Events.Clear();
                Tap(s, Side.Left);
                int guard = 0;
                while (s.Phase != Phase.PlayerHolding && s.Phase != Phase.GameOver && guard++ < 10000) s.Tick(DT);
                if (s.Phase == Phase.GameOver) break;
            }
            foreach (var e in s.Events) if (e.Kind == SimEventKind.ZoneChange) zones.Add(e.IntValue);
            Assert.AreEqual(new[] { 1, 2, 3 }, zones.ToArray(), "zone 1, 2 then 3, each announced once");
        }

        [Test]
        public void CatchEventFiresForEveryNewBomb()
        {
            var s = NewSim(Plain());
            int catches = 0;
            foreach (var e in s.Events) if (e.Kind == SimEventKind.Catch) catches++;
            Assert.AreEqual(1, catches, "the first bomb is a catch too");
        }
    }
}
