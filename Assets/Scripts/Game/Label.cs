using UnityEngine;
using UnityEngine.UI;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// All the game's text, in one place, so swapping the typeface later is one edit.
    ///
    /// Stage 1 uses Unity's built-in runtime font. The chunky display face the brief asks for is a
    /// Stage 2 job, and the plan is to DRAW an original block alphabet as a generated sprite atlas
    /// rather than download a font, so there is never a font licence to argue about (the same reason
    /// every other sprite in this game is generated). See DECISIONS_FOR_ROY.
    /// </summary>
    public sealed class Label : MonoBehaviour
    {
        Text _text;
        RectTransform _rt;
        Outline _outline;

        public RectTransform Rect => _rt;
        public Text Raw => _text;

        static Font _font;
        static string _fontSource = "not loaded";

        /// <summary>
        /// Which font actually resolved. Exposed because a null font is the nastiest possible
        /// failure here: UI Text with no font renders NOTHING, while the `text` property still
        /// returns the right string - so every test that checks what the HUD says would pass
        /// while the player saw a blank screen. That is precisely the failure
        /// KT_QUALITY_LESSONS B4 warns about, so it is asserted rather than assumed.
        /// </summary>
        public static string FontSource => _fontSource;
        public static bool FontLoaded => _font != null;

        static Font BuiltinFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _fontSource = _font != null ? "LegacyRuntime.ttf" : null;
            if (_font == null)
            {
                _font = Font.CreateDynamicFontFromOSFont("Arial", 48);
                _fontSource = _font != null ? "OS Arial (fallback)" : "NONE - TEXT WILL BE INVISIBLE";
            }
            if (_font == null)
                Debug.LogError("[SHIFTIT] no font could be loaded. All UI text will render blank.");
            return _font;
        }

        /// <summary>A label on an existing canvas (the HUD).</summary>
        public static Label Create(Transform parent, string name, int size, Color colour,
                                   TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Label>();
            l.Init(size, colour, anchor);
            return l;
        }

        /// <summary>
        /// A label that lives in the world, above a robot or the bomb. It gets its own tiny
        /// world-space canvas so it scales with the object it belongs to.
        /// </summary>
        public static Label CreateWorld(Transform parent, string name, Vector3 localPos,
                                        int size, Color colour)
        {
            var holder = new GameObject(name + "Canvas");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;

            var canvas = holder.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            var crt = holder.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(420, 90);
            holder.transform.localScale = Vector3.one * 0.0045f;   // canvas px -> world units

            var l = Create(holder.transform, name, size, colour);
            l._rt.anchorMin = l._rt.anchorMax = new Vector2(0.5f, 0.5f);
            l._rt.sizeDelta = new Vector2(420, 90);
            l._rt.anchoredPosition = Vector2.zero;
            return l;
        }

        void Init(int size, Color colour, TextAnchor anchor)
        {
            _text = gameObject.AddComponent<Text>();
            _text.font = BuiltinFont();
            _text.fontSize = size;
            _text.fontStyle = FontStyle.Bold;
            _text.color = colour;
            _text.alignment = anchor;
            _text.horizontalOverflow = HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;
            // A heavy dark outline is what makes text read over a busy background, and it is also
            // the look the brief asks for.
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(0.05f, 0.05f, 0.07f, 0.95f);
            _outline.effectDistance = new Vector2(3f, -3f);
            _rt = GetComponent<RectTransform>();
        }

        public Label SetText(string s) { if (_text.text != s) _text.text = s; return this; }
        public Label SetColour(Color c) { _text.color = c; return this; }
        public Label SetSize(int s) { _text.fontSize = s; return this; }
        public Label SetOutline(float px)
        {
            _outline.effectDistance = new Vector2(px, -px);
            return this;
        }
        /// <summary>
        /// Let this label wrap instead of overflowing. Labels default to overflow because almost
        /// every one of them is a short HUD readout that must never reflow mid-game; the Sticky
        /// Keys tip is a real sentence and needs the opposite.
        /// </summary>
        public Label SetWrap(bool on)
        {
            _text.horizontalOverflow = on ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            return this;
        }

        public Label SetAlpha(float a)
        {
            var c = _text.color; _text.color = new Color(c.r, c.g, c.b, a);
            var o = _outline.effectColor; _outline.effectColor = new Color(o.r, o.g, o.b, 0.95f * a);
            return this;
        }
        public Label SetAnchored(Vector2 anchorMinMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            _rt.anchorMin = _rt.anchorMax = anchorMinMax;
            _rt.pivot = pivot;
            _rt.anchoredPosition = pos;
            _rt.sizeDelta = size;
            return this;
        }
        public void SetActive(bool on) { if (gameObject.activeSelf != on) gameObject.SetActive(on); }
    }
}
