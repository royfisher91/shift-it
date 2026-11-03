using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// Draws a string in the generated block display face by laying out one Image per glyph.
    ///
    /// It deliberately mirrors the bits of <see cref="Label"/> the HUD actually uses, so a readout
    /// can be moved onto the display face by changing which Create call builds it and nothing else.
    /// Prose stays on <see cref="Label"/>: a 5x7 pixel face is right for a score and wrong for a
    /// sentence.
    ///
    /// The glyph Images are pooled and reused, because the score changes several times a second and
    /// allocating a row of UI objects per change would be the kind of polish that costs frames.
    /// </summary>
    public sealed class BlockText : MonoBehaviour
    {
        public const int CellPx = 8;                // glyph texture resolution, not screen size

        /// <summary>Build the whole alphabet at boot, so no glyph is ever generated mid-play.</summary>
        public static void WarmFont() => BlockFont.Warm(CellPx);

        readonly List<Image> _pool = new List<Image>();
        readonly List<Image> _shadowPool = new List<Image>();
        Transform _shadowLayer, _glyphLayer;
        bool _shadow = true;                        // on by default: nearly all display text sits on art
        float _shadowOffset = 0.34f;                // in cells
        RectTransform _rt;
        string _text = "";
        Color _colour = Color.white;
        float _alpha = 1f;
        int _height = 48;                           // cap height in canvas units
        TextAnchor _anchor = TextAnchor.MiddleCenter;
        // One full cell of air between glyphs - BlockFont.AdvanceCols says 5 wide + 1 gap. The
        // first cut used 0.18 and the letters ran into each other: "ROOFTOP" came out as a smear.
        float _tracking = 1f;                       // extra gap between glyphs, in cell widths

        public RectTransform Rect => _rt;
        public string Text => _text;
        public Color Colour => _colour;

        public static BlockText Create(Transform parent, string name, int height, Color colour,
                                       TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<BlockText>();
            t._rt = go.GetComponent<RectTransform>();
            if (t._rt == null) t._rt = go.AddComponent<RectTransform>();
            t._height = height;
            t._colour = colour;
            t._anchor = anchor;

            // Shadow layer first so it sits BEHIND the glyphs: UI draws in sibling order.
            var sh = new GameObject("shadow"); sh.transform.SetParent(go.transform, false);
            StretchFull(sh);
            var gl = new GameObject("glyphs"); gl.transform.SetParent(go.transform, false);
            StretchFull(gl);
            t._shadowLayer = sh.transform;
            t._glyphLayer = gl.transform;
            return t;
        }

        static void StretchFull(GameObject go)
        {
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// A block-face label that lives in the world, above a robot or the bomb. Mirrors
        /// Label.CreateWorld so the two can be swapped at a call site without anything else moving.
        /// </summary>
        public static BlockText CreateWorld(Transform parent, string name, Vector3 localPos,
                                            int height, Color colour)
        {
            var holder = new GameObject(name + "Canvas");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;

            var canvas = holder.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 30;
            var crt = holder.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(680, 120);
            holder.transform.localScale = Vector3.one * 0.0045f;   // canvas px -> world units

            var t = Create(holder.transform, name, height, colour);
            t._rt.anchorMin = t._rt.anchorMax = new Vector2(0.5f, 0.5f);
            t._rt.sizeDelta = new Vector2(680, 120);
            t._rt.anchoredPosition = Vector2.zero;
            return t;
        }

        // ---- the Label-shaped surface the HUD uses ----

        public BlockText SetText(string s)
        {
            s = s ?? "";
            if (s == _text) return this;
            _text = s;
            Rebuild();
            return this;
        }

        public BlockText SetColour(Color c) { _colour = c; Apply(); return this; }
        public BlockText SetAlpha(float a) { _alpha = Mathf.Clamp01(a); Apply(); return this; }

        public BlockText SetSize(int height)
        {
            if (height == _height) return this;
            _height = height;
            Rebuild();
            return this;
        }

        public BlockText SetTracking(float cells) { _tracking = cells; Rebuild(); return this; }

        /// <summary>
        /// A dark offset copy behind the glyphs. White display text over a sunset skyline or a
        /// floodlit launchpad needs it; text on a flat panel does not.
        /// </summary>
        public BlockText SetShadow(bool on, float offsetCells = 0.34f)
        {
            _shadow = on; _shadowOffset = offsetCells; Rebuild(); return this;
        }

        public BlockText SetAnchored(Vector2 anchorMinMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            _rt.anchorMin = _rt.anchorMax = anchorMinMax;
            _rt.pivot = pivot;
            _rt.anchoredPosition = pos;
            _rt.sizeDelta = size;
            Rebuild();
            return this;
        }

        public void SetActive(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        /// <summary>Width the current string occupies, in canvas units.</summary>
        public float MeasuredWidth { get; private set; }

        // ---- layout ----

        void Rebuild()
        {
            if (_glyphLayer == null) return;        // not built yet

            float cell = _height / (float)BlockFont.Rows;
            float glyphW = BlockFont.Cols * cell;
            float step = glyphW + cell * _tracking;

            int visible = 0;
            foreach (char c in _text) if (c != ' ' && BlockFont.Has(c)) visible++;
            MeasuredWidth = _text.Length > 0 ? _text.Length * step - cell * _tracking : 0f;

            while (_pool.Count < visible) _pool.Add(NewGlyph(_glyphLayer));
            if (_shadow) while (_shadowPool.Count < visible) _shadowPool.Add(NewGlyph(_shadowLayer));

            // Horizontal origin from the anchor; vertical is always centred on the rect.
            float startX;
            switch (_anchor)
            {
                case TextAnchor.UpperLeft:
                case TextAnchor.MiddleLeft:
                case TextAnchor.LowerLeft:
                    startX = -_rt.sizeDelta.x * _rt.pivot.x + glyphW * 0.5f;
                    break;
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                case TextAnchor.LowerRight:
                    startX = _rt.sizeDelta.x * (1f - _rt.pivot.x) - MeasuredWidth + glyphW * 0.5f;
                    break;
                default:
                    startX = -MeasuredWidth * 0.5f + glyphW * 0.5f;
                    break;
            }

            float off = cell * _shadowOffset;
            int slot = 0;
            for (int i = 0; i < _text.Length; i++)
            {
                char c = _text[i];
                if (c == ' ') continue;
                var sprite = BlockFont.Glyph(c, CellPx);
                if (sprite == null) continue;         // unknown character: leave a gap, never a box

                var pos = new Vector2(startX + i * step, 0f);
                Place(_pool[slot], sprite, pos, glyphW);
                if (_shadow) Place(_shadowPool[slot], sprite, pos + new Vector2(off, -off), glyphW);
                slot++;
            }
            for (int i = slot; i < _pool.Count; i++) _pool[i].enabled = false;
            for (int i = slot; i < _shadowPool.Count; i++) _shadowPool[i].enabled = false;
            Apply();
        }

        void Place(Image img, Sprite sprite, Vector2 pos, float glyphW)
        {
            img.enabled = true;
            img.sprite = sprite;
            var r = img.rectTransform;
            r.sizeDelta = new Vector2(glyphW, _height);
            r.anchoredPosition = pos;
        }

        Image NewGlyph(Transform layer)
        {
            var go = new GameObject("g");
            go.transform.SetParent(layer, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        void Apply()
        {
            var c = new Color(_colour.r, _colour.g, _colour.b, _colour.a * _alpha);
            for (int i = 0; i < _pool.Count; i++) _pool[i].color = c;
            var sc = new Color(0f, 0f, 0f, 0.62f * _alpha);
            for (int i = 0; i < _shadowPool.Count; i++) _shadowPool[i].color = sc;
        }
    }
}
