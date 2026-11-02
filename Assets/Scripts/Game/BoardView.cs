using UnityEngine;
using KT.ShiftIt.Sim;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// The playfield: the player robot, the receivers, the bomb, the fuse ring and the Mode A aim
    /// arrow. It READS the simulation and never writes to it - the only thing that writes to the
    /// simulation is ShiftInput.
    ///
    /// The whole board is rebuilt when the control mode changes, because Mode A and Mode B have a
    /// different number of receivers in different places.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        public const float PlayerY = -2.6f;
        public const float ModeAArcRadius = 5.6f;
        const float BombHeldY = 1.35f;

        ShiftSim _sim;
        RobotView _player;
        RobotView[] _receivers = new RobotView[0];
        Label[] _keyLabels = new Label[0];

        SpriteRenderer _bomb, _bombHighlight, _bombOutline;
        /// <summary>Holds only the bomb's own art, so the shiver cannot squash the fuse ring.</summary>
        Transform _bombArt;
        Transform _bombRoot;
        Label _bombTag;
        FuseRingView _ring;

        Transform _aim;
        SpriteRenderer _aimShaft, _aimHead;

        Label _mirrorTag;
        Transform _world;

        ControlMode _builtMode = (ControlMode)(-1);
        float _lastFuseFraction = 1f;
        Vector3 _bombPos;

        static readonly Color PlayerColour = new Color(1.00f, 0.80f, 0.15f);
        static readonly Color[] ReceiverColours =
        {
            new Color(0.38f, 0.72f, 1.00f),   // blue
            new Color(1.00f, 0.45f, 0.42f),   // red
            new Color(0.55f, 0.88f, 0.48f),   // green
            new Color(0.80f, 0.56f, 1.00f),   // purple
            new Color(1.00f, 0.66f, 0.30f),   // orange
        };

        public RobotView Player => _player;
        public RobotView[] Receivers => _receivers;
        public Vector3 BombPosition => _bombPos;

        public static BoardView Create(Transform parent, ShiftSim sim)
        {
            var go = new GameObject("Board");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BoardView>();
            b._sim = sim;
            b._world = go.transform;
            b.BuildCommon();
            return b;
        }

        void BuildCommon()
        {
            // --- the bomb: black ball, bold outline, sparking fuse (Stage 2 adds the sparks) ---
            var bombGo = new GameObject("Bomb");
            bombGo.transform.SetParent(_world, false);
            _bombRoot = bombGo.transform;
            var artGo = new GameObject("Art");
            artGo.transform.SetParent(bombGo.transform, false);
            _bombArt = artGo.transform;
            _bombOutline = AddSprite(_bombArt, "Outline", Shapes.Circle(96),
                                     new Color(0.05f, 0.05f, 0.06f), 10, 1.16f);
            _bomb = AddSprite(_bombArt, "Body", Shapes.Circle(96),
                              new Color(0.16f, 0.16f, 0.19f), 11, 1f);
            _bombHighlight = AddSprite(_bombArt, "Shine", Shapes.Circle(28),
                                       new Color(1f, 1f, 1f, 0.33f), 12, 1f);
            _bombHighlight.transform.localPosition = new Vector3(-0.16f, 0.17f, 0f);
            _bombTag = Label.CreateWorld(bombGo.transform, "Tag", new Vector3(0, 0.02f, 0f), 40, Color.white);
            _bombTag.SetText("");

            _ring = FuseRingView.Create(bombGo.transform, 2.05f);

            // --- Mode A aim arrow ---
            var aimGo = new GameObject("Aim");
            aimGo.transform.SetParent(_world, false);
            aimGo.transform.localPosition = new Vector3(0f, PlayerY, 0f);
            _aim = aimGo.transform;
            _aimShaft = AddSprite(_aim, "Shaft", Shapes.RoundedRect(14, 300, 7),
                                  new Color(1f, 0.95f, 0.5f, 0.85f), 5, 1f);
            _aimShaft.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            _aimHead = AddSprite(_aim, "Head", Shapes.TriangleUp(60, 56),
                                 new Color(1f, 0.95f, 0.5f, 1f), 6, 1f);
            _aimHead.transform.localPosition = new Vector3(0f, 3.1f, 0f);
            aimGo.SetActive(false);

            _mirrorTag = Label.CreateWorld(_world, "MirrorTag", new Vector3(0f, PlayerY + 2.5f, 0f), 46,
                                           new Color(1f, 0.45f, 0.85f));
            _mirrorTag.SetText("");
        }

        static SpriteRenderer AddSprite(Transform parent, string name, Sprite s, Color c, int order, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Lay out the receivers for the live control mode. Safe to call every frame.</summary>
        public void EnsureLayout()
        {
            if (_builtMode == _sim.Mode && _receivers.Length == _sim.SlotCount) return;
            _builtMode = _sim.Mode;

            for (int i = 0; i < _receivers.Length; i++)
                if (_receivers[i] != null) Destroy(_receivers[i].gameObject);
            for (int i = 0; i < _keyLabels.Length; i++)
                if (_keyLabels[i] != null) Destroy(_keyLabels[i].gameObject);

            if (_player == null)
                _player = RobotView.Create(_world, "Player", new Vector3(0f, PlayerY, 0f),
                                           PlayerColour, true, 1.12f);

            int n = _sim.SlotCount;
            _receivers = new RobotView[n];
            _keyLabels = new Label[n];

            for (int i = 0; i < n; i++)
            {
                Vector3 p = SlotPosition(i);
                _receivers[i] = RobotView.Create(_world, "Receiver" + i, p,
                                                 ReceiverColours[i % ReceiverColours.Length], false,
                                                 _sim.Mode == ControlMode.LeftRight ? 1.0f : 0.78f);
                // The brief: ALWAYS show the L-SHIFT / R-SHIFT labels under the two robots in Mode B.
                if (_sim.Mode == ControlMode.LeftRight)
                {
                    _keyLabels[i] = Label.CreateWorld(_world, "Key" + i,
                                                      p + new Vector3(0f, -1.35f, 0f), 40, Color.white);
                }
            }
            _aim.gameObject.SetActive(_sim.Mode == ControlMode.HoldRelease);
        }

        /// <summary>Where receiver i stands, in world units.</summary>
        public Vector3 SlotPosition(int i)
        {
            if (_sim.Mode == ControlMode.LeftRight)
                return new Vector3(i == 0 ? -5.3f : 5.3f, 0.9f, 0f);

            float deg = _sim.SlotCentreDeg(i);
            float rad = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad) * ModeAArcRadius,
                               PlayerY + Mathf.Cos(rad) * ModeAArcRadius, 0f);
        }

        // =====================================================================

        public void Tick(float dt)
        {
            EnsureLayout();

            var sim = _sim;
            bool playing = sim.Phase != Phase.Title;
            bool held = sim.Phase == Phase.PlayerHolding;

            // ---- blockers and faces ----
            for (int i = 0; i < _receivers.Length; i++)
            {
                var r = _receivers[i];
                Blocker b = i < sim.Turn.SlotCount ? sim.Turn.Slots[i] : Blocker.None;
                r.SetBlocker(b);

                bool hasBomb = (sim.Phase == Phase.ReceiverHolding || sim.Phase == Phase.BombIncoming)
                               && sim.ActiveSlot == i;
                if (b == Blocker.None)
                {
                    if (hasBomb) r.SetFace(Face.Panic);
                    else if (sim.Phase == Phase.BombOutgoing && sim.ActiveSlot == i) r.SetFace(Face.Panic);
                    else r.SetFace(Face.Calm);
                }
                r.Tick(dt, hasBomb ? 1f : 0f);

                if (i < _keyLabels.Length && _keyLabels[i] != null)
                {
                    // Mirrored: the labels SWAP and turn pink, so the tell is in the words the
                    // player is already reading rather than only in a banner.
                    bool mir = sim.Turn.Mirrored;
                    bool leftKeyHere = mir ? (i == 1) : (i == 0);
                    _keyLabels[i].SetText(leftKeyHere ? "L-SHIFT" : "R-SHIFT");
                    _keyLabels[i].SetColour(mir ? new Color(1f, 0.45f, 0.85f) : new Color(0.92f, 0.92f, 0.95f));
                    _keyLabels[i].SetActive(playing);
                }
            }

            // ---- player face: panic tracks the fuse ----
            float fuse = sim.FuseFraction;
            float panic = held ? Mathf.Clamp01(1f - (fuse - 0.1f) / 0.5f) : 0f;
            if (sim.Phase == Phase.Failing) _player.SetFace(Face.Dead);
            else if (held) _player.SetFace(panic > 0.35f ? Face.Panic : Face.Calm);
            else if (sim.Phase == Phase.BombOutgoing) _player.SetFace(Face.Happy);
            else _player.SetFace(Face.Calm);
            _player.Tick(dt, panic);

            _mirrorTag.SetText(held && sim.Turn.Mirrored ? "MIRRORED" : "");
            _mirrorTag.SetAlpha(held && sim.Turn.Mirrored
                ? 0.75f + 0.25f * Mathf.Sin(Time.time * 7f) : 0f);

            // ---- bomb ----
            UpdateBomb(dt, held);

            // ---- aim arrow ----
            if (sim.Mode == ControlMode.HoldRelease)
            {
                bool show = held && sim.Holding;
                _aimShaft.enabled = show;
                _aimHead.enabled = show;
                if (show)
                {
                    _aim.localRotation = Quaternion.Euler(0f, 0f, -sim.AimDeg);
                    int over = sim.SlotForAim(sim.AimDeg);
                    // The arrow turns red over a gap or a blocked robot: the player can see the
                    // mistake BEFORE committing, which is the whole point of a tell.
                    bool bad = over < 0 || (over < sim.Turn.SlotCount && sim.Turn.Slots[over] != Blocker.None);
                    var c = bad ? new Color(1f, 0.33f, 0.28f) : new Color(0.6f, 1f, 0.55f);
                    _aimShaft.color = new Color(c.r, c.g, c.b, 0.85f);
                    _aimHead.color = c;
                }
            }

            _lastFuseFraction = fuse;
        }

        void UpdateBomb(float dt, bool held)
        {
            var sim = _sim;
            Vector3 hands = _player.Home + new Vector3(0f, BombHeldY, 0f);
            Vector3 target = sim.ActiveSlot >= 0 && sim.ActiveSlot < _receivers.Length
                           ? _receivers[sim.ActiveSlot].Home + new Vector3(0f, 0.55f, 0f)
                           : hands;

            bool visible = true;
            switch (sim.Phase)
            {
                case Phase.Title:
                    _bombPos = hands;
                    visible = false;
                    break;
                case Phase.PlayerHolding:
                    _bombPos = hands;
                    break;
                case Phase.BombOutgoing:
                    _bombPos = Arc(hands, target, sim.PhaseT);
                    break;
                case Phase.ReceiverHolding:
                    _bombPos = target;
                    break;
                case Phase.BombIncoming:
                    _bombPos = Arc(target, hands, sim.PhaseT);
                    break;
                case Phase.Failing:
                    _bombPos = hands;
                    visible = false;         // it has just exploded
                    break;
                case Phase.GameOver:
                    visible = false;
                    break;
            }

            _bombRoot.localPosition = _bombPos;
            _bomb.enabled = visible; _bombOutline.enabled = visible; _bombHighlight.enabled = visible;

            // Bomb colour and label by kind.
            Color body = new Color(0.16f, 0.16f, 0.19f);
            string tag = "";
            Color tagCol = Color.white;
            switch (sim.Bomb)
            {
                case BombKind.Hot:
                    // Glows brighter as it burns down.
                    body = Color.Lerp(new Color(1f, 0.30f, 0.12f), new Color(0.62f, 0.12f, 0.06f),
                                      Mathf.Clamp01(sim.FuseFraction));
                    tag = "HOT"; tagCol = new Color(1f, 0.92f, 0.6f);
                    break;
                case BombKind.Dud:
                    body = new Color(0.25f, 0.62f, 0.30f);
                    tag = "DUD"; tagCol = new Color(0.88f, 1f, 0.9f);
                    break;
            }
            _bomb.color = body;
            _bombTag.SetText(visible ? tag : "");
            _bombTag.SetColour(tagCol);

            // The bomb shivers harder as the fuse runs down - readable even without the ring.
            float shiver = held ? Mathf.Clamp01(1f - sim.FuseFraction) : 0f;
            float s = 1f + Mathf.Sin(Time.time * (14f + 40f * shiver)) * 0.035f * (0.4f + shiver);
            _bombArt.localScale = new Vector3(s, 2f - s, 1f);

            _ring.SetVisible(held);
            if (held)
                _ring.Set(sim.FuseFraction, sim.BandLow, sim.BandHigh,
                          sim.Bomb == BombKind.Dud, sim.Bomb == BombKind.Hot, dt);
        }

        /// <summary>A thrown bomb arcs; a straight lerp reads as a slide and kills the feel.</summary>
        static Vector3 Arc(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(a, b, t);
            p.y += Mathf.Sin(t * Mathf.PI) * 1.5f;
            return p;
        }

        /// <summary>Make the robot at this slot react to catching the bomb.</summary>
        public void PopSlot(int slot)
        {
            if (slot >= 0 && slot < _receivers.Length) _receivers[slot].Pop();
        }

        /// <summary>The L-SHIFT / R-SHIFT label under receiver i, as the player reads it.</summary>
        public string KeyLabelText(int i)
            => i >= 0 && i < _keyLabels.Length && _keyLabels[i] != null ? _keyLabels[i].Raw.text : "";
        public Color KeyLabelColour(int i)
            => i >= 0 && i < _keyLabels.Length && _keyLabels[i] != null ? _keyLabels[i].Raw.color : Color.white;
        public string MirrorTagText => _mirrorTag != null && _mirrorTag.Raw.color.a > 0.05f ? _mirrorTag.Raw.text : "";
        public bool AimArrowShown => _aimHead != null && _aimHead.enabled;
        public Color AimArrowColour => _aimHead != null ? _aimHead.color : Color.white;
        public FuseRingView Ring => _ring;
        public bool BombVisible => _bomb != null && _bomb.enabled;
        public Color BombColour => _bomb != null ? _bomb.color : Color.white;
        public string BombTagText => _bombTag != null ? _bombTag.Raw.text : "";

        public void PopPlayer() => _player.Pop();

        public void KillAll()
        {
            _player.SetFace(Face.Dead);
            for (int i = 0; i < _receivers.Length; i++) _receivers[i].SetFace(Face.Dead);
        }
    }
}
