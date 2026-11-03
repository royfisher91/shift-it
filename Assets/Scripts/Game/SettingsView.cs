using UnityEngine;
using UnityEngine.UI;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// Music volume, sound volume and a fullscreen toggle.
    ///
    /// Everything here is driven by the mouse. The brief's rule is that the Shift keys run the
    /// GAME, and that menus and settings may also take the mouse - so a settings screen with real
    /// buttons breaks nothing, and it is the only sensible way to offer a volume control in a game
    /// with two keys.
    ///
    /// Volumes are stepped 0-10 with minus and plus buttons rather than a drag slider: the steps
    /// suit the chunky art, they are easier to hit, and they work identically on a touchscreen.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        public const int Steps = 10;

        // Laid out inside a box this wide, so every row has room at both ends.
        const float BoxW = 1150f, BoxH = 700f;

        GameObject _panel;
        BlockText _musicValue, _sfxValue, _fullscreenValue;
        Image[] _musicBar, _sfxBar;

        public Button MusicDown { get; private set; }
        public Button MusicUp { get; private set; }
        public Button SfxDown { get; private set; }
        public Button SfxUp { get; private set; }
        public Button FullscreenToggle { get; private set; }
        public Button CloseButton { get; private set; }

        public bool Shown => _panel != null && _panel.activeSelf;

        public static SettingsView Create(Transform parent)
        {
            var go = new GameObject("Settings");
            go.transform.SetParent(parent, false);

            // The root needs a RectTransform of its own, stretched to the canvas. A RectTransform
            // child of a PLAIN Transform under a Canvas has no parent rect to anchor against, so
            // the full-screen dim below quietly covered nothing at all and the whole panel floated
            // on top of a fully visible title screen.
            var rrt = go.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;

            var v = go.AddComponent<SettingsView>();
            v.Build();
            return v;
        }

        void Build()
        {
            _panel = new GameObject("SettingsPanel");
            _panel.transform.SetParent(transform, false);
            var rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            // Full-screen dim, so the game behind reads as "paused and not yours right now".
            var dim = new GameObject("Dim");
            dim.transform.SetParent(_panel.transform, false);
            var di = dim.AddComponent<Image>();
            di.sprite = Shapes.Block();
            di.color = new Color(0.02f, 0.03f, 0.05f, 0.88f);
            var drt = dim.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;

            // A solid box, so this reads as a dialog rather than text floating over the game.
            var box = new GameObject("Box");
            box.transform.SetParent(_panel.transform, false);
            var bi = box.AddComponent<Image>();
            bi.sprite = Shapes.RoundedRect(1150, 700, 28);
            bi.color = new Color(0.10f, 0.13f, 0.18f, 0.98f);
            var brt = box.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(BoxW, BoxH);
            brt.anchoredPosition = Vector2.zero;

            var edge = new GameObject("BoxEdge");
            edge.transform.SetParent(_panel.transform, false);
            var ei = edge.AddComponent<Image>();
            ei.sprite = Shapes.RoundedRectOutline(1150, 700, 28, 6);
            ei.color = new Color(0.30f, 0.38f, 0.48f, 1f);
            ei.raycastTarget = false;
            var ert = edge.GetComponent<RectTransform>();
            ert.anchorMin = ert.anchorMax = new Vector2(0.5f, 0.5f);
            ert.sizeDelta = new Vector2(BoxW, BoxH);
            ert.anchoredPosition = Vector2.zero;

            BlockText.Create(_panel.transform, "SettingsTitle", 72, new Color(1f, 0.82f, 0.16f))
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(0f, 250f), new Vector2(1000f, 100f))
                .SetText("SETTINGS");

            Button md, mu, sd, su;
            _musicBar = Row(_panel.transform, "Music", "MUSIC", 95f, out _musicValue, out md, out mu);
            MusicDown = md; MusicUp = mu;
            _sfxBar = Row(_panel.transform, "Sfx", "SOUND", -25f, out _sfxValue, out sd, out su);
            SfxDown = sd; SfxUp = su;

            // Fullscreen: a label and one button that states its own current state.
            BlockText.Create(_panel.transform, "FullLabel", 40, new Color(0.86f, 0.90f, 0.96f),
                             TextAnchor.MiddleLeft)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-340f, -150f), new Vector2(340f, 70f))
                .SetText("FULLSCREEN");
            FullscreenToggle = MakeButton(_panel.transform, "FullToggle", new Vector2(160f, -150f),
                                          new Vector2(300f, 82f), new Color(0.24f, 0.34f, 0.44f));
            _fullscreenValue = BlockText.Create(FullscreenToggle.transform, "FullValue", 38, Color.white);
            _fullscreenValue.SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                         Vector2.zero, new Vector2(300f, 82f));

            CloseButton = MakeButton(_panel.transform, "Close", new Vector2(0f, -268f),
                                     new Vector2(360f, 92f), new Color(0.22f, 0.46f, 0.30f));
            BlockText.Create(CloseButton.transform, "CloseText", 42, Color.white)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(360f, 92f))
                .SetText("BACK");

            _panel.SetActive(false);
        }

        /// <summary>One labelled volume row: name, minus, a ten-segment bar, plus, and the number.</summary>
        Image[] Row(Transform parent, string id, string label, float y,
                    out BlockText value, out Button down, out Button up)
        {
            BlockText.Create(parent, id + "Label", 40, new Color(0.86f, 0.90f, 0.96f), TextAnchor.MiddleLeft)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             new Vector2(-340f, y), new Vector2(340f, 70f))
                .SetText(label);

            down = MakeButton(parent, id + "Down", new Vector2(-110f, y), new Vector2(78f, 78f),
                              new Color(0.26f, 0.32f, 0.38f));
            BlockText.Create(down.transform, "t", 38, Color.white)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(78f, 78f))
                .SetText("-");

            var bar = new Image[Steps];
            const float segW = 30f, gap = 6f;
            const float x0 = -30f;
            for (int i = 0; i < Steps; i++)
            {
                var g = new GameObject(id + "Seg" + i);
                g.transform.SetParent(parent, false);
                var im = g.AddComponent<Image>();
                im.sprite = Shapes.RoundedRect(30, 52, 8);
                im.raycastTarget = false;
                var r = g.GetComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(segW, 52f);
                r.anchoredPosition = new Vector2(x0 + i * (segW + gap), y);
                bar[i] = im;
            }

            up = MakeButton(parent, id + "Up", new Vector2(355f, y), new Vector2(78f, 78f),
                            new Color(0.26f, 0.32f, 0.38f));
            BlockText.Create(up.transform, "t", 38, Color.white)
                .SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                             Vector2.zero, new Vector2(78f, 78f))
                .SetText("+");

            value = BlockText.Create(parent, id + "Value", 40, new Color(1f, 0.86f, 0.35f));
            value.SetAnchored(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                              new Vector2(460f, y), new Vector2(140f, 70f));
            return bar;
        }

        static Button MakeButton(Transform parent, string name, Vector2 pos, Vector2 size, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = Shapes.RoundedRect(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 16);
            img.color = colour;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var c = btn.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            c.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            c.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            btn.colors = c;
            return btn;
        }

        // =====================================================================

        public void Show(bool on)
        {
            if (_panel.activeSelf != on) _panel.SetActive(on);
        }

        /// <summary>Redraw the three readouts from the live values.</summary>
        public void Refresh(int music, int sfx, bool fullscreen)
        {
            Paint(_musicBar, music, new Color(0.45f, 0.82f, 1f));
            Paint(_sfxBar, sfx, new Color(0.55f, 0.95f, 0.6f));
            _musicValue.SetText(music.ToString());
            _sfxValue.SetText(sfx.ToString());
            _fullscreenValue.SetText(fullscreen ? "ON" : "OFF");
        }

        static void Paint(Image[] bar, int level, Color on)
        {
            var off = new Color(0.20f, 0.24f, 0.28f, 1f);
            for (int i = 0; i < bar.Length; i++) bar[i].color = i < level ? on : off;
        }

        // Read back for the automated tests.
        public string MusicValueText => _musicValue != null ? _musicValue.Text : "";
        public string SfxValueText => _sfxValue != null ? _sfxValue.Text : "";
        public string FullscreenValueText => _fullscreenValue != null ? _fullscreenValue.Text : "";
    }
}
