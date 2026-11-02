using System;

namespace KT.ShiftIt.Sim
{
    /// <summary>
    /// How well the simulated player plays. "Average" and "Skilled" are the two Roy asked to be
    /// reported; the rest are deliberate single-behaviour bots for the automated play tests.
    /// </summary>
    public enum BotSkill
    {
        /// <summary>A new player: slow to react, loose timing, confused by mirrors and swaps.</summary>
        Average = 0,
        /// <summary>Someone who has learnt the game: tight timing, reads the tells.</summary>
        Skilled = 1,
        /// <summary>Throws the instant it can - and therefore mostly GUESSES which side.</summary>
        PanicEarly = 2,
        /// <summary>Frame-perfect timing and superhuman reading. The ceiling, not a human.</summary>
        Perfect = 3,
        /// <summary>Waits until the fuse is nearly gone. Should die to FuseRanOut often.</summary>
        Late = 4,
        /// <summary>Never throws at all. Must die to FuseRanOut on pass 0.</summary>
        NeverThrows = 5,
        /// <summary>Always aims at a blocked slot when one exists. Must die the first time one appears.</summary>
        WrongSide = 6,
        /// <summary>Mode A: holds past the cap. Used to prove no hold can reach 8 s.</summary>
        HoldsTooLong = 7,
    }

    /// <summary>The tunable behaviour of one bot profile. Test-instrument data, not balance data.</summary>
    public sealed class BotProfile
    {
        public BotSkill Skill;

        /// <summary>Seconds before the bot can act on a new catch at all.</summary>
        public float ReactionSeconds;

        /// <summary>
        /// How long this player needs to LOOK at a turn before they reliably know what it is:
        /// which side is blocked, whether the keys are mirrored, whether the bomb is a dud.
        ///
        /// This is the single most important number in the bot, and it was missing from the first
        /// version. Without it a bot that threw instantly also read perfectly, which made
        /// panic-throwing an immortal strategy and produced a "finding" about the game that was
        /// really a bug in the measuring device (KT_QUALITY_LESSONS lesson 6). A real player who
        /// throws in 0.1 s has not read anything and is guessing.
        /// </summary>
        public float ReadSeconds;

        /// <summary>Standard deviation of release-timing error, seconds.</summary>
        public float TimingSigma;
        /// <summary>Baseline chance of a mistake EVEN when they had time to read.</summary>
        public float MisreadChance;
        /// <summary>Chance of forgetting a MIRRORED turn despite having read it.</summary>
        public float MirrorConfusionChance;
        /// <summary>Chance of not re-reading after a SWAP.</summary>
        public float SwapMissChance;
        /// <summary>Chance of throwing a dud they had correctly identified.</summary>
        public float DudMistakeChance;
        /// <summary>
        /// Mode A: standard deviation of WHEN the player lets go, in seconds.
        ///
        /// The first version of this bot carried a fixed angular error in degrees, which was
        /// physically wrong and quietly flattered the design: a human's error is in the timing of
        /// the release, so the ANGULAR error is jitter x sweep speed and therefore grows as the
        /// sweep accelerates from 140 to 340 deg/s. Modelling it as a constant angle made a fast
        /// sweep look no harder than a slow one, which is the opposite of the truth.
        /// </summary>
        public float ReleaseJitterSeconds;
        /// <summary>Where in the band it aims: 0 = the late edge, 1 = the early edge.</summary>
        public float BandBias;

        public static BotProfile For(BotSkill s)
        {
            switch (s)
            {
                case BotSkill.Average:
                    return new BotProfile
                    {
                        Skill = s, ReactionSeconds = 0.28f, ReadSeconds = 0.52f, TimingSigma = 0.090f,
                        MisreadChance = 0.05f, MirrorConfusionChance = 0.35f,
                        SwapMissChance = 0.50f, DudMistakeChance = 0.12f,
                        ReleaseJitterSeconds = 0.055f, BandBias = 0.5f,
                    };
                case BotSkill.Skilled:
                    return new BotProfile
                    {
                        Skill = s, ReactionSeconds = 0.16f, ReadSeconds = 0.30f, TimingSigma = 0.035f,
                        MisreadChance = 0.012f, MirrorConfusionChance = 0.08f,
                        SwapMissChance = 0.15f, DudMistakeChance = 0.03f,
                        ReleaseJitterSeconds = 0.025f, BandBias = 0.5f,
                    };
                case BotSkill.Perfect:
                    return new BotProfile
                    {
                        Skill = s, ReactionSeconds = 0.02f, ReadSeconds = 0.02f, TimingSigma = 0f,
                        MisreadChance = 0f, MirrorConfusionChance = 0f,
                        SwapMissChance = 0f, DudMistakeChance = 0f,
                        ReleaseJitterSeconds = 0f, BandBias = 0.5f,
                    };
                case BotSkill.PanicEarly:
                    // Throws as early as it physically can. ReadSeconds is a normal human value,
                    // so throwing that early means it has NOT read the turn and is guessing.
                    return new BotProfile
                    {
                        Skill = s, ReactionSeconds = 0.12f, ReadSeconds = 0.45f, TimingSigma = 0f,
                        MisreadChance = 0.02f, MirrorConfusionChance = 0.3f,
                        SwapMissChance = 0.6f, DudMistakeChance = 0.05f,
                        ReleaseJitterSeconds = 0.050f, BandBias = 1f,
                    };
                case BotSkill.Late:
                    return new BotProfile
                    {
                        Skill = s, ReactionSeconds = 0.12f, ReadSeconds = 0.30f, TimingSigma = 0.01f,
                        MisreadChance = 0.01f, MirrorConfusionChance = 0.08f,
                        SwapMissChance = 0.15f, DudMistakeChance = 0.03f,
                        ReleaseJitterSeconds = 0.030f, BandBias = 0f,
                    };
                default:
                    return new BotProfile
                    {
                        Skill = s, ReactionSeconds = 0.15f, ReadSeconds = 0.30f, TimingSigma = 0.02f,
                        MisreadChance = 0f, MirrorConfusionChance = 0f,
                        SwapMissChance = 0f, DudMistakeChance = 0f,
                        ReleaseJitterSeconds = 0.030f, BandBias = 0.5f,
                    };
            }
        }
    }

    /// <summary>The result of one simulated run.</summary>
    public struct BotRunResult
    {
        public int Passes;
        public int Score;
        public int Perfects;
        public int MaxCombo;
        public FailReason Fail;
        public float Seconds;
        /// <summary>Hit the watchdog cap rather than ending in an explosion.</summary>
        public bool HitCap;
        /// <summary>
        /// Hit the cap WITHOUT completing a pass for a long stretch: a genuine stuck state.
        /// Distinguished from HitCap because a bot that is still scoring at the cap is
        /// surviving, not wedged - and reporting those as one number hides the real defect
        /// (KT_QUALITY_LESSONS: report the SHAPE of a stuck state, not the count).
        /// </summary>
        public bool Frozen;
        /// <summary>Seconds the run spent without completing a pass, at its worst.</summary>
        public float LongestStall;
    }

    /// <summary>
    /// Drives a ShiftSim through the same two-key input path a player uses: it may only call
    /// SetKey. It can never write a throw straight into the simulation.
    ///
    /// Its randomness comes from its OWN Random, never the simulation's, so changing the bot
    /// cannot change which turns the game generates for a given seed.
    /// </summary>
    public sealed class SimBot
    {
        public readonly BotProfile P;
        readonly Random _rng;
        readonly ShiftSim _sim;

        // per-turn plan
        bool _planned;
        int _targetSlot;
        float _throwAtFuse;
        bool _holdDud;
        float _elapsedInTurn;
        bool _pressedThisTurn;
        bool _waitingForOpen;
        bool _sawSwap;
        bool _forgotMirror;
        bool _committed;

        public SimBot(ShiftSim sim, BotSkill skill, int seed)
        {
            _sim = sim;
            P = BotProfile.For(skill);
            _rng = new Random(seed);
        }

        public static BotRunResult RunOne(SimConfig cfg, ControlMode mode, BotSkill skill,
                                          int seed, float dt = 1f / 60f, float maxSeconds = 900f)
        {
            var sim = new ShiftSim(cfg, seed, mode);
            // Hints count as already seen, so a balance run measures play and not reading time.
            var all = new Hint[(int)Hint.Count];
            for (int i = 0; i < all.Length; i++) all[i] = (Hint)i;
            sim.RestoreShownHints(all);

            var bot = new SimBot(sim, skill, seed ^ 0x5bf03635);
            sim.StartRun();

            int perfects = 0, maxCombo = 0, lastPasses = 0;
            float t = 0f, lastPassAt = 0f, longestStall = 0f;

            while (sim.Phase != Phase.GameOver && t < maxSeconds)
            {
                bot.Step(dt);
                sim.Tick(dt);
                t += dt;
                for (int i = 0; i < sim.Events.Count; i++)
                {
                    var e = sim.Events[i];
                    if (e.Kind == SimEventKind.Throw && e.Quality == ThrowQuality.Perfect) perfects++;
                }
                sim.Events.Clear();
                if (sim.Combo > maxCombo) maxCombo = sim.Combo;
                if (sim.Passes != lastPasses)
                {
                    lastPasses = sim.Passes;
                    float stall = t - lastPassAt;
                    if (stall > longestStall) longestStall = stall;
                    lastPassAt = t;
                }
            }

            bool hitCap = sim.Phase != Phase.GameOver;
            float tailStall = t - lastPassAt;
            if (tailStall > longestStall) longestStall = tailStall;

            return new BotRunResult
            {
                Passes = sim.Passes, Score = sim.Score, Perfects = perfects,
                MaxCombo = maxCombo, Fail = sim.Fail, Seconds = t,
                HitCap = hitCap,
                // A run is FROZEN only if it stopped making progress. Ten seconds is many times
                // the longest legitimate gap between passes (fuse 3 s + return 0.55 s + flights).
                Frozen = hitCap && tailStall > 10f,
                LongestStall = longestStall,
            };
        }

        /// <summary>One bot decision step. Must be called before sim.Tick for the same dt.</summary>
        public void Step(float dt)
        {
            if (_sim.Phase == Phase.Title || _sim.Phase == Phase.GameOver)
            {
                _sim.SetKey(Side.Left, true);
                _sim.SetKey(Side.Left, false);
                _planned = false;
                return;
            }

            if (_sim.Phase != Phase.PlayerHolding)
            {
                _sim.ReleaseAll();
                _planned = false;
                return;
            }

            if (!_planned) PlanTurn();
            _elapsedInTurn += dt;

            // A swap may have moved the block since the plan was made.
            if (_sim.Turn.Swapped && !_sawSwap)
            {
                _sawSwap = true;
                if (_rng.NextDouble() < P.SwapMissChance) _committed = true;   // did not notice
            }

            if (P.Skill == BotSkill.NeverThrows) return;
            if (_holdDud) return;
            if (_elapsedInTurn < P.ReactionSeconds) return;

            if (_sim.Mode == ControlMode.LeftRight) StepModeB();
            else StepModeA(dt);
        }

        /// <summary>
        /// Did this player actually take in what this turn is? Rolled ONCE per turn at the moment
        /// of commitment, against how long they had been looking.
        /// </summary>
        bool DidRead()
        {
            float need = P.ReadSeconds;
            float frac = need <= 0f ? 1f : Clamp01(_elapsedInTurn / need);
            if (_rng.NextDouble() >= frac) return false;              // ran out of looking time
            return _rng.NextDouble() >= P.MisreadChance;              // or just slipped
        }

        void StepModeB()
        {
            if (_sim.Fuse > _throwAtFuse) return;

            bool read = DidRead();

            // A dud: holding is only correct if they SAW that it was a dud.
            if (_sim.Turn.Bomb == BombKind.Dud)
            {
                if (read && _rng.NextDouble() >= P.DudMistakeChance)
                {
                    _holdDud = true;
                    return;
                }
                // otherwise it throws the dud, which is fatal - exactly what a rushed player does
            }

            // Both sides blocked: waiting for the OPEN is only possible if they read the turn.
            if (_sim.Turn.BothBlocked && !_sim.Turn.Opened)
            {
                if (read)
                {
                    _waitingForOpen = true;
                    return;                     // wait; the open will come
                }
                // did not read it: throws into a shield
            }
            _waitingForOpen = false;

            int slot = ResolveTargetSlot(read);

            // They only act on a MIRRORED turn if they read it AND did not forget it. Forgetting
            // is rolled once in PlanTurn so it cannot be re-rolled frame by frame.
            bool mirrored = _sim.Turn.Mirrored && read && !_forgotMirror;

            int logical = mirrored ? 1 - slot : slot;
            Side key = logical == 0 ? Side.Left : Side.Right;
            _sim.SetKey(key, true);
            _sim.SetKey(key, false);
            _planned = false;
        }

        void StepModeA(float dt)
        {
            if (P.Skill == BotSkill.HoldsTooLong)
            {
                if (!_pressedThisTurn) { _pressedThisTurn = true; _sim.SetKey(Side.Left, true); }
                return;
            }

            // A dud in Mode A: simply never press.
            if (_sim.Turn.Bomb == BombKind.Dud && !_sim.Holding)
            {
                if (DidRead() && _rng.NextDouble() >= P.DudMistakeChance) { _holdDud = true; return; }
            }

            float sweep = _sim.Cfg.SweepFor(_sim.Passes);
            // Release-timing jitter becomes an ANGLE by way of the sweep speed, so the same human
            // hand is far less accurate at 340 deg/s than at 140.
            float aimErrorDeg = (float)Gauss(0, P.ReleaseJitterSeconds * sweep);
            float target = _sim.SlotCentreDeg(_targetSlot) + aimErrorDeg;
            // The arrow starts ON robot 0, not at the edge of the arc, so the distance it has to
            // travel is measured from there.
            float travel = target - _sim.SlotCentreDeg(0);
            if (travel < 0f) travel = -travel;
            float toTarget = travel / Math.Max(1f, sweep);

            if (!_sim.Holding)
            {
                // Pressing IS the commitment in Mode A: the target is chosen here, with whatever
                // reading time has elapsed so far - which is why Mode A punishes rushing harder.
                float fuseBeforePress = _sim.Fuse - _throwAtFuse - toTarget * _sim.Cfg.holdFuseMultiplier;
                if (fuseBeforePress <= 0f)
                {
                    bool read = DidRead();
                    _targetSlot = ResolveTargetSlot(read);
                    _pressedThisTurn = true;
                    _sim.SetKey(Side.Left, true);
                }
                return;
            }

            if (_sim.AimDir > 0 ? _sim.AimDeg >= target : _sim.AimDeg <= target)
            {
                _sim.SetKey(Side.Left, false);
                _planned = false;
            }
        }

        void PlanTurn()
        {
            _planned = true;
            _elapsedInTurn = 0f;
            _pressedThisTurn = false;
            _committed = false;
            _sawSwap = _sim.Turn.Swapped;
            _forgotMirror = _rng.NextDouble() < P.MirrorConfusionChance;
            _holdDud = false;
            _waitingForOpen = false;
            _targetSlot = PickOpen(_sim.Turn.SlotCount);
            ChooseTiming();
        }

        /// <summary>Which slot this player is about to aim at, given whether they read the turn.</summary>
        int ResolveTargetSlot(bool read)
        {
            int n = _sim.Turn.SlotCount;

            if (P.Skill == BotSkill.WrongSide)
            {
                for (int i = 0; i < n; i++) if (_sim.Turn.Slots[i] != Blocker.None) return i;
                return PickOpen(n);
            }

            // Did not take the turn in: guess. In Mode B that is a coin flip; in Mode A it is a
            // guess among five, which is why Mode A is less forgiving of a rushed throw.
            if (!read) return _rng.Next(n);
            return PickOpen(n);
        }

        void ChooseTiming()
        {
            float lo = _sim.BandLow, hi = _sim.BandHigh;
            float frac = lo + (hi - lo) * Clamp01(P.BandBias);
            if (P.Skill == BotSkill.Late) frac = lo * 0.25f;
            if (P.Skill == BotSkill.PanicEarly) frac = 0.995f;

            _throwAtFuse = _sim.FuseMax * frac + (float)Gauss(0, P.TimingSigma);
            if (_throwAtFuse < 0f) _throwAtFuse = 0f;
            if (_throwAtFuse > _sim.FuseMax) _throwAtFuse = _sim.FuseMax;
        }

        int PickOpen(int n)
        {
            int best = -1; float bestCost = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (_sim.Turn.Slots[i] != Blocker.None) continue;
                float cost = n == 2 ? 0f : Math.Abs(i - (n - 1) * 0.5f);
                if (cost < bestCost) { bestCost = cost; best = i; }
            }
            if (best >= 0) return best;
            return _rng.Next(n);
        }

        double Gauss(double mean, double sigma)
        {
            if (sigma <= 0) return mean;
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = _rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + sigma * z;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
