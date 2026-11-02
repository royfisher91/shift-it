using UnityEngine;
using UnityEngine.UI;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// The fuse ring around the bomb: the one thing the player actually reads under pressure, so it
    /// gets a design rather than a progress bar.
    ///
    /// Layout, back to front:
    ///   1. TRACK      - full dark ring, so the empty part of the fuse is still a visible circle.
    ///   2. FUSE ARC   - bright arc from the top, clockwise, length = fuse remaining.
    ///   3. BAND       - the PERFECT window, a translucent green zone painted ON TOP of the arc so
    ///                   it never disappears underneath it. Painted by rotating a Radial360 image so
    ///                   its fill origin sits at the band's low edge - no masks, no erasing.
    ///   4. BAND EDGES - two bright ticks at the band boundaries, because a translucent zone alone
    ///                   reads poorly at speed.
    ///   5. TIP        - a solid dot at the arc's leading end. This is the pointer the player aims
    ///                   with: PERFECT is "tip inside the green".
    ///
    /// The band pulses to full brightness while the tip is inside it, so the player gets the tell
    /// BEFORE committing rather than only learning the result afterwards.
    /// </summary>
    public sealed class FuseRingView : MonoBehaviour
    {
        const int RingPx = 256;
        const int RingThickPx = 26;

        Image _track, _fuseArc, _band;
        RectTransform _tip, _edgeLo, _edgeHi;
        Image _tipImg;
        CanvasGroup _group;
        float _radiusPx;
        float _bandPulse;

        public Color TrackColour = new Color(0.10f, 0.11f, 0.13f, 0.92f);
        public Color FuseColour = new Color(1.00f, 0.72f, 0.18f, 1f);
        public Color FuseLowColour = new Color(1.00f, 0.26f, 0.18f, 1f);
        public Color BandColour = new Color(0.33f, 0.90f, 0.40f, 0.50f);
        public Color BandHotColour = new Color(0.45f, 1.00f, 0.52f, 0.95f);

        public static FuseRingView Create(Transform parent, float worldDiameter)
        {
            var go = new GameObject("FuseRing");
            go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(RingPx, RingPx);
            // One pixel of the ring canvas maps to this many world units.
            float scale = worldDiameter / RingPx;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            canvas.sortingOrder = 20;

            var v = go.AddComponent<FuseRingView>();
            v._radiusPx = RingPx * 0.5f - RingThickPx * 0.5f;
            v.Build(rt);
            return v;
        }

        void Build(RectTransform root)
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            var ring = Shapes.Ring(RingPx, RingThickPx);

            _track = AddRing(root, "Track", ring, TrackColour, Image.Type.Simple);
            _fuseArc = AddRing(root, "FuseArc", ring, FuseColour, Image.Type.Filled);
            _band = AddRing(root, "Band", ring, BandColour, Image.Type.Filled);

            _edgeLo = AddTick(root, "EdgeLo");
            _edgeHi = AddTick(root, "EdgeHi");

            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(root, false);
            _tipImg = tipGo.AddComponent<Image>();
            _tipImg.sprite = Shapes.Circle(40);
            _tipImg.color = Color.white;
            _tip = tipGo.GetComponent<RectTransform>();
            _tip.sizeDelta = new Vector2(34, 34);
        }

        static Image AddRing(RectTransform parent, string name, Sprite sprite, Color colour, Image.Type type)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = colour;
            img.type = type;
            img.raycastTarget = false;
            if (type == Image.Type.Filled)
            {
                img.fillMethod = Image.FillMethod.Radial360;
                img.fillOrigin = (int)Image.Origin360.Top;
                img.fillClockwise = true;
                img.fillAmount = 1f;
            }
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(RingPx, RingPx);
            rt.anchoredPosition = Vector2.zero;
            return img;
        }

        RectTransform AddTick(RectTransform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = Shapes.Block();
            img.color = new Color(0.60f, 1f, 0.66f, 1f);
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(7, RingThickPx + 12);
            return rt;
        }

        /// <summary>How much of the ring the fuse arc currently fills, 0..1.</summary>
        public float FuseFill => _fuseArc != null ? _fuseArc.fillAmount : -1f;
        /// <summary>The painted width of the green band, 0..1. Zero on a dud, which has no band.</summary>
        public float BandFill => _band != null && _band.enabled ? _band.fillAmount : 0f;
        public bool BandShown => _band != null && _band.enabled;
        public float Alpha => _group != null ? _group.alpha : 0f;

        public void SetVisible(bool on)
        {
            if (_group != null) _group.alpha = on ? 1f : 0f;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Drive the ring. fuseFraction is time remaining over the bomb's full fuse; bandLow/High
        /// are the PERFECT window as fractions of the same.
        /// </summary>
        public void Set(float fuseFraction, float bandLow, float bandHigh, bool dud, bool hot, float dt)
        {
            float f = Mathf.Clamp01(fuseFraction);

            _fuseArc.fillAmount = f;
            bool inBand = f >= bandLow && f <= bandHigh;

            // A dud must not look like a timer you have to beat: it reads cool and flat, with no
            // band at all, because the correct play is to do nothing.
            if (dud)
            {
                _fuseArc.color = new Color(0.40f, 0.85f, 0.45f, 1f);
                _band.enabled = false;
                _edgeLo.gameObject.SetActive(false);
                _edgeHi.gameObject.SetActive(false);
            }
            else
            {
                _band.enabled = true;
                _edgeLo.gameObject.SetActive(true);
                _edgeHi.gameObject.SetActive(true);

                // Colour the arc by urgency, and make a HOT bomb read red from the start.
                Color baseCol = hot ? new Color(1f, 0.42f, 0.22f) : FuseColour;
                _fuseArc.color = Color.Lerp(FuseLowColour, baseCol, Mathf.Clamp01((f - 0.08f) / 0.30f));

                // The band: rotate the image so its Radial360 origin lands on the band's low edge,
                // then fill forward by the band's width. No mask, no second erasing layer.
                float span = Mathf.Clamp01(bandHigh - bandLow);
                _band.rectTransform.localEulerAngles = new Vector3(0f, 0f, -bandLow * 360f);
                _band.fillAmount = span;

                _bandPulse = Mathf.MoveTowards(_bandPulse, inBand ? 1f : 0f, dt * 9f);
                _band.color = Color.Lerp(BandColour, BandHotColour, _bandPulse);
                float pop = 1f + 0.05f * _bandPulse;
                _band.rectTransform.localScale = new Vector3(pop, pop, 1f);

                PlaceOnRing(_edgeLo, bandLow);
                PlaceOnRing(_edgeHi, bandHigh);
                _edgeLo.localEulerAngles = new Vector3(0, 0, -bandLow * 360f);
                _edgeHi.localEulerAngles = new Vector3(0, 0, -bandHigh * 360f);
            }

            PlaceOnRing(_tip, f);
            _tipImg.color = dud ? new Color(0.7f, 1f, 0.75f)
                                : (inBand ? new Color(0.55f, 1f, 0.6f) : Color.white);
            float tipPop = inBand ? 1.30f : 1f;
            _tip.localScale = Vector3.Lerp(_tip.localScale, new Vector3(tipPop, tipPop, 1f), dt * 14f);
        }

        /// <summary>Put a child at the given fraction around the ring, clockwise from the top.</summary>
        void PlaceOnRing(RectTransform rt, float fraction)
        {
            float a = fraction * Mathf.PI * 2f;
            rt.anchoredPosition = new Vector2(Mathf.Sin(a) * _radiusPx, Mathf.Cos(a) * _radiusPx);
        }
    }
}
