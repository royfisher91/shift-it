using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using KT.ShiftIt.Sim;

namespace KT.ShiftIt.Tests
{
    /// <summary>
    /// Two jobs, in this order:
    ///
    ///  1. INSTRUMENT TESTS. Prove the bot does what it claims before any number it produces
    ///     is believed. KT_QUALITY_LESSONS lesson 6: Mole's entire "balance collapse" was bugs
    ///     in the test bot, not the game. A bot that cannot be shown to panic-throw, to aim at
    ///     the blocked side, or to hold too long on demand is not a measuring device.
    ///
    ///  2. THE BALANCE SWEEP. Roy's target: a new player usually dies between 15 and 35 passes,
    ///     a skilled player can reach 60+.
    /// </summary>
    public class BalanceTests
    {
        const int Runs = 20;          // Roy asked for 20 runs per profile
        const int BigRuns = 200;      // for distribution claims, where 20 is too few to trust

        static SimConfig Cfg() => new SimConfig();

        static List<BotRunResult> Sweep(ControlMode mode, BotSkill skill, int runs, SimConfig cfg = null)
        {
            var list = new List<BotRunResult>(runs);
            for (int i = 0; i < runs; i++)
                list.Add(SimBot.RunOne(cfg ?? Cfg(), mode, skill, seed: 10_000 + i * 7919));
            return list;
        }

        struct Stats
        {
            public float Mean, Median, Min, Max, P25, P75, MeanSeconds, MeanScore;
            public int N;
        }

        static Stats Describe(List<BotRunResult> rs)
        {
            var p = new List<int>();
            float secs = 0f, score = 0f;
            foreach (var r in rs) { p.Add(r.Passes); secs += r.Seconds; score += r.Score; }
            p.Sort();
            float sum = 0f; foreach (var v in p) sum += v;
            return new Stats
            {
                N = p.Count,
                Mean = sum / Math.Max(1, p.Count),
                Median = p[p.Count / 2],
                Min = p[0],
                Max = p[p.Count - 1],
                P25 = p[(int)(p.Count * 0.25f)],
                P75 = p[(int)(p.Count * 0.75f)],
                MeanSeconds = secs / Math.Max(1, rs.Count),
                MeanScore = score / Math.Max(1, rs.Count),
            };
        }

        static string Report(string label, List<BotRunResult> rs)
        {
            var s = Describe(rs);
            var reasons = new Dictionary<FailReason, int>();
            int capped = 0;
            foreach (var r in rs)
            {
                if (r.HitCap) capped++;
                reasons[r.Fail] = reasons.TryGetValue(r.Fail, out var n) ? n + 1 : 1;
            }
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(
                "[SHIFTIT] {0,-26} n={1,3}  passes mean={2,6:0.0} median={3,5:0} min={4,4:0} max={5,4:0} " +
                "p25={6,4:0} p75={7,4:0}  score={8,6:0.0}  run={9,5:0.0}s",
                label, s.N, s.Mean, s.Median, s.Min, s.Max, s.P25, s.P75, s.MeanScore, s.MeanSeconds));
            // Per-item, never averaged (lesson 7).
            var parts = new List<string>();
            foreach (var kv in reasons) parts.Add(kv.Key + "=" + kv.Value);
            parts.Sort();
            sb.AppendLine("[SHIFTIT]   deaths: " + string.Join(", ", parts) +
                          (capped > 0 ? "   *** " + capped + " RUN(S) HIT THE WATCHDOG CAP ***" : ""));
            return sb.ToString();
        }

        /// <summary>
        /// Goes to the Unity log as well as the test output, so Tools/unity.sh can grep the
        /// numbers out of the log rather than parsing the results XML.
        /// </summary>
        static void Log(string s)
        {
            TestContext.Out.Write(s);
            UnityEngine.Debug.Log(s.TrimEnd());
        }

        // =====================================================================
        //  1. INSTRUMENT TESTS - the bot must be shown to work first
        // =====================================================================

        [Test]
        public void Instrument_NeverThrowsBot_DiesToTheFuseOnTheFirstBomb()
        {
            for (int i = 0; i < 10; i++)
            {
                var r = SimBot.RunOne(Cfg(), ControlMode.LeftRight, BotSkill.NeverThrows, seed: i);
                Assert.IsFalse(r.HitCap, "run " + i + " never ended");
                Assert.AreEqual(FailReason.FuseRanOut, r.Fail);
                Assert.AreEqual(0, r.Passes, "a bot that never throws cannot complete a pass");
            }
        }

        [Test]
        public void Instrument_WrongSideBot_DiesToABlockerOnceBlockersExist()
        {
            int blockerDeaths = 0;
            for (int i = 0; i < 40; i++)
            {
                var r = SimBot.RunOne(Cfg(), ControlMode.LeftRight, BotSkill.WrongSide, seed: 500 + i);
                Assert.IsFalse(r.HitCap, "run " + i + " never ended");
                bool blocker = r.Fail == FailReason.ThrewAtShield || r.Fail == FailReason.ThrewAtSleeper
                            || r.Fail == FailReason.ThrewAtEmpty;
                if (blocker)
                {
                    blockerDeaths++;
                    // Read from the config, not restated as a literal: this assertion is about
                    // the RULE (nothing can die to a blocker before blockers exist), and pinning
                    // the number here just meant moving the ladder broke a test that still held.
                    Assert.GreaterOrEqual(r.Passes, Cfg().blockerRampStartPass,
                        "no blocker exists before pass " + Cfg().blockerRampStartPass +
                        ", so it cannot die to one earlier");
                }
            }
            Assert.Greater(blockerDeaths, 30, "a bot that aims at the blocked side must mostly die to one");
        }

        [Test]
        public void Instrument_PanicEarlyBot_AlmostNeverEarnsAPerfect()
        {
            // Throwing the instant it can is far outside the green band, so the combo ladder must
            // be effectively unreachable. Reported as a RATE rather than asserted at exactly zero:
            // a rare edge (a both-blocked turn that opens late, forcing a throw deep into the
            // fuse) is legitimate, and an absolute assertion would hide the number.
            int passes = 0, perfects = 0;
            for (int i = 0; i < 40; i++)
            {
                var r = SimBot.RunOne(Cfg(), ControlMode.LeftRight, BotSkill.PanicEarly, seed: 900 + i);
                passes += r.Passes; perfects += r.Perfects;
            }
            float rate = passes == 0 ? 0f : (float)perfects / passes;
            Log(string.Format("[SHIFTIT] instrument: PanicEarly band-hit rate {0:P2} " +
                              "({1} perfects in {2} passes)\n", rate, perfects, passes));
            Assert.Less(rate, 0.02f, "an instant throw should almost never land in the band");
        }

        [Test]
        public void Instrument_PerfectBot_EarnsAPerfectOnNearlyEveryPass()
        {
            int passes = 0, perfects = 0;
            for (int i = 0; i < 20; i++)
            {
                var r = SimBot.RunOne(Cfg(), ControlMode.LeftRight, BotSkill.Perfect, seed: 1300 + i);
                passes += r.Passes; perfects += r.Perfects;
            }
            float rate = passes == 0 ? 0f : (float)perfects / passes;
            Log(string.Format("[SHIFTIT] instrument: Perfect bot band-hit rate {0:P1} over {1} passes\n", rate, passes));
            Assert.Greater(rate, 0.80f, "a frame-perfect bot must hit the band almost every time");
        }

        [Test]
        public void Instrument_HoldsTooLongBot_NeverReachesEightSecondsOfHold()
        {
            // The safety requirement is absolute: nothing may hold Shift for 8 s, because that
            // is when Windows opens Filter Keys. Mode A's 5 s cap is the stated guard, but at
            // pass 0 the fuse (3.0 s burning 1.5x) runs out first - so BOTH limits are checked
            // here, and the test asserts the property rather than which limit delivered it.
            for (int i = 0; i < 10; i++)
            {
                var sim = new ShiftSim(Cfg(), i, ControlMode.HoldRelease);
                sim.StartRun();
                var bot = new SimBot(sim, BotSkill.HoldsTooLong, i);
                float worstHold = 0f, t = 0f;
                while (sim.Phase != Phase.GameOver && t < 60f)
                {
                    bot.Step(1f / 120f);
                    sim.Tick(1f / 120f);
                    if (sim.HoldTime > worstHold) worstHold = sim.HoldTime;
                    sim.Events.Clear();
                    t += 1f / 120f;
                }
                Assert.Less(worstHold, 8f, "run " + i + ": a hold reached " + worstHold + " s");
                Assert.LessOrEqual(worstHold, Cfg().maxHoldSeconds + 0.05f,
                    "run " + i + ": the 5 s cap was exceeded");
                Assert.AreNotEqual(Phase.Title, sim.Phase);
            }
        }

        [Test]
        public void Instrument_BotOnlyEverTouchesTheTwoShiftKeys()
        {
            // "A bot can only ask for what a player can ask for." The bot's sole route into the
            // simulation is SetKey(Left|Right, bool) - there is no API on ShiftSim that applies
            // a throw directly, so this is enforced by the type, and this test documents it.
            var t = typeof(ShiftSim);
            foreach (var name in new[] { "ThrowAtSlot", "Explode", "Fizzle", "CompletePass", "RollTurn" })
            {
                var m = t.GetMethod(name, System.Reflection.BindingFlags.Public
                                        | System.Reflection.BindingFlags.Instance
                                        | System.Reflection.BindingFlags.Static);
                Assert.IsNull(m, name + " must not be public: a bot could then bypass the input path");
            }
            Assert.IsNotNull(t.GetMethod("SetKey"), "SetKey is the only way in");
        }

        [Test]
        public void Instrument_NoRunEverFreezes()
        {
            // Report the SHAPE of a stuck state, not a count (KT_QUALITY_LESSONS).
            //
            // The first version of this test asserted that every run reaches game over, and the
            // Perfect bot failed it by surviving 479 passes. That is not a stuck state: it is a
            // bot with no reading error and no timing error, which this game cannot kill. Three
            // outcomes are now counted separately:
            //   ENDED   - exploded, the normal case
            //   SURVIVED- hit the watchdog cap while still completing passes
            //   FROZEN  - hit the cap having completed no pass for over 10 s. The real defect.
            var frozen = new List<string>();
            int ended = 0, survived = 0;
            float worstStall = 0f;

            foreach (var mode in new[] { ControlMode.LeftRight, ControlMode.HoldRelease })
            foreach (var skill in new[] { BotSkill.Average, BotSkill.Skilled, BotSkill.Perfect,
                                          BotSkill.PanicEarly, BotSkill.Late, BotSkill.WrongSide })
            {
                for (int i = 0; i < 25; i++)
                {
                    var r = SimBot.RunOne(Cfg(), mode, skill, seed: 7000 + i, maxSeconds: 180f);
                    if (r.LongestStall > worstStall) worstStall = r.LongestStall;
                    if (!r.HitCap) { ended++; continue; }
                    if (r.Frozen)
                        frozen.Add(mode + "/" + skill + " seed " + (7000 + i) +
                                   ": no pass for " + r.LongestStall.ToString("0.0") +
                                   "s, stopped at pass " + r.Passes);
                    else survived++;
                }
            }

            Log(string.Format("[SHIFTIT] stuck check: ended={0} survived-to-cap={1} FROZEN={2} " +
                              "worst gap between passes={3:0.00}s\n", ended, survived, frozen.Count, worstStall));
            if (frozen.Count > 0) Log("[SHIFTIT] FROZEN RUNS:\n" + string.Join("\n", frozen) + "\n");
            Assert.IsEmpty(frozen, "a run stopped making progress without ending");
            Assert.Less(worstStall, 10f, "the longest gap between two passes was " + worstStall + "s");
        }

        [Test]
        public void Instrument_RunsAreReproducibleFromTheirSeed()
        {
            for (int i = 0; i < 10; i++)
            {
                var a = SimBot.RunOne(Cfg(), ControlMode.LeftRight, BotSkill.Average, seed: 4242 + i);
                var b = SimBot.RunOne(Cfg(), ControlMode.LeftRight, BotSkill.Average, seed: 4242 + i);
                Assert.AreEqual(a.Passes, b.Passes, "same seed must give the same run");
                Assert.AreEqual(a.Score, b.Score);
                Assert.AreEqual(a.Fail, b.Fail);
            }
        }

        // =====================================================================
        //  2. THE BALANCE SWEEP - the numbers Roy asked for
        // =====================================================================

        [Test]
        public void BalanceSweep_ModeB_TwentyRunsEach()
        {
            var avg = Sweep(ControlMode.LeftRight, BotSkill.Average, Runs);
            var skl = Sweep(ControlMode.LeftRight, BotSkill.Skilled, Runs);
            Log("[SHIFTIT] ===== MODE B (Left/Right Shift), 20 runs per profile =====\n");
            Log(Report("MODE B average", avg));
            Log(Report("MODE B skilled", skl));

            var a = Describe(avg); var s = Describe(skl);
            Assert.Greater(s.Mean, a.Mean, "a skilled player must outlast an average one");
        }

        [Test]
        public void BalanceSweep_ModeA_TwentyRunsEach()
        {
            var avg = Sweep(ControlMode.HoldRelease, BotSkill.Average, Runs);
            var skl = Sweep(ControlMode.HoldRelease, BotSkill.Skilled, Runs);
            Log("[SHIFTIT] ===== MODE A (Hold & Release), 20 runs per profile =====\n");
            Log(Report("MODE A average", avg));
            Log(Report("MODE A skilled", skl));

            var a = Describe(avg); var s = Describe(skl);
            Assert.Greater(s.Mean, a.Mean, "a skilled player must outlast an average one");
        }

        [Test]
        public void ModeA_MeasuredBothWaysForTheGateDecision()
        {
            // Mode A as specified asks for two things at the same instant: the arrow on a robot AND
            // the ring's tip in the green band. Those probabilities multiply. This reports the mode
            // with Roy's original rule and with PERFECT judged on aim accuracy instead, so the
            // Stage 1 recommendation rests on numbers rather than on my opinion.
            var asSpecified = Cfg();
            var aimJudged = Cfg();
            aimJudged.modeAPerfectWithinDeg = 6f;

            foreach (var skill in new[] { BotSkill.Average, BotSkill.Skilled })
            {
                var a = Sweep(ControlMode.HoldRelease, skill, 60, asSpecified);
                var b = Sweep(ControlMode.HoldRelease, skill, 60, aimJudged);
                Log(Report("MODE A " + skill + " band-judged", a));
                Log(Report("MODE A " + skill + " aim-judged ", b));

                int pa = 0, pb = 0, sa = 0, sb = 0;
                foreach (var r in a) { pa += r.Perfects; sa += r.Passes; }
                foreach (var r in b) { pb += r.Perfects; sb += r.Passes; }
                Log(string.Format("[SHIFTIT]   PERFECT rate {0}: band-judged {1:P1}, aim-judged {2:P1}\n",
                    skill, sa == 0 ? 0f : (float)pa / sa, sb == 0 ? 0f : (float)pb / sb));
            }
        }

        [Test]
        public void ModeA_SweptAcrossRobotWidths_ToFindWhatWouldRescueIt()
        {
            // Mode A kills an average player almost entirely through ReleasedBetween - letting go
            // in a gap. The PERFECT band is NOT the problem: Mode A's band-hit rate is already
            // about 89% for an average bot. So the question is whether geometry can rescue it.
            //
            // Five robots over a 140 degree arc means each owns 28 degrees. At a half-width of 12
            // that is 24 degrees of robot and 4 of gap, so 14.3% of the arc is lethal floor. At 14
            // the robots touch and there are no gaps at all - which removes the "release between
            // robots = the floor" rule entirely. This reports the range so the trade is visible.
            Log("[SHIFTIT] ===== MODE A: robot half-width sweep (5 robots, 140 deg arc) =====\n");
            foreach (float half in new[] { 12f, 13f, 14f })
            {
                var c = Cfg();
                c.modeASlotHalfWidthDeg = half;
                float gap = (c.modeAArcHalfDeg * 2f) / c.modeASlots - half * 2f;
                foreach (var skill in new[] { BotSkill.Average, BotSkill.Skilled })
                {
                    var rs = Sweep(ControlMode.HoldRelease, skill, 60, c);
                    Log(Report(string.Format("MODE A {0} half={1:0.#} gap={2:0.#}", skill, half, gap), rs));
                }
            }
        }

        /// <summary>
        /// Stage 2 asked for a faster ramp with more blockers while staying inside the targets.
        /// Rather than guess, this sweeps the three dials that control blocker pressure and prints
        /// the two numbers that decide it: where a new player dies, and whether a skilled player can
        /// still reach 60. Run it by name; it is not part of the normal suite.
        ///
        ///   Tools/unity.sh tune -runTests -testPlatform EditMode -testFilter ".*TuningSweep.*"
        /// </summary>
        [Test, Explicit]
        public void TuningSweep_FindsTheFasterRamp()
        {
            Log("[SHIFTIT] ===== blocker-pressure sweep: target is average median 15-35, skilled max 60+ =====\n");
            Log("[SHIFTIT]  start   end  rampEnd |  avg median  avg mean | skilled mean  skilled max | verdict\n");

            foreach (float start in new[] { 0.35f, 0.45f, 0.55f })
            foreach (float end in new[] { 0.85f, 0.92f, 0.98f })
            foreach (int rampEnd in new[] { 40, 32, 26 })
            {
                var c = Cfg();
                c.blockerChanceAtUnlock = start;
                c.blockerChanceAtEnd = end;
                c.blockerRampEndPass = rampEnd;

                var avg = Describe(Sweep(ControlMode.LeftRight, BotSkill.Average, 80, c));
                var skl = Describe(Sweep(ControlMode.LeftRight, BotSkill.Skilled, 80, c));

                bool inBand = avg.Median >= 15f && avg.Median <= 35f;
                bool ceiling = skl.Max >= 60f;
                string verdict = inBand && ceiling ? "OK"
                               : !inBand ? (avg.Median < 15f ? "too hard" : "too easy")
                               : "skilled ceiling too low";

                Log(string.Format("[SHIFTIT]   {0:0.00}  {1:0.00}      {2,2} |  {3,10:0} {4,9:0.0} | {5,12:0.0} {6,12:0} | {7}\n",
                    start, end, rampEnd, avg.Median, avg.Mean, skl.Mean, skl.Max, verdict));
            }
        }

        /// <summary>
        /// Second tuning sweep. The first one showed that blocker FREQUENCY is a weak dial: pushing
        /// it from 0.35 to 0.98 moved an average player's median only from 26 to 22, because what
        /// kills that player is failing to READ a turn, not how often a turn has something in it.
        ///
        /// So this sweeps the dials that change what a turn ASKS of the player: how often the keys
        /// are mirrored, how often both sides are blocked, and how fast the PERFECT band narrows.
        /// Blocker pressure is pinned at the top of the first sweep's useful range.
        ///
        ///   Tools/unity.sh tune -runTests -testPlatform EditMode -testFilter ".*TuningSweep2.*"
        /// </summary>
        [Test, Explicit]
        public void TuningSweep2_TheDialsThatChangeWhatATurnAsks()
        {
            Log("[SHIFTIT] ===== twist-pressure sweep (blockers pinned at 0.55 -> 0.98 by pass 26) =====\n");
            Log("[SHIFTIT]  mirror  both  bandOver |  avg median  avg mean | skilled mean  skilled max | verdict\n");

            foreach (float mirror in new[] { 0.25f, 0.35f, 0.45f })
            foreach (float both in new[] { 0.25f, 0.35f, 0.45f })
            foreach (float bandOver in new[] { 40f, 28f, 20f })
            {
                var c = Cfg();
                c.blockerChanceAtUnlock = 0.55f;
                c.blockerChanceAtEnd = 0.98f;
                c.blockerRampEndPass = 26;
                c.chanceMirrored = mirror;
                c.chanceBothBlocked = both;
                c.bandNarrowOverPasses = bandOver;

                var avg = Describe(Sweep(ControlMode.LeftRight, BotSkill.Average, 80, c));
                var skl = Describe(Sweep(ControlMode.LeftRight, BotSkill.Skilled, 80, c));

                bool inBand = avg.Median >= 15f && avg.Median <= 35f;
                bool ceiling = skl.Max >= 60f;
                string verdict = inBand && ceiling ? "OK"
                               : !inBand ? (avg.Median < 15f ? "TOO HARD" : "too easy")
                               : "skilled ceiling too low";

                Log(string.Format("[SHIFTIT]   {0:0.00}  {1:0.00}        {2,2:0} |  {3,10:0} {4,9:0.0} | {5,12:0.0} {6,12:0} | {7}\n",
                    mirror, both, bandOver, avg.Median, avg.Mean, skl.Mean, skl.Max, verdict));
            }
        }

        [Test]
        public void BalanceTarget_AverageDiesBetween15And35Passes()
        {
            var rs = Sweep(ControlMode.LeftRight, BotSkill.Average, BigRuns);
            var s = Describe(rs);
            Log(Report("TARGET CHECK average (n=200)", rs));
            Assert.That(s.Median, Is.InRange(15f, 35f),
                "Roy's target: a new player usually dies between 15 and 35 passes. Median was " + s.Median);
        }

        [Test]
        public void BalanceTarget_SkilledCanReachSixtyPasses()
        {
            var rs = Sweep(ControlMode.LeftRight, BotSkill.Skilled, BigRuns);
            var s = Describe(rs);
            Log(Report("TARGET CHECK skilled (n=200)", rs));
            Assert.GreaterOrEqual(s.Max, 60f,
                "Roy's target: a skilled player can reach 60+. Best of 200 runs was " + s.Max);
        }

        /// <summary>
        /// Roy played the Stage 3 build, reached 115 passes and asked for the bottom half of his
        /// own band: a new-player median of about 15-18, with a skilled player still reaching 60+.
        ///
        /// This is tighter than BalanceTarget_AverageDiesBetween15And35Passes on purpose. That test
        /// guards the band the game must never leave; this one guards the point inside it that was
        /// actually chosen, so a later change that drifts back towards 20 fails here and says so
        /// instead of passing the wider check and quietly undoing the Stage 4 retune.
        /// </summary>
        [Test]
        public void BalanceTarget_Stage4_AverageMedianIsFifteenToEighteen()
        {
            var rs = Sweep(ControlMode.LeftRight, BotSkill.Average, BigRuns);
            var s = Describe(rs);
            Log(Report("STAGE 4 TARGET average (n=200)", rs));
            Assert.That(s.Median, Is.InRange(15f, 18f),
                "Stage 4 target: a new player's median run is 15-18 passes. Median was " + s.Median);
        }

        /// <summary>
        /// The ceiling stated as a REACH rather than a best-of. A single lucky maximum can clear 60
        /// while the mode is in practice unreachable, so this asserts the share of runs that get
        /// there - which is what "a skilled player can still reach 60+" actually means.
        /// </summary>
        [Test]
        public void BalanceTarget_Stage4_SkilledReachesSixtyOftenEnough()
        {
            var rs = Sweep(ControlMode.LeftRight, BotSkill.Skilled, BigRuns);
            int reached = 0;
            foreach (var r in rs) if (r.Passes >= 60) reached++;
            float pct = 100f * reached / rs.Count;
            Log(string.Format("[SHIFTIT] skilled reached 60+ in {0}/{1} runs ({2:0.0}%)\n",
                reached, rs.Count, pct));
            Assert.GreaterOrEqual(pct, 10f,
                "a skilled player must reach 60+ in at least 1 run in 10; it was " + pct + "%");
        }

        [Test]
        public void BalanceSweep_RunLengthIsBetweenThirtySecondsAndThreeMinutes()
        {
            // Roy's target run length. Measured on the average profile, which is the typical run.
            var rs = Sweep(ControlMode.LeftRight, BotSkill.Average, BigRuns);
            var s = Describe(rs);
            Log(string.Format("[SHIFTIT] run length: mean {0:0.0}s over {1} average runs\n", s.MeanSeconds, s.N));
            Assert.That(s.MeanSeconds, Is.InRange(30f, 180f),
                "target run length is 30 s to 3 minutes; mean was " + s.MeanSeconds + "s");
        }

        [Test]
        public void BalanceSweep_DifficultyCurveIsMonotonic()
        {
            // Each profile in order of skill must do at least as well as the one below it.
            var order = new[] { BotSkill.PanicEarly, BotSkill.Average, BotSkill.Skilled, BotSkill.Perfect };
            var means = new List<float>();
            foreach (var sk in order)
            {
                var s = Describe(Sweep(ControlMode.LeftRight, sk, 60));
                means.Add(s.Mean);
                Log(string.Format("[SHIFTIT] curve: {0,-11} mean passes {1,6:0.0}\n", sk, s.Mean));
            }
            // PanicEarly throws instantly so it never dies to the fuse - it is allowed to beat
            // Average on survival. What must hold is that skill beats averageness.
            Assert.Greater(means[2], means[1], "Skilled must beat Average");
            Assert.GreaterOrEqual(means[3], means[2], "Perfect must at least match Skilled");
        }

        [Test]
        public void SafeStrategy_PanicThrowingIsNotAFreeRide()
        {
            // The design worry: with no penalty for an early throw, is "throw instantly, always"
            // a dominant strategy? It should survive a while but score badly.
            var panic = Describe(Sweep(ControlMode.LeftRight, BotSkill.PanicEarly, BigRuns));
            var skilled = Describe(Sweep(ControlMode.LeftRight, BotSkill.Skilled, BigRuns));
            Log(string.Format("[SHIFTIT] panic-throw: mean passes {0:0.0}, mean score {1:0.0}\n",
                panic.Mean, panic.MeanScore));
            Log(string.Format("[SHIFTIT] skilled:     mean passes {0:0.0}, mean score {1:0.0}\n",
                skilled.Mean, skilled.MeanScore));
            Assert.Less(panic.MeanScore, skilled.MeanScore,
                "panic-throwing must score worse than playing the band, or the fuse is pointless");
        }

        [Test]
        public void TwistsActuallyOccurAcrossALongRun()
        {
            // A twist that is configured but never fires is the classic KT failure
            // (quality lessons A1/A2: configured is not on). Prove each one reaches the player.
            var cfg = Cfg();
            var seenBomb = new HashSet<BombKind>();
            var seenBlocker = new HashSet<Blocker>();
            bool mirrored = false, both = false, swap = false, opened = false;
            int maxPass = 0;

            for (int seed = 0; seed < 50; seed++)
            {
                var sim = new ShiftSim(cfg, seed, ControlMode.LeftRight);
                var all = new Hint[(int)Hint.Count];
                for (int i = 0; i < all.Length; i++) all[i] = (Hint)i;
                sim.RestoreShownHints(all);
                var bot = new SimBot(sim, BotSkill.Perfect, seed);
                sim.StartRun();
                float t = 0f;
                while (sim.Phase != Phase.GameOver && t < 240f)
                {
                    bot.Step(1f / 120f);
                    sim.Tick(1f / 120f);
                    t += 1f / 120f;
                    seenBomb.Add(sim.Bomb);
                    for (int i = 0; i < sim.Turn.SlotCount; i++)
                        if (sim.Turn.Slots[i] != Blocker.None) seenBlocker.Add(sim.Turn.Slots[i]);
                    if (sim.Turn.Mirrored) mirrored = true;
                    if (sim.Turn.BothBlocked) both = true;
                    if (sim.Turn.CanSwap) swap = true;
                    foreach (var e in sim.Events) if (e.Kind == SimEventKind.Open) opened = true;
                    sim.Events.Clear();
                    if (sim.Passes > maxPass) maxPass = sim.Passes;
                }
            }

            Log("[SHIFTIT] twists reached: bombs=" + string.Join("/", seenBomb) +
                " blockers=" + string.Join("/", seenBlocker) +
                " mirrored=" + mirrored + " bothBlocked=" + both + " opened=" + opened +
                " swap=" + swap + " deepest pass=" + maxPass + "\n");

            Assert.IsTrue(seenBomb.Contains(BombKind.Hot), "RED HOT bombs never appeared");
            Assert.IsTrue(seenBomb.Contains(BombKind.Dud), "DUD bombs never appeared");
            Assert.IsTrue(seenBlocker.Contains(Blocker.Shield), "SHIELD never appeared");
            Assert.IsTrue(seenBlocker.Contains(Blocker.Asleep), "SLEEPING robot never appeared");
            Assert.IsTrue(seenBlocker.Contains(Blocker.OnBreak), "ON BREAK never appeared");
            Assert.IsTrue(mirrored, "MIRRORED turns never appeared");
            Assert.IsTrue(both, "BOTH SIDES BLOCKED never appeared");
            Assert.IsTrue(opened, "a both-blocked turn never opened");
            Assert.IsTrue(swap, "SWAP never appeared");
        }
    }
}
