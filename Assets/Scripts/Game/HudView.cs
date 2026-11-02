using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using KT.ShiftIt.Sim;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// Every pixel of UI: the HUD, the title screen, the game-over card, the one-time hint line and
    /// the zone-change wipe.
    ///
    /// All of it is built in code onto one overlay canvas scaled to a 1920x1080 reference, so it is
    /// identical in the editor, in the Windows build and in a browser at any window size.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public static readonly string[] ZoneNames = { "FACTORY FLOOR", "ROOFTOP", "SCRAPYARD", "LAUNCHPAD" };

        Canvas _canvas;
        RectTransform _root;
        Camera _cam;

        // HUD
        Label _score, _combo, _best, _zone, _passes;
        // Title
        GameObject _titlePanel;
        Label _logo, _tagline, _modeLine, _startLine, _modeHelp;
        // The Sticky Keys tip: a small strip the player can dismiss for good.
        GameObject _tipPanel;
        Label _tipText;
        public Button TipDismissButton { get; private set; }
        // Game over
        GameObject _overPanel;
        Label _overTitle, _overReason, _overScore, _overBest, _overRetry;
        // Quit confirm
        GameObject _quitPanel;
        Label _quitTitle, _quitHelp;
        public Button QuitYesButton { get; private set; }
        public Button QuitNoButton { get; private set; }
        // Transient
        Label _hint, _banner, _centrePop;
        Image _wipe, _flash;
        RectTransform _wipeRt;

        float _hintTimer, _bannerTimer, _popTimer;
        float _wipeT = -1f;
        float _flashA;
        string _pendingBanner;

        readonly List<ScorePop> _pops = new List<ScorePop>();

        sealed class ScorePop
        {
            public Label L; public float T; public Vector2 From;
        }

        public Canvas Canvas => _canvas;

        public static HudView Create(Transform parent, Camera cam)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<HudView>();
            h.Build(cam);
            return h;
        }

        void Build(Camera cam)
        {
            _canvas = gameObject.AddComponent<Canvas>();
            // ScreenSpaceCamera, not Overlay, for one practical reason: an Overlay canvas is drawn
            // straight to the backbuffer and does NOT appear when a camera is rendered into a
            // RenderTexture. Every automated screenshot would then show the game with no UI on it,
            // which is exactly the kind of evidence that looks fine and proves nothing
            // (KT_QUALITY_LESSONS B4: a check must read what the player sees).
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = cam;
            _cam = cam;
            _canvas.planeDistance = 1f;
            _canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            _root = GetComponent<RectTransform>();

            // ---------- HUD ----------
            _score = Label.Create(_root, "Score", 96, Color.white, TextAnchor.UpperLeft)
                .SetAnchored(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -30f), new Vector2(560f, 110f));
            _combo = Label.Create(_root, "Combo", 48, new Color(0.45f, 1f, 0.55f), TextAnchor.UpperLeft)
                .SetAnchored(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -132f), new Vector2(560f, 64f));
            _best = Label.Create(_root, "Best", 52, new Color(0.95f, 0.88f, 0.55f), TextAnchor.UpperRight)
                .SetAnchored(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -34f), new Vector2(560f, 70f));
            _zone = Label.Create(_root, "Zone", 54, new Color(0.88f, 0.93f, 1f), TextAnchor.UpperCenter)
                .SetAnchored(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(900f, 70f));
            _passes = Label.Create(_root, "Passes", 36, new Color(0.72f, 0.78f, 0.9f), TextAnchor.UpperCenter)
                .SetAnchored(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -84f), new Vector2(760f, 46f));

            // ---------- transient ----------
            // Sits high and clear: it used to overlap the PERFECT pop at y=150 and, in Mode A,
            // the topmost robot of the arc.
            _hint = Label.Create(_root, "Hint", 54, new Color(1f, 0.95f, 0.5f))
                .SetAnchored(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(1600f, 84f));
            _banner = Label.Create(_root, "Banner", 110, Color.white)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(1900f, 160f));
            _centrePop = Label.Create(_root, "CentrePop", 92, new Color(0.5f, 1f, 0.6f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(1200f, 130f));
            _hint.SetAlpha(0f); _banner.SetAlpha(0f); _centrePop.SetAlpha(0f);

            // Zone-change hazard wipe: a wide striped bar that sweeps across the screen.
            // Hazard stripes, not a slab: a row of alternating yellow and black diagonal bars
            // that sweeps across. The first version was a single solid yellow rectangle 760 px
            // wide, which read as a wall dropping over the game rather than a wipe.
            var wipeGo = new GameObject("Wipe");
            wipeGo.transform.SetParent(_root, false);
            _wipeRt = wipeGo.AddComponent<RectTransform>();
            _wipeRt.anchorMin = _wipeRt.anchorMax = new Vector2(0.5f, 0.5f);
            _wipeRt.sizeDelta = new Vector2(620f, 2600f);
            _wipeRt.localEulerAngles = new Vector3(0f, 0f, 18f);
            _wipe = wipeGo.AddComponent<Image>();
            _wipe.sprite = Shapes.Block();
            _wipe.color = new Color(0.07f, 0.07f, 0.09f, 1f);
            _wipe.raycastTarget = false;
            for (int i = 0; i < 7; i++)
            {
                var bar = new GameObject("Stripe" + i);
                bar.transform.SetParent(wipeGo.transform, false);
                var bi = bar.AddComponent<Image>();
                bi.sprite = Shapes.Block();
                bi.color = new Color(1f, 0.78f, 0.1f, 1f);
                bi.raycastTarget = false;
                var brt = bar.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
                brt.sizeDelta = new Vector2(44f, 2600f);
                brt.anchoredPosition = new Vector2(-270f + i * 88f, 0f);
            }
            wipeGo.SetActive(false);

            // Full-screen white flash for the explosion.
            var flashGo = new GameObject("Flash");
            flashGo.transform.SetParent(_root, false);
            _flash = flashGo.AddComponent<Image>();
            _flash.sprite = Shapes.Block();
            _flash.color = new Color(1f, 1f, 1f, 0f);
            _flash.raycastTarget = false;
            var frt = flashGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;

            BuildTitle();
            BuildGameOver();
            BuildQuitConfirm();
        }

        void BuildTitle()
        {
            _titlePanel = new GameObject("Title");
            _titlePanel.transform.SetParent(_root, false);
            var rt = _titlePanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var dim = new GameObject("Dim");
            dim.transform.SetParent(_titlePanel.transform, false);
            var di = dim.AddComponent<Image>();
            di.sprite = Shapes.Block();
            di.color = new Color(0.03f, 0.04f, 0.06f, 0.55f);
            di.raycastTarget = false;
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            _logo = Label.Create(_titlePanel.transform, "Logo", 190, new Color(1f, 0.82f, 0.16f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1800f, 260f))
                .SetOutline(7f);
            _logo.SetText("SHIFT IT!");
            _tagline = Label.Create(_titlePanel.transform, "Tagline", 54, new Color(0.92f, 0.94f, 1f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(1700f, 80f));
            _tagline.SetText("Pass the bomb. Don't be holding it.");
            _modeLine = Label.Create(_titlePanel.transform, "Mode", 58, new Color(0.55f, 0.9f, 1f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(1700f, 80f));
            _modeHelp = Label.Create(_titlePanel.transform, "ModeHelp", 38, new Color(0.75f, 0.8f, 0.9f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -92f), new Vector2(1700f, 60f));
            // With Mode A off there is nothing to switch to, so the line becomes a plain control
            // reminder instead of a toggle, and the mouse target goes with it.
            _modeHelp.SetText(GameRoot.ModeAEnabled ? "TAB or click to change mode" : "");
            _startLine = Label.Create(_titlePanel.transform, "Start", 72, Color.white)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(1700f, 100f));
            _startLine.SetText("PRESS SHIFT TO START");
            BuildStickyTip();

            // The mode line is a real button so the title screen works with the mouse alone.
            var btn = _modeLine.gameObject.AddComponent<Button>();
            _modeLine.Raw.raycastTarget = GameRoot.ModeAEnabled;
            btn.interactable = GameRoot.ModeAEnabled;
            btn.transition = Selectable.Transition.None;
            ModeButton = btn;
        }

        public Button ModeButton { get; private set; }

        void BuildGameOver()
        {
            _overPanel = new GameObject("GameOver");
            _overPanel.transform.SetParent(_root, false);
            var rt = _overPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var dim = new GameObject("Dim");
            dim.transform.SetParent(_overPanel.transform, false);
            var di = dim.AddComponent<Image>();
            di.sprite = Shapes.Block();
            di.color = new Color(0.04f, 0.03f, 0.05f, 0.68f);
            di.raycastTarget = false;
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            _overTitle = Label.Create(_overPanel.transform, "OverTitle", 150, new Color(1f, 0.42f, 0.32f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1800f, 200f))
                .SetOutline(6f);
            _overTitle.SetText("BOOM!");
            _overReason = Label.Create(_overPanel.transform, "OverReason", 56, new Color(1f, 0.92f, 0.8f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1800f, 90f));
            _overScore = Label.Create(_overPanel.transform, "OverScore", 80, Color.white)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1800f, 110f));
            _overBest = Label.Create(_overPanel.transform, "OverBest", 46, new Color(0.95f, 0.88f, 0.55f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1800f, 70f));
            _overRetry = Label.Create(_overPanel.transform, "OverRetry", 68, Color.white)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(1800f, 100f));
            _overRetry.SetText("PRESS SHIFT TO RETRY");
            _overPanel.SetActive(false);
        }

        /// <summary>
        /// A small, friendly strip along the bottom of the title screen warning browser players
        /// about the Windows Sticky Keys beep, with a GOT IT that remembers the answer.
        ///
        /// It must not get in the way: it sits below everything, it never takes Shift, and pressing
        /// Shift starts the game whether or not it has been read. Dismissing it is a mouse click,
        /// which is fine because the title screen already has to work with the mouse alone.
        /// </summary>
        void BuildStickyTip()
        {
            _tipPanel = new GameObject("StickyTip");
            _tipPanel.transform.SetParent(_titlePanel.transform, false);
            var prt = _tipPanel.AddComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.sizeDelta = new Vector2(1580f, 88f);
            prt.anchoredPosition = new Vector2(0f, 44f);

            var bg = _tipPanel.AddComponent<Image>();
            // Generated at roughly the aspect it is drawn at (1580x88). A stretched sprite keeps
            // its corner radius only if the source is close to the target shape; a 3:1 source
            // stretched to 18:1 smears the rounded corners sideways.
            bg.sprite = Shapes.RoundedRect(632, 36, 9);
            bg.color = new Color(0.07f, 0.09f, 0.11f, 0.72f);
            bg.raycastTarget = false;

            _tipText = Label.Create(_tipPanel.transform, "TipText", 30,
                                    new Color(0.80f, 0.85f, 0.93f), TextAnchor.MiddleLeft);
            var trt = _tipText.Rect;
            trt.anchorMin = new Vector2(0f, 0.5f);
            trt.anchorMax = new Vector2(0f, 0.5f);
            trt.pivot = new Vector2(0f, 0.5f);
            trt.sizeDelta = new Vector2(1280f, 80f);
            trt.anchoredPosition = new Vector2(26f, 0f);
            _tipText.SetOutline(2f).SetWrap(true);

            var b = new GameObject("TipDismiss");
            b.transform.SetParent(_tipPanel.transform, false);
            var bi = b.AddComponent<Image>();
            bi.sprite = Shapes.RoundedRect(220, 70, 14);
            bi.color = new Color(0.26f, 0.32f, 0.38f, 0.95f);
            var brt = b.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
            brt.pivot = new Vector2(1f, 0.5f);
            brt.sizeDelta = new Vector2(220f, 62f);
            brt.anchoredPosition = new Vector2(-24f, 0f);

            var btn = b.AddComponent<Button>();
            btn.targetGraphic = bi;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            c.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = c;
            TipDismissButton = btn;

            var bl = Label.Create(b.transform, "TipDismissText", 30, Color.white);
            bl.SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 62f));
            bl.SetText("GOT IT");

            _tipPanel.SetActive(false);
        }

        void BuildQuitConfirm()
        {
            _quitPanel = new GameObject("QuitConfirm");
            _quitPanel.transform.SetParent(_root, false);
            var rt = _quitPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var dim = new GameObject("Dim");
            dim.transform.SetParent(_quitPanel.transform, false);
            var di = dim.AddComponent<Image>();
            di.sprite = Shapes.Block();
            di.color = new Color(0.02f, 0.02f, 0.04f, 0.78f);
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            _quitTitle = Label.Create(_quitPanel.transform, "QuitTitle", 110, new Color(1f, 0.82f, 0.16f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1700f, 160f))
                .SetOutline(6f);
            _quitTitle.SetText("QUIT SHIFT IT?");
            _quitHelp = Label.Create(_quitPanel.transform, "QuitHelp", 40, new Color(0.78f, 0.82f, 0.9f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -150f), new Vector2(1700f, 70f));
            _quitHelp.SetText("ESC again to quit     SHIFT to carry on     or click");

            QuitYesButton = MakeButton(_quitPanel.transform, "QuitYes", "QUIT",
                                       new Vector2(-230f, 30f), new Color(0.72f, 0.22f, 0.18f));
            QuitNoButton = MakeButton(_quitPanel.transform, "QuitNo", "KEEP PLAYING",
                                      new Vector2(230f, 30f), new Color(0.22f, 0.46f, 0.30f));
            _quitPanel.SetActive(false);
        }

        /// <summary>A real clickable button with hover and press states, per the standing KT rule.</summary>
        Button MakeButton(Transform parent, string name, string text, Vector2 pos, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = Shapes.RoundedRect(360, 110, 18);
            img.color = colour;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 120f);
            rt.anchoredPosition = pos;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var colours = btn.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colours.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colours.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = colours;

            var l = Label.Create(go.transform, "Text", 46, Color.white);
            l.SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 120f));
            l.SetText(text);
            return btn;
        }

        public void ShowQuitConfirm(bool on)
        {
            if (_quitPanel.activeSelf != on) _quitPanel.SetActive(on);
        }

        public bool QuitConfirmShown => _quitPanel != null && _quitPanel.activeSelf;

        // =====================================================================

        /// <summary>Set the tip's wording and whether it is on screen at all.</summary>
        public void SetStickyTip(string text, bool show)
        {
            _tipText.SetText(text);
            if (_tipPanel.activeSelf != show) _tipPanel.SetActive(show);
        }

        public bool StickyTipShown => _tipPanel != null && _tipPanel.activeSelf;
        public string StickyTipText => _tipText != null ? _tipText.Raw.text : "";

        // ---- what the player can actually read, for the automated view checks ----
        public string ScoreText => _score.Raw.text;
        public string ComboText => _combo.gameObject.activeSelf ? _combo.Raw.text : "";
        public string BestText => _best.Raw.text;
        public string ZoneText => _zone.Raw.text;
        public string PassesText => _passes.Raw.text;
        public string HintText => _hint.Raw.color.a > 0.05f ? _hint.Raw.text : "";
        public string ReasonText => _overReason.Raw.text;
        public bool TitleShown => _titlePanel.activeSelf;
        public bool GameOverShown => _overPanel.activeSelf;
        public bool ScoreShown => _score.gameObject.activeSelf;

        public void Tick(ShiftSim sim, float dt, float unscaledDt)
        {
            bool playing = sim.Phase != Phase.Title && sim.Phase != Phase.GameOver;

            _score.SetText(sim.Score.ToString());
            _score.SetActive(playing);
            _combo.SetActive(playing && sim.Combo > 0);
            if (sim.Combo > 0) _combo.SetText("COMBO x" + sim.Combo);
            _best.SetText("BEST " + sim.Best);
            _best.SetActive(sim.Phase != Phase.Title);

            int z = Mathf.Clamp(sim.Zone, 0, ZoneNames.Length - 1);
            _zone.SetText(ZoneNames[z]);
            _zone.SetActive(playing);
            _passes.SetText(sim.Passes == 1 ? "1 PASS" : sim.Passes + " PASSES");
            _passes.SetActive(playing);

            // Title
            bool title = sim.Phase == Phase.Title;
            if (_titlePanel.activeSelf != title) _titlePanel.SetActive(title);
            if (title)
            {
                if (GameRoot.ModeAEnabled)
                {
                    _modeLine.SetText(sim.Mode == ControlMode.LeftRight
                        ? "MODE B  -  LEFT / RIGHT SHIFT"
                        : "MODE A  -  HOLD & RELEASE");
                }
                else
                {
                    _modeLine.SetText("LEFT SHIFT throws left     RIGHT SHIFT throws right");
                }
                // Blinking start prompt.
                float b = Mathf.Repeat(Time.unscaledTime * 1.6f, 1f) < 0.62f ? 1f : 0.18f;
                _startLine.SetAlpha(b);
            }

            // Game over
            bool over = sim.Phase == Phase.GameOver;
            if (_overPanel.activeSelf != over) _overPanel.SetActive(over);
            if (over)
            {
                _overReason.SetText(ReasonLine(sim.Fail));
                _overScore.SetText("SCORE " + sim.Score + "    PASSES " + sim.Passes);
                _overBest.SetText(sim.Score >= sim.Best && sim.Score > 0
                    ? "NEW PERSONAL BEST!" : "BEST " + sim.Best);
                float b = Mathf.Repeat(Time.unscaledTime * 1.8f, 1f) < 0.6f ? 1f : 0.2f;
                _overRetry.SetAlpha(b);
            }

            TickTransients(dt, unscaledDt);
        }

        void TickTransients(float dt, float unscaledDt)
        {
            // hint
            if (_hintTimer > 0f)
            {
                _hintTimer -= unscaledDt;
                float a = Mathf.Clamp01(_hintTimer < 0.5f ? _hintTimer / 0.5f : 1f);
                _hint.SetAlpha(a);
                _hint.Rect.anchoredPosition = new Vector2(0f, 96f - (1f - a) * 14f);
            }
            else _hint.SetAlpha(0f);

            // zone banner
            if (_bannerTimer > 0f)
            {
                _bannerTimer -= unscaledDt;
                float t = 1f - Mathf.Clamp01(_bannerTimer / 1.5f);
                // Slides in from the left, holds, slides out to the right.
                float x = t < 0.25f ? Mathf.Lerp(-1900f, 0f, t / 0.25f)
                        : t < 0.72f ? 0f
                        : Mathf.Lerp(0f, 1900f, (t - 0.72f) / 0.28f);
                _banner.Rect.anchoredPosition = new Vector2(x, 90f);
                _banner.SetAlpha(1f);
            }
            else _banner.SetAlpha(0f);

            // centre pop (PERFECT, SWAP!, OPEN!)
            if (_popTimer > 0f)
            {
                _popTimer -= unscaledDt;
                float t = 1f - Mathf.Clamp01(_popTimer / 0.6f);
                _centrePop.SetAlpha(Mathf.Clamp01(1f - t * t));
                float s = 1f + 0.35f * Mathf.Sin(Mathf.Clamp01(t * 3f) * Mathf.PI * 0.5f);
                _centrePop.Rect.localScale = new Vector3(s, s, 1f);
                _centrePop.Rect.anchoredPosition = new Vector2(0f, 150f + t * 60f);
            }
            else _centrePop.SetAlpha(0f);

            // hazard wipe
            if (_wipeT >= 0f)
            {
                _wipeT += unscaledDt / 0.85f;
                if (_wipeT >= 1f)
                {
                    _wipeT = -1f;
                    _wipe.gameObject.SetActive(false);
                }
                else
                {
                    _wipe.gameObject.SetActive(true);
                    _wipeRt.anchoredPosition = new Vector2(Mathf.Lerp(-2400f, 2400f, _wipeT), 0f);
                    // Fire the banner as the bar passes the middle.
                    if (_pendingBanner != null && _wipeT > 0.45f)
                    {
                        _banner.SetText(_pendingBanner);
                        _bannerTimer = 1.5f;
                        _pendingBanner = null;
                    }
                }
            }

            // flash
            if (_flashA > 0f)
            {
                // Was 0.85 alpha decaying at 3.2/s, which held the screen near-white for about a
                // quarter of a second and hid the explosion it was supposed to punctuate.
                _flashA = Mathf.MoveTowards(_flashA, 0f, unscaledDt * 6.5f);
                _flash.color = new Color(1f, 1f, 1f, _flashA * 0.42f);
            }

            // floating score pops
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.T += unscaledDt;
                float t = p.T / 0.85f;
                if (t >= 1f)
                {
                    Destroy(p.L.gameObject);
                    _pops.RemoveAt(i);
                    continue;
                }
                p.L.Rect.anchoredPosition = p.From + new Vector2(0f, t * 120f);
                p.L.SetAlpha(1f - t * t);
                float s = 1f + 0.3f * Mathf.Sin(Mathf.Clamp01(t * 4f) * Mathf.PI * 0.5f);
                p.L.Rect.localScale = new Vector3(s, s, 1f);
            }
        }

        // ---- triggers ----

        public void ShowHint(string s, float seconds = 2.2f)
        {
            _hint.SetText(s);
            _hintTimer = seconds;
        }

        public void ShowPop(string s, Color c)
        {
            _centrePop.SetText(s);
            _centrePop.SetColour(c);
            _popTimer = 0.6f;
        }

        public void ZoneWipe(int zone)
        {
            int z = Mathf.Clamp(zone, 0, ZoneNames.Length - 1);
            _pendingBanner = "SHIFT!  " + ZoneNames[z];
            _wipeT = 0f;
        }

        public void Flash(float amount = 1f) => _flashA = Mathf.Max(_flashA, amount);

        /// <summary>A floating "+4" at a world point, converted to canvas space.</summary>
        public void ScorePopAt(Camera cam, Vector3 worldPos, string text, Color colour)
        {
            Vector2 screen = cam.WorldToScreenPoint(worldPos);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out local);
            var l = Label.Create(_root, "Pop", 70, colour);
            l.SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), local, new Vector2(400f, 90f));
            l.SetText(text);
            _pops.Add(new ScorePop { L = l, T = 0f, From = local });
        }

        public static string ReasonLine(FailReason r)
        {
            switch (r)
            {
                case FailReason.FuseRanOut: return "The fuse ran out in your hands.";
                case FailReason.ThrewAtShield: return "That one had its shield up.";
                case FailReason.ThrewAtSleeper: return "That one was fast asleep.";
                case FailReason.ThrewAtEmpty: return "Nobody there. The bomb hit the floor.";
                case FailReason.ThrewADud: return "That was a dud. You should have held on to it.";
                case FailReason.HeldTooLong: return "You held on far too long.";
                case FailReason.ThrewAtNope: return "That one was not having it.";
                case FailReason.ReleasedBetween: return "You threw it at thin air.";
                default: return "It went off.";
            }
        }

        public static string HintLine(Hint h)
        {
            switch (h)
            {
                case Hint.FirstCatchModeB: return "LEFT or RIGHT SHIFT!";
                case Hint.FirstCatchModeA_Hold: return "HOLD SHIFT";
                case Hint.FirstCatchModeA_Release: return "RELEASE ON A ROBOT!";
                case Hint.Shield: return "SHIELD UP! THROW THE OTHER WAY";
                case Hint.Asleep: return "THAT ONE'S ASLEEP! THROW THE OTHER WAY";
                case Hint.OnBreak: return "NOBODY HOME! THROW THE OTHER WAY";
                case Hint.Hot: return "RED HOT! HALF THE FUSE, DOUBLE POINTS";
                case Hint.Mirrored: return "MIRRORED! THE KEYS ARE SWAPPED";
                case Hint.Dud: return "GREEN DUD! DON'T THROW IT - HOLD IT";
                case Hint.BothBlocked: return "BOTH BLOCKED! WAIT FOR ONE TO OPEN";
                case Hint.Swap: return "IT CAN SWAP SIDES! KEEP WATCHING";
                case Hint.DontHoldShift: return "TAP SHIFT - DON'T HOLD IT DOWN";
                default: return "";
            }
        }
    }
}
