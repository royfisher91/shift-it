using System;
using System.Collections.Generic;

namespace KT.ShiftIt.Sim
{
    /// <summary>
    /// The whole game, as a deterministic pure-C# state machine.
    ///
    /// Drive it with Tick(dt) plus SetKey(side, down). Drain Events each frame to drive
    /// presentation. Nothing here knows that Unity exists, which is what lets the balance
    /// sweep run tens of thousands of simulated passes in an EditMode test in under a second
    /// (quality lesson 6: test the bot before trusting any balance number).
    ///
    /// One clock owns time (pattern 01 item 4): this one. Nothing else accumulates game time.
    /// </summary>
    public sealed class ShiftSim
    {
        public SimConfig Cfg { get; private set; }
        public ControlMode Mode;

        readonly Random _rng;                       // gameplay stream only - never used for cosmetics
        public readonly List<SimEvent> Events = new List<SimEvent>(32);

        // ---- run state ----
        public Phase Phase { get; private set; } = Phase.Title;
        public int Score { get; private set; }
        public int Passes { get; private set; }
        public int Combo { get; private set; }
        public int Best;
        public int Zone { get; private set; }
        public FailReason Fail { get; private set; }

        // ---- bomb / fuse ----
        public readonly Turn Turn = new Turn();
        public float Fuse { get; private set; }          // seconds remaining
        public float FuseMax { get; private set; }       // seconds this bomb started with
        public float FuseFraction => FuseMax <= 0f ? 0f : Fuse / FuseMax;
        public BombKind Bomb => Turn.Bomb;

        // ---- Mode A aim ----
        public float AimDeg { get; private set; }
        public int AimDir { get; private set; } = 1;
        public bool Holding { get; private set; }
        public float HoldTime { get; private set; }

        // ---- phase timing (presentation reads these to lerp the bomb) ----
        public float PhaseTime { get; private set; }
        public float PhaseLength { get; private set; }
        public float PhaseT => PhaseLength <= 0f ? 1f : Clamp01(PhaseTime / PhaseLength);
        /// <summary>Which slot the bomb is travelling to / coming back from.</summary>
        public int ActiveSlot { get; private set; } = -1;

        // ---- band for this bomb ----
        public float BandLow { get; private set; }
        public float BandHigh { get; private set; }

        /// <summary>Mode A: how far off a robot's centre the last release was, in degrees.</summary>
        public float LastAimErrorDeg { get; private set; }

        // ---- input edge tracking ----
        bool _leftDown, _rightDown;
        float _leftHeld, _rightHeld;
        bool _warnedHoldThisPress;
        float _gameOverLock;

        /// <summary>
        /// True while the short post-explosion lockout is still running, so a Shift press that was
        /// part of the death cannot instantly restart the run. Exposed read-only because the
        /// browser harness needs to know when a retry press will actually be accepted.
        /// </summary>
        public bool RetryLocked => _gameOverLock > 0f;

        readonly bool[] _hintShown = new bool[(int)Hint.Count];

        public ShiftSim(SimConfig cfg, int seed, ControlMode mode)
        {
            Cfg = cfg ?? new SimConfig();
            Mode = mode;
            _rng = new Random(seed);
        }

        /// <summary>Hints are shown once per player, not once per run, so retries do not nag.</summary>
        public void RestoreShownHints(IEnumerable<Hint> shown)
        {
            if (shown == null) return;
            foreach (var h in shown)
            {
                int i = (int)h;
                if (i > 0 && i < _hintShown.Length) _hintShown[i] = true;
            }
        }

        public bool HintWasShown(Hint h) => _hintShown[(int)h];
        public int SlotCount => Mode == ControlMode.LeftRight ? 2 : Cfg.modeASlots;

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        public void StartRun()
        {
            Score = 0; Passes = 0; Combo = 0; Zone = 0; Fail = FailReason.None;
            _leftDown = _rightDown = false; _leftHeld = _rightHeld = 0f;
            Holding = false; HoldTime = 0f; _gameOverLock = 0f;
            Emit(SimEventKind.Started);
            BeginCatch(firstCatch: true);
        }

        public void GoToTitle()
        {
            Phase = Phase.Title;
            Holding = false;
            _leftDown = _rightDown = false;
        }

        // =====================================================================
        //  Input - edge detected, so a key LATCHED by Windows Sticky Keys cannot
        //  machine-gun throws, and a held key is one throw not many.
        // =====================================================================

        public void SetKey(Side side, bool down)
        {
            if (side == Side.None) return;
            bool was = side == Side.Left ? _leftDown : _rightDown;
            if (side == Side.Left) _leftDown = down; else _rightDown = down;

            if (down && !was)
            {
                if (side == Side.Left) _leftHeld = 0f; else _rightHeld = 0f;
                _warnedHoldThisPress = false;
                OnPress(side);
            }
            else if (!down && was)
            {
                OnRelease(side);
            }
        }

        public void ReleaseAll()
        {
            if (_leftDown) SetKey(Side.Left, false);
            if (_rightDown) SetKey(Side.Right, false);
        }

        void OnPress(Side side)
        {
            switch (Phase)
            {
                case Phase.Title:
                    StartRun();
                    return;

                case Phase.GameOver:
                    if (_gameOverLock <= 0f) { StartRun(); }
                    return;

                case Phase.PlayerHolding:
                    if (Mode == ControlMode.LeftRight)
                    {
                        ThrowAtSlot(SlotForKey(side));
                    }
                    else if (!Holding)
                    {
                        Holding = true;
                        HoldTime = 0f;
                        // Start ON the first robot, not at the edge of the arc. The edge is a gap,
                        // so starting there meant a nervous press-and-release in the same instant
                        // threw the bomb at thin air and killed the player for touching the key.
                        // A tap now throws at the leftmost robot, which is a real decision.
                        AimDeg = SlotCentreDeg(0);
                        AimDir = 1;
                        ShowHint(Hint.FirstCatchModeA_Release);
                    }
                    return;
            }
        }

        void OnRelease(Side side)
        {
            if (Phase != Phase.PlayerHolding || Mode != ControlMode.HoldRelease || !Holding) return;
            // Only the last key up ends the hold, if the other is still down.
            if (_leftDown || _rightDown) return;
            Holding = false;
            ThrowAtSlot(SlotForAim(AimDeg));
        }

        /// <summary>Mode B key -> slot, honouring a MIRRORED turn.</summary>
        public int SlotForKey(Side side)
        {
            int slot = side == Side.Left ? 0 : 1;
            if (Turn.Mirrored) slot = 1 - slot;
            return slot;
        }

        /// <summary>Mode A aim angle -> slot, or -1 for a gap between robots.</summary>
        public int SlotForAim(float deg)
        {
            int n = Cfg.modeASlots;
            float step = (Cfg.modeAArcHalfDeg * 2f) / n;
            for (int i = 0; i < n; i++)
            {
                float centre = -Cfg.modeAArcHalfDeg + (i + 0.5f) * step;
                if (deg >= centre - Cfg.modeASlotHalfWidthDeg && deg <= centre + Cfg.modeASlotHalfWidthDeg)
                    return i;
            }
            return -1;
        }

        public float SlotCentreDeg(int i)
        {
            float step = (Cfg.modeAArcHalfDeg * 2f) / Cfg.modeASlots;
            return -Cfg.modeAArcHalfDeg + (i + 0.5f) * step;
        }

        // =====================================================================
        //  Tick
        // =====================================================================

        public void Tick(float dt)
        {
            if (dt <= 0f) return;
            if (_leftDown) _leftHeld += dt;
            if (_rightDown) _rightHeld += dt;
            WarnIfHoldingTooLong();

            if (_gameOverLock > 0f) _gameOverLock -= dt;
            PhaseTime += dt;

            switch (Phase)
            {
                case Phase.Title:
                    break;

                case Phase.PlayerHolding:
                    TickPlayerHolding(dt);
                    break;

                case Phase.BombOutgoing:
                    if (PhaseTime >= PhaseLength) BeginReceiverHolding();
                    break;

                case Phase.ReceiverHolding:
                    if (PhaseTime >= PhaseLength) BeginBombIncoming();
                    break;

                case Phase.BombIncoming:
                    if (PhaseTime >= PhaseLength) BeginCatch(firstCatch: false);
                    break;

                case Phase.Failing:
                    if (PhaseTime >= PhaseLength) EnterGameOver();
                    break;

                case Phase.GameOver:
                    break;
            }
        }

        void TickPlayerHolding(float dt)
        {
            float burn = dt;
            if (Mode == ControlMode.HoldRelease && Holding) burn *= Cfg.holdFuseMultiplier;

            float before = FuseFraction;
            Fuse -= burn;
            float after = FuseFraction;

            // Mid-fuse scheduled twists. Checked on the crossing, so a long frame cannot skip one.
            if (Turn.BothBlocked && !Turn.Opened && before > Turn.OpenAtFuseFraction && after <= Turn.OpenAtFuseFraction)
            {
                Turn.Opened = true;
                if (Turn.OpenSlot >= 0 && Turn.OpenSlot < Turn.Slots.Length)
                {
                    Turn.Slots[Turn.OpenSlot] = Blocker.None;
                    Emit(SimEventKind.Open, slot: Turn.OpenSlot);
                }
            }
            if (Turn.CanSwap && !Turn.Swapped && before > Turn.SwapAtFuseFraction && after <= Turn.SwapAtFuseFraction)
            {
                Turn.Swapped = true;
                DoSwap();
            }

            if (Mode == ControlMode.HoldRelease && Holding)
            {
                HoldTime += dt;
                if (HoldTime >= Cfg.maxHoldSeconds)
                {
                    Explode(FailReason.HeldTooLong);
                    return;
                }
                float sweep = Cfg.SweepFor(Passes);
                AimDeg += sweep * AimDir * dt;
                float lim = Cfg.modeAArcHalfDeg;
                // Ping-pong, and reflect correctly even if a long frame overshoots.
                int guard = 0;
                while ((AimDeg > lim || AimDeg < -lim) && guard++ < 64)
                {
                    if (AimDeg > lim) { AimDeg = 2f * lim - AimDeg; AimDir = -1; }
                    if (AimDeg < -lim) { AimDeg = -2f * lim - AimDeg; AimDir = 1; }
                }
            }

            if (Fuse <= 0f)
            {
                Fuse = 0f;
                if (Turn.Bomb == BombKind.Dud) Fizzle();
                else Explode(FailReason.FuseRanOut);
            }
        }

        void WarnIfHoldingTooLong()
        {
            // Windows opens Filter Keys after 8 s of held Right Shift. Nothing here may get close.
            float held = _leftHeld > _rightHeld ? _leftHeld : _rightHeld;
            if (!_warnedHoldThisPress && held >= Cfg.holdWarningSeconds && (_leftDown || _rightDown))
            {
                _warnedHoldThisPress = true;
                Emit(SimEventKind.HoldWarning);
                ShowHint(Hint.DontHoldShift);
            }
        }

        void DoSwap()
        {
            // Move the single blocked slot to a different slot.
            int from = -1;
            for (int i = 0; i < Turn.Slots.Length; i++) if (Turn.Slots[i] != Blocker.None) { from = i; break; }
            if (from < 0) return;
            Blocker kind = Turn.Slots[from];
            int to = from;
            if (Turn.Slots.Length == 2) to = 1 - from;
            else
            {
                int tries = 0;
                while (to == from && tries++ < 16) to = _rng.Next(Turn.Slots.Length);
            }
            if (to == from) return;
            Turn.Slots[from] = Blocker.None;
            Turn.Slots[to] = kind;
            Emit(SimEventKind.Swap, slot: to);
        }

        // =====================================================================
        //  Phase transitions
        // =====================================================================

        void BeginCatch(bool firstCatch)
        {
            Phase = Phase.PlayerHolding;
            PhaseTime = 0f; PhaseLength = 0f;
            ActiveSlot = -1;
            Holding = false; HoldTime = 0f;

            RollTurn();

            FuseMax = Cfg.FuseFor(Passes);
            if (Turn.Bomb == BombKind.Hot) FuseMax *= 0.5f;   // "half the fuse"
            Fuse = FuseMax;

            BandLow = Cfg.bandLow;
            BandHigh = Cfg.BandHighFor(Passes);

            Emit(SimEventKind.Catch);

            if (firstCatch)
            {
                if (Mode == ControlMode.LeftRight) ShowHint(Hint.FirstCatchModeB);
                else ShowHint(Hint.FirstCatchModeA_Hold);
            }
            AnnounceTurnHints();
        }

        void BeginReceiverHolding()
        {
            Phase = Phase.ReceiverHolding;
            PhaseTime = 0f;
            PhaseLength = Cfg.ReturnDelayFor(Passes);
        }

        void BeginBombIncoming()
        {
            Phase = Phase.BombIncoming;
            PhaseTime = 0f;
            PhaseLength = Cfg.FlightFor(Passes);
            Emit(SimEventKind.ReturnThrow, slot: ActiveSlot);
        }

        void EnterGameOver()
        {
            Phase = Phase.GameOver;
            PhaseTime = 0f; PhaseLength = 0f;
            _gameOverLock = Cfg.retryIgnoreSeconds;
            if (Score > Best) { Best = Score; Emit(SimEventKind.NewBest, i: Score); }
            Emit(SimEventKind.GameOver, reason: Fail);
        }

        // =====================================================================
        //  Throwing
        // =====================================================================

        void ThrowAtSlot(int slot)
        {
            // A dud must be held, never thrown - checked before anything else so the
            // fail reason the player is shown is the one that actually applies.
            if (Turn.Bomb == BombKind.Dud) { Explode(FailReason.ThrewADud); return; }

            if (slot < 0) { Explode(FailReason.ReleasedBetween); return; }
            if (slot >= Turn.Slots.Length) { Explode(FailReason.ThrewAtEmpty); return; }

            switch (Turn.Slots[slot])
            {
                case Blocker.Shield: Explode(FailReason.ThrewAtShield); return;
                case Blocker.Asleep: Explode(FailReason.ThrewAtSleeper); return;
                case Blocker.OnBreak: Explode(FailReason.ThrewAtEmpty); return;
                case Blocker.Nope: Explode(FailReason.ThrewAtNope); return;
            }

            // A good throw. Quality normally comes from where the ring's tip sits.
            float frac = FuseFraction;
            float elapsed = 1f - frac;
            ThrowQuality q;
            if (Cfg.rushedBelowElapsedFraction > 0f && elapsed < Cfg.rushedBelowElapsedFraction)
            {
                q = ThrowQuality.Rushed;
            }
            else if (Mode == ControlMode.HoldRelease && Cfg.modeAPerfectWithinDeg > 0f)
            {
                // Mode A variant: judge the PERFECT on how close to the robot's centre the arrow
                // was, so the mode has one skill to master rather than two at once.
                float off = AimDeg - SlotCentreDeg(slot);
                if (off < 0f) off = -off;
                LastAimErrorDeg = off;
                q = off <= Cfg.modeAPerfectWithinDeg ? ThrowQuality.Perfect : ThrowQuality.Normal;
            }
            else
            {
                q = frac >= BandLow && frac <= BandHigh ? ThrowQuality.Perfect : ThrowQuality.Normal;
            }

            int points;
            if (q == ThrowQuality.Perfect)
            {
                Combo++;
                points = 1 + Combo;
                if (points > Cfg.perfectMaxPoints) points = Cfg.perfectMaxPoints;
            }
            else if (q == ThrowQuality.Rushed) { Combo = 0; points = 0; }
            else { Combo = 0; points = 1; }

            if (Turn.Bomb == BombKind.Hot) points *= Cfg.hotScoreMultiplier;

            Score += points;
            ActiveSlot = slot;

            Emit(SimEventKind.Throw, slot: slot, quality: q, i: points);

            Phase = Phase.BombOutgoing;
            PhaseTime = 0f;
            PhaseLength = Cfg.FlightFor(Passes);

            CompletePass();
        }

        void Fizzle()
        {
            Score += Cfg.dudFizzlePoints;
            Emit(SimEventKind.Fizzle, i: Cfg.dudFizzlePoints);
            // A robot hands over the replacement, so the incoming bomb has somewhere to come FROM.
            // Leaving this at -1 made the new bomb hop on the spot in the player's own hands.
            int from = Turn.FirstOpenSlot();
            ActiveSlot = from >= 0 ? from : 0;
            // The spent dud is cleared away and a fresh bomb comes in on the usual return beat.
            Phase = Phase.ReceiverHolding;
            PhaseTime = 0f;
            PhaseLength = Cfg.ReturnDelayFor(Passes);
            CompletePass();
        }

        /// <summary>
        /// One pass banked. A fizzled dud counts as a pass too: it advanced the run, so the
        /// difficulty ramp and the zone clock should advance with it (DECISIONS_FOR_ROY D-006).
        /// </summary>
        void CompletePass()
        {
            Passes++;
            Emit(SimEventKind.Scored, i: Score);
            int z = Cfg.ZoneFor(Passes);
            if (z != Zone)
            {
                Zone = z;
                Emit(SimEventKind.ZoneChange, i: z);
            }
        }

        void Explode(FailReason reason)
        {
            Fail = reason;
            Holding = false;
            Phase = Phase.Failing;
            PhaseTime = 0f;
            PhaseLength = Cfg.explodeSeconds;
            Emit(SimEventKind.Explode, reason: reason);
        }

        // =====================================================================
        //  Turn generation - decide once, then act (brief rule 17)
        // =====================================================================

        void RollTurn()
        {
            int n = SlotCount;
            Turn.Reset(n);
            int p = Passes;

            // --- bomb kind ---
            if (p >= Cfg.unlockDud && Roll() < Cfg.chanceDud) Turn.Bomb = BombKind.Dud;
            else if (p >= Cfg.unlockHot && Roll() < Cfg.chanceHot) Turn.Bomb = BombKind.Hot;
            else Turn.Bomb = BombKind.Normal;

            // A DUD turn carries no blocker and no mirror. "Hold it" must be the only
            // correct action, so the fail is never ambiguous (DECISIONS_FOR_ROY D-004).
            if (Turn.Bomb == BombKind.Dud) return;

            bool modeB = Mode == ControlMode.LeftRight;

            // --- both sides blocked (Mode B, 30+) ---
            if (modeB && p >= Cfg.unlockBothBlocked && Roll() < Cfg.chanceBothBlocked)
            {
                Turn.BothBlocked = true;
                Turn.Slots[0] = Blocker.Shield;
                Turn.Slots[1] = Blocker.Shield;
                Turn.OpenSlot = _rng.Next(2);
                Turn.OpenAtFuseFraction = Lerp(Cfg.bothBlockedOpenAtMin, Cfg.bothBlockedOpenAtMax, (float)Roll());
                return;  // not combined with mirrored: the fail must stay legible
            }

            // --- ordinary blockers ---
            float chance = Cfg.BlockerChanceFor(p);
            int blockersWanted = 0;
            if (Roll() < chance) blockersWanted = 1;
            // Mode A gets two blocked robots at once from 30+.
            if (!modeB && blockersWanted == 1 && p >= Cfg.unlockTwoBlockedModeA && Roll() < chance) blockersWanted = 2;
            // Never block every slot: there must always be a right answer.
            if (blockersWanted > n - 1) blockersWanted = n - 1;

            for (int k = 0; k < blockersWanted; k++)
            {
                int slot = PickFreeSlot(n);
                if (slot < 0) break;
                Turn.Slots[slot] = PickBlocker(p, modeB);
            }

            // --- mirrored (Mode B, 18+) ---
            if (modeB && p >= Cfg.unlockMirrored && Roll() < Cfg.chanceMirrored) Turn.Mirrored = true;

            // --- swap mid-hold (40+) ---
            if (p >= Cfg.unlockSwap && blockersWanted > 0 && Roll() < Cfg.chanceSwap)
            {
                Turn.CanSwap = true;
                Turn.SwapAtFuseFraction = Lerp(Cfg.swapAtMin, Cfg.swapAtMax, (float)Roll());
            }
        }

        int PickFreeSlot(int n)
        {
            for (int tries = 0; tries < 32; tries++)
            {
                int s = _rng.Next(n);
                if (Turn.Slots[s] == Blocker.None) return s;
            }
            for (int s = 0; s < n; s++) if (Turn.Slots[s] == Blocker.None) return s;
            return -1;
        }

        Blocker PickBlocker(int p, bool modeB)
        {
            // Build the unlocked set, then pick evenly from it.
            var pool = new Blocker[3];
            int c = 0;
            if (modeB)
            {
                if (p >= Cfg.unlockShield) pool[c++] = Blocker.Shield;
                if (p >= Cfg.unlockAsleep) pool[c++] = Blocker.Asleep;
                if (p >= Cfg.unlockOnBreak) pool[c++] = Blocker.OnBreak;
            }
            else
            {
                // Mode A: NOPE dodgers and sleeping robots. No ON BREAK gap - Mode A
                // already punishes the gaps between robots.
                if (p >= Cfg.unlockShield) pool[c++] = Blocker.Nope;
                if (p >= Cfg.unlockAsleep) pool[c++] = Blocker.Asleep;
            }
            if (c == 0) return Blocker.None;
            return pool[_rng.Next(c)];
        }

        // =====================================================================
        //  Hints - one short line, once only, the first time a twist appears
        // =====================================================================

        void AnnounceTurnHints()
        {
            if (Turn.Bomb == BombKind.Dud) { ShowHint(Hint.Dud); return; }
            if (Turn.Bomb == BombKind.Hot) ShowHint(Hint.Hot);
            if (Turn.BothBlocked) { ShowHint(Hint.BothBlocked); return; }
            for (int i = 0; i < Turn.Slots.Length; i++)
            {
                switch (Turn.Slots[i])
                {
                    case Blocker.Shield: ShowHint(Hint.Shield); break;
                    case Blocker.Nope: ShowHint(Hint.Shield); break;
                    case Blocker.Asleep: ShowHint(Hint.Asleep); break;
                    case Blocker.OnBreak: ShowHint(Hint.OnBreak); break;
                }
            }
            if (Turn.Mirrored) ShowHint(Hint.Mirrored);
            if (Turn.CanSwap) ShowHint(Hint.Swap);
        }

        void ShowHint(Hint h)
        {
            int i = (int)h;
            if (i <= 0 || i >= _hintShown.Length || _hintShown[i]) return;
            _hintShown[i] = true;
            Emit(SimEventKind.HintShown, hint: h);
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        double Roll() => _rng.NextDouble();

        void Emit(SimEventKind kind, int slot = -1, int i = 0, float f = 0f,
                  ThrowQuality quality = ThrowQuality.Normal, FailReason reason = FailReason.None,
                  Hint hint = Hint.None)
        {
            Events.Add(new SimEvent
            {
                Kind = kind, Slot = slot, IntValue = i, FloatValue = f,
                Quality = quality, Reason = reason, Hint = hint,
            });
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
