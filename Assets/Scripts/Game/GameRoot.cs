using System.Collections.Generic;
using System.Text;
using UnityEngine;
using KT.ShiftIt.Sim;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// The one bootstrap. It constructs every service in a declared order and hands them out:
    /// no FindObjectOfType, no static Instance reached from gameplay, no Awake-order luck
    /// (pattern 01 item 3). The scene contains exactly one GameObject carrying this component;
    /// everything else in the game is built here at runtime.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        const string PrefBestB = "shiftit.best.modeB";
        const string PrefBestA = "shiftit.best.modeA";
        const string PrefHints = "shiftit.hints";
        const string PrefMode = "shiftit.mode";

        [Tooltip("Leave 0 to seed from the clock. Set a value to reproduce a run exactly.")]
        public int Seed = 0;
        public bool StartMuted = false;
        /// <summary>Set by the automated tests so a run is deterministic and silent.</summary>
        public static bool TestMode;
        /// <summary>
        /// Test seam: when set, this config is used instead of the JSON. The play tests use it to
        /// force a specific scenario (a shield, a dud, a both-blocked turn) through the real game
        /// rather than reaching inside the simulation to fake one.
        /// </summary>
        public static SimConfig ConfigOverride;

        public SimConfig Config { get; private set; }
        public ShiftSim Sim { get; private set; }
        public ShiftInput Input { get; private set; }
        public BoardView Board { get; private set; }
        public HudView Hud { get; private set; }
        public Juice Juice { get; private set; }
        public AudioService Audio { get; private set; }
        public ZoneBackdrop Zones { get; private set; }
        public Camera Cam { get; private set; }

        float _tickAccum;
        bool _focused = true;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;

            // 1. data
            Config = ConfigOverride ?? LoadConfig();

            // 2. camera
            Cam = BuildCamera();

            // 3. simulation
            int seed = Seed != 0 ? Seed : System.Environment.TickCount;
            var mode = (ControlMode)PlayerPrefs.GetInt(PrefMode, (int)ControlMode.LeftRight);
            Sim = new ShiftSim(Config, seed, mode);
            Sim.RestoreShownHints(LoadShownHints());
            Sim.Best = PlayerPrefs.GetInt(BestKey(mode), 0);

            // 4. input
            Input = new ShiftInput();

            // 5. presentation
            // Timed, because every sprite in the game is generated here from 4x4-supersampled
            // coverage and that is the one plausible load hitch in the WebGL build. Measured
            // rather than assumed - if this number is large, Shapes is where to look.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            Zones = ZoneBackdrop.Create(transform, Cam);
            Board = BoardView.Create(transform, Sim);
            Juice = Juice.Create(transform, Cam.transform);
            Hud = HudView.Create(transform, Cam);
            Audio = AudioService.Create(transform, StartMuted || TestMode);

            // 6. wiring that needs everything to exist
            Hud.ModeButton.onClick.AddListener(ToggleMode);
            Hud.SetStickyTip(StickyTipForPlatform());

            Sim.GoToTitle();
            Board.EnsureLayout();
            Zones.Show(0);

            sw.Stop();
            Debug.Log("[SHIFTIT] boot ok. mode=" + mode + " seed=" + seed +
                      " sprites=" + Shapes.CachedCount +
                      " build=" + sw.ElapsedMilliseconds + "ms" +
                      " accessibility=" + WindowsAccessibility.StatusLine);
        }

        static string BestKey(ControlMode m) => m == ControlMode.LeftRight ? PrefBestB : PrefBestA;

        static string StickyTipForPlatform()
        {
            // WebGL cannot turn the Windows shortcuts off, so the player gets told instead.
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                return "Tip: if Windows pops up a Sticky Keys box, click Cancel - this game only taps Shift, never holds it.";
            return "";
        }

        SimConfig LoadConfig()
        {
            var ta = Resources.Load<TextAsset>("shiftit_balance");
            if (ta == null)
            {
                Debug.LogWarning("[SHIFTIT] shiftit_balance.json missing - using built-in defaults.");
                return new SimConfig();
            }
            try
            {
                var cfg = JsonUtility.FromJson<SimConfig>(ta.text);
                if (cfg == null) throw new System.Exception("parsed to null");
                // Validate loudly, naming the field (brief rule 9).
                if (cfg.fuseStart <= 0f || cfg.fuseMin <= 0f || cfg.fuseMin > cfg.fuseStart)
                    Debug.LogError("[SHIFTIT] shiftit_balance.json: fuseStart/fuseMin are not sane.");
                if (cfg.bandLow < 0f || cfg.bandHighStart <= cfg.bandLow)
                    Debug.LogError("[SHIFTIT] shiftit_balance.json: PERFECT band is inverted or empty.");
                if (cfg.maxHoldSeconds >= 8f)
                    Debug.LogError("[SHIFTIT] shiftit_balance.json: maxHoldSeconds must stay under 8 s " +
                                   "or Windows Filter Keys will open mid-game.");
                return cfg;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SHIFTIT] shiftit_balance.json failed to parse (" + e.Message +
                               ") - using built-in defaults.");
                return new SimConfig();
            }
        }

        Camera BuildCamera()
        {
            var go = new GameObject("MainCamera");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -10f);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5.4f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.24f, 0.26f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
            return cam;
        }

        IEnumerable<Hint> LoadShownHints()
        {
            var raw = PlayerPrefs.GetString(PrefHints, "");
            var list = new List<Hint>();
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var part in raw.Split(','))
                if (int.TryParse(part, out var v) && v > 0 && v < (int)Hint.Count) list.Add((Hint)v);
            return list;
        }

        void SaveShownHints()
        {
            var sb = new StringBuilder();
            for (int i = 1; i < (int)Hint.Count; i++)
            {
                if (!Sim.HintWasShown((Hint)i)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(i);
            }
            PlayerPrefs.SetString(PrefHints, sb.ToString());
        }

        public void ToggleMode()
        {
            if (Sim.Phase != Phase.Title) return;
            var next = Sim.Mode == ControlMode.LeftRight ? ControlMode.HoldRelease : ControlMode.LeftRight;
            SetMode(next);
            Audio.Play(Sfx.UiPress);
        }

        public void SetMode(ControlMode mode)
        {
            if (Sim.Mode == mode) return;
            Sim.Mode = mode;
            Sim.Best = PlayerPrefs.GetInt(BestKey(mode), 0);
            PlayerPrefs.SetInt(PrefMode, (int)mode);
            Board.EnsureLayout();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;

            // ---- input -> simulation ----
            Input.Poll();
            if (_focused) Input.Apply(Sim);

            if (Input.ToggleModePressed) ToggleMode();

            // ---- simulation ----
            Sim.Tick(dt);

            // ---- events -> presentation ----
            DrainEvents();

            // ---- presentation ----
            Zones.Tick(dt);
            Board.Tick(dt);
            Juice.Tick(udt);
            Hud.Tick(Sim, dt, udt);

            TickFuseFeedback(dt);
        }

        /// <summary>Ticking that speeds up as the fuse burns, plus sparks off the fuse.</summary>
        void TickFuseFeedback(float dt)
        {
            if (Sim.Phase != Phase.PlayerHolding) { _tickAccum = 0f; return; }
            float f = Sim.FuseFraction;
            if (Sim.Bomb == BombKind.Dud)
            {
                // A dud does not tick: its silence is part of the tell.
                return;
            }
            float interval = Mathf.Lerp(0.07f, 0.40f, Mathf.Clamp01(f));
            _tickAccum += dt;
            if (_tickAccum >= interval)
            {
                _tickAccum = 0f;
                Audio.Play(Sfx.Tick, Mathf.Lerp(1.5f, 0.9f, Mathf.Clamp01(f)), 0.55f);
            }
            Juice.FuseSpark(Board.BombPosition + new Vector3(0.18f, 0.42f, 0f), 1f - f);
        }

        void DrainEvents()
        {
            var ev = Sim.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                switch (e.Kind)
                {
                    case SimEventKind.Catch:
                        Audio.Play(Sfx.Catch);
                        Juice.CatchPuff(Board.BombPosition);
                        Board.PopPlayer();
                        break;

                    case SimEventKind.Throw:
                        Audio.Play(Sfx.Throw);
                        Board.PopSlot(e.Slot);
                        if (e.Quality == ThrowQuality.Perfect)
                        {
                            // Pitch climbs with the combo so the ladder is audible, not just visible.
                            Audio.Play(Sfx.Perfect, 1f + 0.08f * Mathf.Min(Sim.Combo, 6));
                            Juice.PerfectBurst(Board.BombPosition);
                            Hud.ShowPop("PERFECT!", new Color(0.5f, 1f, 0.6f));
                            Hud.ScorePopAt(Cam, Board.BombPosition, "+" + e.IntValue,
                                           new Color(0.55f, 1f, 0.62f));
                        }
                        else if (e.IntValue > 0)
                        {
                            Hud.ScorePopAt(Cam, Board.BombPosition, "+" + e.IntValue, Color.white);
                        }
                        break;

                    case SimEventKind.ReturnThrow:
                        Audio.Play(Sfx.Throw, 0.9f, 0.7f);
                        break;

                    case SimEventKind.Fizzle:
                        Audio.Play(Sfx.DudFizzle);
                        Hud.ShowPop("FIZZLED!", new Color(0.6f, 1f, 0.7f));
                        Hud.ScorePopAt(Cam, Board.BombPosition, "+" + e.IntValue,
                                       new Color(0.6f, 1f, 0.7f));
                        break;

                    case SimEventKind.Open:
                        Audio.Play(Sfx.OpenPop);
                        Audio.Play(Sfx.ShieldClang, 1.2f, 0.6f);
                        Hud.ShowPop("OPEN!", new Color(0.6f, 1f, 0.7f));
                        break;

                    case SimEventKind.Swap:
                        Audio.Play(Sfx.SwapClang);
                        Hud.ShowPop("SWAP!", new Color(1f, 0.7f, 0.3f));
                        Juice.Shake(0.3f);
                        break;

                    case SimEventKind.ZoneChange:
                        Zones.Show(e.IntValue);
                        Hud.ZoneWipe(e.IntValue);
                        Audio.Play(Sfx.ZoneJingle);
                        break;

                    case SimEventKind.HintShown:
                        Hud.ShowHint(HudView.HintLine(e.Hint));
                        break;

                    case SimEventKind.Explode:
                        OnExplode(e.Reason);
                        break;

                    case SimEventKind.GameOver:
                        PlayerPrefs.SetInt(BestKey(Sim.Mode), Sim.Best);
                        SaveShownHints();
                        PlayerPrefs.Save();
                        break;

                    case SimEventKind.Started:
                        Zones.Show(0);
                        break;
                }
            }
            ev.Clear();
        }

        void OnExplode(FailReason reason)
        {
            // Different causes get different sounds. One sound for every failure, differing only in
            // volume, is an explicit red flag in pattern 10.
            switch (reason)
            {
                case FailReason.ThrewAtShield:
                case FailReason.ThrewAtNope:
                    Audio.Play(Sfx.ShieldClang);
                    Juice.ShieldSparks(Board.BombPosition);
                    break;
                case FailReason.ThrewADud:
                    Audio.Play(Sfx.FailBuzz);
                    break;
                case FailReason.ThrewAtSleeper:
                case FailReason.ThrewAtEmpty:
                case FailReason.ReleasedBetween:
                    Audio.Play(Sfx.FailBuzz, 0.85f);
                    break;
            }
            Audio.Play(Sfx.Explosion);
            Juice.Explosion(Board.BombPosition);
            Hud.Flash(1f);
            Board.KillAll();
        }

        void OnApplicationFocus(bool focus)
        {
            _focused = focus;
            if (!focus)
            {
                // Drop every key: a hold must not survive the window losing focus, or the player
                // comes back to a bomb that went off while they were in another window.
                Input.Clear(Sim);
                Audio.StopAll();
            }
        }

        void OnApplicationQuit()
        {
            PlayerPrefs.SetInt(BestKey(Sim.Mode), Sim.Best);
            SaveShownHints();
            PlayerPrefs.Save();
            WindowsAccessibility.RestoreNow();
        }

        /// <summary>Wipe the saved best and hints. Used by the automated tests.</summary>
        public static void ClearSavedProgress()
        {
            PlayerPrefs.DeleteKey(PrefBestA);
            PlayerPrefs.DeleteKey(PrefBestB);
            PlayerPrefs.DeleteKey(PrefHints);
            PlayerPrefs.DeleteKey(PrefMode);
            PlayerPrefs.Save();
        }
    }
}
