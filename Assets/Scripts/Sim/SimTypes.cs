// SHIFT IT! - pure-C# simulation types. No UnityEngine reference anywhere in this assembly.
// Pattern 01: the simulation must not be able to see the UI. Enforced by KT.ShiftIt.Sim.asmdef.
namespace KT.ShiftIt.Sim
{
    /// <summary>The two control schemes built for Roy's Stage 1 comparison.</summary>
    public enum ControlMode
    {
        /// <summary>MODE A: hold Shift, an aim arrow sweeps the arc, release to throw.</summary>
        HoldRelease = 0,
        /// <summary>MODE B: Left Shift throws left, Right Shift throws right.</summary>
        LeftRight = 1,
    }

    public enum Side { None = 0, Left = 1, Right = 2 }

    public enum Phase
    {
        Title = 0,
        /// <summary>Bomb is in the player's hands, fuse burning. The only phase that takes input.</summary>
        PlayerHolding = 1,
        /// <summary>Bomb arcing out to a receiver.</summary>
        BombOutgoing = 2,
        /// <summary>Receiver panicking before it throws back.</summary>
        ReceiverHolding = 3,
        /// <summary>Bomb arcing back to the player.</summary>
        BombIncoming = 4,
        /// <summary>Explosion playing out; input ignored.</summary>
        Failing = 5,
        GameOver = 6,
    }

    public enum BombKind { Normal = 0, Hot = 1, Dud = 2 }

    /// <summary>What is wrong with a receiver slot this turn. One value per slot.</summary>
    public enum Blocker
    {
        None = 0,
        /// <summary>Energy shield arc + red X eyes. Bounces back.</summary>
        Shield = 1,
        /// <summary>Eyes shut, floating Zzz, no idle bob. Bounces back.</summary>
        Asleep = 2,
        /// <summary>Spot empty: dashed outline + ON BREAK sign. Bomb hits the floor.</summary>
        OnBreak = 3,
        /// <summary>Mode A dodger: crossed arms + red NOPE sign.</summary>
        Nope = 4,
    }

    public enum FailReason
    {
        None = 0,
        FuseRanOut = 1,
        ThrewAtShield = 2,
        ThrewAtSleeper = 3,
        ThrewAtEmpty = 4,
        ThrewADud = 5,
        HeldTooLong = 6,
        ThrewAtNope = 7,
        /// <summary>Mode A: released the arrow in a gap between robots.</summary>
        ReleasedBetween = 8,
    }

    public enum ThrowQuality
    {
        /// <summary>Thrown before the green band. +1, no combo.</summary>
        Normal = 0,
        /// <summary>Ring end inside the green band. +1+combo, max +6.</summary>
        Perfect = 1,
        /// <summary>Thrown inside the rushed window, if one is configured. Scores nothing.</summary>
        Rushed = 2,
    }

    /// <summary>Which one-time hint line this is, so each is shown once only.</summary>
    public enum Hint
    {
        None = 0,
        FirstCatchModeA_Hold = 1,
        FirstCatchModeA_Release = 2,
        FirstCatchModeB = 3,
        Shield = 4,
        Asleep = 5,
        OnBreak = 6,
        Hot = 7,
        Mirrored = 8,
        Dud = 9,
        BothBlocked = 10,
        Swap = 11,
        DontHoldShift = 12,
        Count = 13,
    }

    public enum SimEventKind
    {
        None = 0,
        Started = 1,
        /// <summary>Bomb landed in the player's hands. A new turn is live.</summary>
        Catch = 2,
        /// <summary>Player threw. Slot = target, Quality, Points.</summary>
        Throw = 3,
        /// <summary>A pass was completed and scored.</summary>
        Scored = 4,
        /// <summary>Receiver threw it back.</summary>
        ReturnThrow = 5,
        /// <summary>A dud fizzled out safely in the player's hands.</summary>
        Fizzle = 6,
        /// <summary>Everything stops: Reason says why.</summary>
        Explode = 7,
        GameOver = 8,
        ZoneChange = 9,
        /// <summary>A both-blocked turn opened one side. Slot = the side that opened.</summary>
        Open = 10,
        /// <summary>The blocked side moved. Slot = its new home.</summary>
        Swap = 11,
        /// <summary>Show this hint line once.</summary>
        HintShown = 12,
        /// <summary>Player is holding a Shift key down dangerously long (Windows Filter Keys).</summary>
        HoldWarning = 13,
        NewBest = 14,
    }

    public struct SimEvent
    {
        public SimEventKind Kind;
        public int Slot;
        public int IntValue;
        public float FloatValue;
        public ThrowQuality Quality;
        public FailReason Reason;
        public Hint Hint;

        public static SimEvent Of(SimEventKind k) => new SimEvent { Kind = k };
    }

    /// <summary>
    /// Everything that is true about one catch: the bomb, every slot's blocker, and the scheduled
    /// mid-fuse events. Decided once when the bomb is caught (brief rule 17, "decide once then act")
    /// so nothing is re-rolled per frame and a run is reproducible from its seed.
    /// </summary>
    public sealed class Turn
    {
        public BombKind Bomb;
        /// <summary>Length 2 in Mode B (0 = left, 1 = right); length 5 in Mode A, left to right.</summary>
        public Blocker[] Slots = new Blocker[2];
        /// <summary>Left Shift throws right and vice versa for this one catch.</summary>
        public bool Mirrored;
        /// <summary>Both sides blocked; one opens part way through the fuse.</summary>
        public bool BothBlocked;
        public int OpenSlot = -1;
        /// <summary>Fuse fraction REMAINING at which OpenSlot unblocks.</summary>
        public float OpenAtFuseFraction;
        public bool Opened;
        /// <summary>The blocked side can move mid-hold.</summary>
        public bool CanSwap;
        public float SwapAtFuseFraction;
        public bool Swapped;

        public int SlotCount => Slots.Length;

        public void Reset(int slotCount)
        {
            if (Slots.Length != slotCount) Slots = new Blocker[slotCount];
            for (int i = 0; i < Slots.Length; i++) Slots[i] = Blocker.None;
            Bomb = BombKind.Normal;
            Mirrored = false;
            BothBlocked = false;
            OpenSlot = -1;
            OpenAtFuseFraction = 0f;
            Opened = false;
            CanSwap = false;
            SwapAtFuseFraction = 0f;
            Swapped = false;
        }

        public bool AnyBlocked()
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] != Blocker.None) return true;
            return false;
        }

        public int FirstOpenSlot()
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] == Blocker.None) return i;
            return -1;
        }
    }
}
