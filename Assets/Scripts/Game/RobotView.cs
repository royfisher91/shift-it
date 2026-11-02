using UnityEngine;
using KT.ShiftIt.Sim;

namespace KT.ShiftIt.Game
{
    /// <summary>What the robot's screen-face is showing.</summary>
    public enum Face { Calm, Happy, Panic, Nope, Asleep, Dead }

    /// <summary>
    /// One chunky robot, built entirely from generated shapes: body, bold outline, screen-face with
    /// eyes and mouth, and an antenna. Stage 1 is deliberately grey-box, but the FACE and the
    /// BLOCKER TELLS are built properly now, because the shield/dodger twist is part of the Stage 1
    /// gate and an unreadable tell is a gameplay bug, not an art gap.
    ///
    /// Every tell is a different SHAPE as well as a different colour, so none of them depends on
    /// hue alone (the accessibility rule in pattern 09).
    /// </summary>
    public sealed class RobotView : MonoBehaviour
    {
        // body
        SpriteRenderer _outline, _body, _screen, _eyeL, _eyeR, _mouth, _antenna, _bulb;
        SpriteRenderer _armL, _armR;
        // tells
        SpriteRenderer _shieldArc, _emptyPad;
        Transform _zzz;
        SpriteRenderer _sweat;
        Label _sign;

        Color _bodyColour;
        float _bobPhase;
        float _bob = 1f;
        Face _face = Face.Calm;
        float _panic;
        float _shieldAlpha, _emptyAlpha, _zzzAlpha;
        Vector3 _home;
        bool _isPlayer;
        /// <summary>The scale this robot was built at. Tick must multiply it, not replace it.</summary>
        float _baseScale = 1f;

        static readonly Color Outline = new Color(0.07f, 0.07f, 0.09f, 1f);

        public Face CurrentFace => _face;

        public static RobotView Create(Transform parent, string name, Vector3 pos, Color colour,
                                       bool isPlayer, float scale = 1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            var r = go.AddComponent<RobotView>();
            r._bodyColour = colour;
            r._home = pos;
            r._isPlayer = isPlayer;
            r._baseScale = scale;
            r._bobPhase = Random.value * 10f;   // cosmetic only - see note in GameRoot about RNG streams
            r.Build();
            return r;
        }

        void Build()
        {
            // --- body: a bold black outline behind a coloured rounded body ---
            _outline = Add("Outline", Shapes.RoundedRect(132, 118, 30), Outline, 0, new Vector3(0, 0, 0), 1.09f);
            _body = Add("Body", Shapes.RoundedRect(132, 118, 30), _bodyColour, 1, Vector3.zero, 1f);

            // --- arms (capsules, pivoted at the shoulder so they can swing / cross) ---
            _armL = Add("ArmL", Shapes.CapsuleLeftPivot(52, 20), Darker(_bodyColour, 0.80f), 1,
                        new Vector3(-0.62f, -0.10f, 0f), 1f);
            _armR = Add("ArmR", Shapes.CapsuleLeftPivot(52, 20), Darker(_bodyColour, 0.80f), 1,
                        new Vector3(0.62f, -0.10f, 0f), 1f);
            _armL.transform.localRotation = Quaternion.Euler(0, 0, 200f);
            _armR.transform.localRotation = Quaternion.Euler(0, 0, -20f);

            // --- screen-face ---
            _screen = Add("Screen", Shapes.RoundedRect(96, 72, 18), new Color(0.11f, 0.13f, 0.16f), 2,
                          new Vector3(0, 0.06f, 0f), 1f);
            _eyeL = Add("EyeL", Shapes.Circle(26), Color.white, 3, new Vector3(-0.19f, 0.11f, 0f), 1f);
            _eyeR = Add("EyeR", Shapes.Circle(26), Color.white, 3, new Vector3(0.19f, 0.11f, 0f), 1f);
            _mouth = Add("Mouth", Shapes.RoundedRect(44, 12, 6), Color.white, 3, new Vector3(0, -0.14f, 0f), 1f);

            // --- antenna ---
            _antenna = Add("Antenna", Shapes.RoundedRect(8, 34, 4), Outline, 0, new Vector3(0, 0.74f, 0f), 1f);
            _bulb = Add("Bulb", Shapes.Circle(22), new Color(1f, 0.35f, 0.3f), 1, new Vector3(0, 0.92f, 0f), 1f);

            // --- blocker tells, all hidden until needed ---
            _shieldArc = Add("Shield", Shapes.Ring(190, 16), new Color(0.35f, 0.85f, 1f, 0f), 6,
                             new Vector3(0, 0.05f, 0f), 1f);
            _emptyPad = Add("EmptyPad", Shapes.DashedRing(150, 10, 14), new Color(1f, 1f, 1f, 0f), 6,
                            Vector3.zero, 1f);
            _sweat = Add("Sweat", Shapes.Circle(16), new Color(0.5f, 0.8f, 1f, 0f), 4,
                         new Vector3(0.44f, 0.30f, 0f), 1f);

            // Zzz: three rising marks for a sleeping robot.
            var z = new GameObject("Zzz");
            z.transform.SetParent(transform, false);
            z.transform.localPosition = new Vector3(0.52f, 0.78f, 0f);
            _zzz = z.transform;
            for (int i = 0; i < 3; i++)
            {
                var m = Add("z" + i, Shapes.RoundedRect(22, 7, 3), new Color(1f, 1f, 1f, 0f), 7,
                            new Vector3(i * 0.17f, i * 0.20f, 0f), 0.7f + i * 0.22f);
                m.transform.SetParent(z.transform, false);
                m.transform.localPosition = new Vector3(i * 0.17f, i * 0.20f, 0f);
                m.transform.localRotation = Quaternion.Euler(0, 0, -18f);
            }

            _sign = Label.CreateWorld(transform, "Sign", new Vector3(0, 1.28f, 0f), 44, Color.white);
            _sign.SetText("");
        }

        SpriteRenderer Add(string name, Sprite sprite, Color colour, int order, Vector3 localPos, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = colour;
            sr.sortingOrder = order;
            return sr;
        }

        static Color Darker(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        // =====================================================================

        public void SetFace(Face f) => _face = f;

        /// <summary>
        /// Show the tell for this slot's blocker. Each one is a distinct silhouette:
        /// shield = a bright arc in front; asleep = shut eyes and floating Zzz with the bob stopped;
        /// on-break = the robot vanishes and a dashed pad outline and sign remain; nope = crossed
        /// arms and a red sign.
        /// </summary>
        public void SetBlocker(Blocker b)
        {
            bool onBreak = b == Blocker.OnBreak;
            _shieldAlpha = b == Blocker.Shield ? 1f : 0f;
            _emptyAlpha = onBreak ? 1f : 0f;
            _zzzAlpha = b == Blocker.Asleep ? 1f : 0f;

            // The whole robot is hidden for ON BREAK - the empty spot IS the tell.
            SetBodyVisible(!onBreak);

            switch (b)
            {
                case Blocker.Shield:
                    SetFace(Face.Nope);
                    _sign.SetText("SHIELD");
                    _sign.SetColour(new Color(0.45f, 0.88f, 1f));
                    CrossArms(false);
                    break;
                case Blocker.Asleep:
                    SetFace(Face.Asleep);
                    _sign.SetText("");
                    CrossArms(false);
                    break;
                case Blocker.OnBreak:
                    _sign.SetText("ON BREAK");
                    _sign.SetColour(new Color(1f, 0.85f, 0.35f));
                    break;
                case Blocker.Nope:
                    SetFace(Face.Nope);
                    _sign.SetText("NOPE");
                    _sign.SetColour(new Color(1f, 0.35f, 0.32f));
                    CrossArms(true);
                    break;
                default:
                    if (_face == Face.Nope || _face == Face.Asleep) SetFace(Face.Calm);
                    _sign.SetText("");
                    CrossArms(false);
                    break;
            }
        }

        void CrossArms(bool crossed)
        {
            _armL.transform.localRotation = Quaternion.Euler(0, 0, crossed ? 250f : 200f);
            _armR.transform.localRotation = Quaternion.Euler(0, 0, crossed ? -70f : -20f);
        }

        void SetBodyVisible(bool on)
        {
            _outline.enabled = on; _body.enabled = on; _screen.enabled = on;
            _eyeL.enabled = on; _eyeR.enabled = on; _mouth.enabled = on;
            _antenna.enabled = on; _bulb.enabled = on;
            _armL.enabled = on; _armR.enabled = on;
        }

        /// <summary>Drive the look. panic01 ramps the shake, sweat and antenna blink.</summary>
        public void Tick(float dt, float panic01, float bobSpeed = 1f)
        {
            _panic = Mathf.MoveTowards(_panic, Mathf.Clamp01(panic01), dt * 6f);
            bool asleep = _face == Face.Asleep;
            bool dead = _face == Face.Dead;

            // Idle bob. A sleeping robot does NOT bob - that absence is part of the tell.
            if (!asleep && !dead)
            {
                _bobPhase += dt * (2.2f + 5f * _panic) * bobSpeed;
                _bob = 1f;
            }
            float bobY = asleep || dead ? 0f : Mathf.Sin(_bobPhase) * 0.055f;

            // Panic shake uses its own noise, never the gameplay stream.
            float shake = _panic * 0.055f;
            float sx = shake * (Mathf.PerlinNoise(Time.time * 23f, 0.3f) - 0.5f) * 2f;
            float sy = shake * (Mathf.PerlinNoise(0.7f, Time.time * 21f) - 0.5f) * 2f;

            transform.localPosition = _home + new Vector3(sx, bobY + sy, 0f);
            float squash = dead ? 0.6f : 1f + Mathf.Sin(_bobPhase * 2f) * 0.02f;
            float k = _baseScale * (dead ? 0.92f : 1f);
            transform.localScale = new Vector3(k / Mathf.Max(0.2f, squash), k * squash, 1f);

            ApplyFace(dt);
            FadeTells(dt);
        }

        void ApplyFace(float dt)
        {
            Color eye = Color.white;
            Vector3 eyeScaleL = Vector3.one, eyeScaleR = Vector3.one;
            Vector3 mouthScale = Vector3.one;
            Color mouthCol = Color.white;

            switch (_face)
            {
                case Face.Calm:
                    mouthScale = new Vector3(0.8f, 1f, 1f);
                    break;
                case Face.Happy:
                    eyeScaleL = eyeScaleR = new Vector3(1.1f, 0.75f, 1f);
                    mouthScale = new Vector3(1.25f, 1.6f, 1f);
                    mouthCol = new Color(0.55f, 1f, 0.65f);
                    break;
                case Face.Panic:
                    // Shaking eyes: the eyes jitter independently of the body.
                    float j = 0.035f;
                    eyeScaleL = new Vector3(1.25f + Mathf.Sin(Time.time * 40f) * 0.12f, 1.25f, 1f);
                    eyeScaleR = new Vector3(1.25f + Mathf.Cos(Time.time * 37f) * 0.12f, 1.25f, 1f);
                    _eyeL.transform.localPosition = new Vector3(-0.19f + Mathf.Sin(Time.time * 51f) * j, 0.11f, 0f);
                    _eyeR.transform.localPosition = new Vector3(0.19f + Mathf.Cos(Time.time * 47f) * j, 0.11f, 0f);
                    mouthScale = new Vector3(0.7f, 2.0f, 1f);
                    mouthCol = new Color(1f, 0.75f, 0.75f);
                    break;
                case Face.Nope:
                    // Red X eyes: two crossed bars read as an X at this size.
                    eye = new Color(1f, 0.3f, 0.28f);
                    eyeScaleL = new Vector3(1.35f, 0.32f, 1f);
                    eyeScaleR = new Vector3(1.35f, 0.32f, 1f);
                    _eyeL.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                    _eyeR.transform.localRotation = Quaternion.Euler(0, 0, -45f);
                    mouthScale = new Vector3(0.6f, 1f, 1f);
                    mouthCol = new Color(1f, 0.5f, 0.5f);
                    break;
                case Face.Asleep:
                    eyeScaleL = eyeScaleR = new Vector3(1.2f, 0.16f, 1f);
                    mouthScale = new Vector3(0.45f, 1.1f, 1f);
                    break;
                case Face.Dead:
                    eye = new Color(0.55f, 0.55f, 0.58f);
                    eyeScaleL = new Vector3(1.3f, 0.3f, 1f);
                    eyeScaleR = new Vector3(1.3f, 0.3f, 1f);
                    _eyeL.transform.localRotation = Quaternion.Euler(0, 0, 45f);
                    _eyeR.transform.localRotation = Quaternion.Euler(0, 0, -45f);
                    mouthScale = new Vector3(0.9f, 0.5f, 1f);
                    mouthCol = new Color(0.5f, 0.5f, 0.5f);
                    break;
            }

            if (_face != Face.Panic)
            {
                _eyeL.transform.localPosition = new Vector3(-0.19f, 0.11f, 0f);
                _eyeR.transform.localPosition = new Vector3(0.19f, 0.11f, 0f);
            }
            if (_face != Face.Nope && _face != Face.Dead)
            {
                _eyeL.transform.localRotation = Quaternion.identity;
                _eyeR.transform.localRotation = Quaternion.identity;
            }

            _eyeL.color = eye; _eyeR.color = eye;
            _eyeL.transform.localScale = eyeScaleL;
            _eyeR.transform.localScale = eyeScaleR;
            _mouth.transform.localScale = mouthScale;
            _mouth.color = mouthCol;

            // Antenna bulb blinks red, faster the more panicked.
            float blink = _panic > 0.05f
                ? (Mathf.Repeat(Time.time * (2f + 8f * _panic), 1f) < 0.5f ? 1f : 0.25f)
                : 0.5f;
            _bulb.color = Color.Lerp(new Color(0.9f, 0.45f, 0.4f) * 0.7f,
                                     new Color(1f, 0.25f, 0.2f), _panic) * blink + new Color(0,0,0,1);
            _bulb.color = new Color(_bulb.color.r, _bulb.color.g, _bulb.color.b, 1f);

            // Sweat drop appears under real pressure.
            var sc = _sweat.color;
            _sweat.color = new Color(sc.r, sc.g, sc.b, Mathf.Clamp01(_panic * 1.4f - 0.35f));
            _sweat.transform.localPosition = new Vector3(0.44f, 0.30f - Mathf.Repeat(Time.time * 1.6f, 1f) * 0.3f, 0f);
        }

        void FadeTells(float dt)
        {
            // Fading IN can be gentle; fading OUT must be instant, because a shield dropping is
            // the cue the player is waiting for on a both-blocked turn.
            Fade(_shieldArc, _shieldAlpha, dt, _shieldAlpha > 0f ? 10f : 26f);
            Fade(_emptyPad, _emptyAlpha, dt, _emptyAlpha > 0f ? 10f : 26f);
            // The shield arc breathes so it never looks like a static decal.
            if (_shieldAlpha > 0f)
            {
                float p = 1f + Mathf.Sin(Time.time * 6f) * 0.04f;
                _shieldArc.transform.localScale = new Vector3(p, p, 1f);
            }
            if (_zzz != null)
            {
                for (int i = 0; i < _zzz.childCount; i++)
                {
                    var sr = _zzz.GetChild(i).GetComponent<SpriteRenderer>();
                    if (sr == null) continue;
                    float phase = Mathf.Repeat(Time.time * 0.6f + i * 0.33f, 1f);
                    var c = sr.color;
                    float a = _zzzAlpha * Mathf.Sin(phase * Mathf.PI);
                    sr.color = new Color(c.r, c.g, c.b, a);
                    sr.transform.localPosition = new Vector3(i * 0.17f + phase * 0.12f,
                                                             i * 0.20f + phase * 0.35f, 0f);
                }
            }
        }

        static void Fade(SpriteRenderer sr, float target, float dt, float speed)
        {
            var c = sr.color;
            sr.color = new Color(c.r, c.g, c.b, Mathf.MoveTowards(c.a, target, dt * speed));
        }

        /// <summary>A quick squash-and-stretch when the robot catches or throws.</summary>
        public void Pop(float amount = 1f)
        {
            _bobPhase += 0.9f * amount;
        }

        // ---- what is actually on screen, for the automated view checks ----

        /// <summary>Opacity of the shield arc. A shielded robot must get this above zero.</summary>
        public float ShieldAlpha => _shieldArc != null ? _shieldArc.color.a : 0f;
        /// <summary>Opacity of the dashed ON BREAK pad outline.</summary>
        public float EmptyPadAlpha => _emptyPad != null ? _emptyPad.color.a : 0f;
        /// <summary>Peak opacity across the three floating Zzz marks.</summary>
        public float ZzzAlpha
        {
            get
            {
                float a = 0f;
                if (_zzz == null) return 0f;
                for (int i = 0; i < _zzz.childCount; i++)
                {
                    var sr = _zzz.GetChild(i).GetComponent<SpriteRenderer>();
                    if (sr != null && sr.color.a > a) a = sr.color.a;
                }
                return a;
            }
        }
        /// <summary>False when the robot itself is hidden, which is the ON BREAK tell.</summary>
        public bool BodyVisible => _body != null && _body.enabled;
        /// <summary>Eye height. A sleeping robot's eyes are shut, so this collapses.</summary>
        public float EyeScaleY => _eyeL != null ? _eyeL.transform.localScale.y : 1f;
        /// <summary>Eye colour. The NOPE / dead faces turn the eyes red or grey.</summary>
        public Color EyeColour => _eyeL != null ? _eyeL.color : Color.white;
        public string SignText => _sign != null && _sign.Raw != null ? _sign.Raw.text : "";

        public void SetHome(Vector3 p) { _home = p; transform.localPosition = p; }
        public Vector3 Home => _home;
        public void SetSign(string text, Color c) { _sign.SetText(text); _sign.SetColour(c); }
    }
}
