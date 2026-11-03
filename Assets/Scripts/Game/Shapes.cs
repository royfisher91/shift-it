using System.Collections.Generic;
using UnityEngine;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// Every sprite in the game, generated in code. Nothing is downloaded and nothing is imported,
    /// so the art is original by construction and there are no binary assets to review or licence.
    ///
    /// All shapes are drawn WHITE and tinted by the renderer, so one texture serves every colour.
    /// Coverage is computed from 4x4 sub-samples per pixel, which gives clean edges on the chunky
    /// cartoon outlines the style needs without a single imported PNG.
    ///
    /// Sprites are cached by their parameters: the whole set is a few hundred KB and is built once
    /// at boot, which matters for the WebGL build.
    /// </summary>
    public static class Shapes
    {
        const int SS = 4;                       // sub-samples per axis
        const float PPU = 100f;                 // pixels per unit

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static void ClearCache() => _cache.Clear();
        public static int CachedCount => _cache.Count;

        static Sprite Build(string key, int w, int h, System.Func<float, float, bool> inside,
                            Vector2? pivot = null, float ppu = PPU)
        {
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "gen_" + key,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[w * h];
            float inv = 1f / (SS * SS);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        float fx = x + (sx + 0.5f) / SS;
                        float fy = y + (sy + 0.5f) / SS;
                        if (inside(fx, fy)) hits++;
                    }
                    byte a = (byte)Mathf.RoundToInt(hits * inv * 255f);
                    px[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);

            var p = pivot ?? new Vector2(0.5f, 0.5f);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), p, ppu, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            _cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// A plain white square measuring exactly ONE WORLD UNIT, so a localScale of (60, 5) means
        /// 60 by 5 units and nothing else has to think about it.
        ///
        /// This used to be built at the shared 100 pixels-per-unit, which made the 4x4 texture
        /// 0.04 units across - so every world object built from it came out TWENTY-FIVE TIMES
        /// smaller than its scale said. That one line is why the skyline was a row of specks, the
        /// scrapyard crane and the rain were invisible, the launchpad gantry never appeared and the
        /// hazard stripes along the floor could not be seen. UI Images are unaffected: they size
        /// themselves from their RectTransform and ignore pixels-per-unit entirely.
        /// </summary>
        public static Sprite Block() => Build("block1u", 4, 4, (x, y) => true, null, 4f);

        /// <summary>Filled circle, diameter in pixels.</summary>
        public static Sprite Circle(int d)
        {
            float r = d * 0.5f;
            return Build("circle" + d, d, d, (x, y) =>
            {
                float dx = x - r, dy = y - r;
                return dx * dx + dy * dy <= r * r;
            });
        }

        /// <summary>Rounded rectangle - the body shape for every robot and panel.</summary>
        public static Sprite RoundedRect(int w, int h, int radius)
        {
            int rad = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);
            return Build($"rr{w}x{h}r{rad}", w, h, (x, y) => InRoundedRect(x, y, w, h, rad));
        }

        static bool InRoundedRect(float x, float y, float w, float h, float rad)
        {
            if (x < 0 || y < 0 || x > w || y > h) return false;
            float cx = Mathf.Clamp(x, rad, w - rad);
            float cy = Mathf.Clamp(y, rad, h - rad);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= rad * rad + 1e-4f;
        }

        /// <summary>
        /// Rounded rectangle outline. The bold black outline is the whole look of the game, so it
        /// is a real shape rather than a second sprite scaled up behind the first.
        /// </summary>
        public static Sprite RoundedRectOutline(int w, int h, int radius, int thickness)
        {
            int rad = Mathf.Clamp(radius, 0, Mathf.Min(w, h) / 2);
            int t = Mathf.Max(1, thickness);
            return Build($"rro{w}x{h}r{rad}t{t}", w, h, (x, y) =>
                InRoundedRect(x, y, w, h, rad) &&
                !InRoundedRect(x - t, y - t, w - 2 * t, h - 2 * t, Mathf.Max(0, rad - t)));
        }

        /// <summary>Ring / annulus. Used for the fuse ring track and for shield arcs.</summary>
        public static Sprite Ring(int d, int thickness)
        {
            float r = d * 0.5f;
            float inner = Mathf.Max(0f, r - thickness);
            return Build($"ring{d}t{thickness}", d, d, (x, y) =>
            {
                float dx = x - r, dy = y - r;
                float q = dx * dx + dy * dy;
                return q <= r * r && q >= inner * inner;
            });
        }

        /// <summary>A dashed ring, for the ON BREAK empty pad outline.</summary>
        public static Sprite DashedRing(int d, int thickness, int dashes)
        {
            float r = d * 0.5f;
            float inner = Mathf.Max(0f, r - thickness);
            int n = Mathf.Max(2, dashes);
            return Build($"dring{d}t{thickness}n{n}", d, d, (x, y) =>
            {
                float dx = x - r, dy = y - r;
                float q = dx * dx + dy * dy;
                if (q > r * r || q < inner * inner) return false;
                float ang = Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 0.5f;   // 0..1
                return Mathf.Repeat(ang * n, 1f) < 0.55f;
            });
        }

        /// <summary>Isoceles triangle pointing UP, used for the Mode A aim arrow head.</summary>
        public static Sprite TriangleUp(int w, int h)
        {
            return Build($"tri{w}x{h}", w, h, (x, y) =>
            {
                float t = y / h;                           // 0 at base, 1 at tip
                float halfAt = (w * 0.5f) * (1f - t);
                return Mathf.Abs(x - w * 0.5f) <= halfAt;
            });
        }

        /// <summary>A four-pointed sparkle, for fuse sparks and PERFECT pops.</summary>
        public static Sprite Spark(int d)
        {
            float r = d * 0.5f;
            return Build("spark" + d, d, d, (x, y) =>
            {
                float dx = (x - r) / r, dy = (y - r) / r;
                float a = Mathf.Abs(dx), b = Mathf.Abs(dy);
                // A star-ish superellipse: tight waist, long points.
                return Mathf.Pow(a, 0.55f) + Mathf.Pow(b, 0.55f) <= 1f;
            });
        }

        /// <summary>A soft radial glow for lamps, floodlights and the explosion flash.</summary>
        public static Sprite Glow(int d)
        {
            float r = d * 0.5f;
            var key = "glow" + d;
            if (_cache.TryGetValue(key, out var c) && c != null) return c;

            var tex = new Texture2D(d, d, TextureFormat.RGBA32, false)
            { name = "gen_" + key, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[d * d];
            for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                float q = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - q);
                a = a * a;                                  // falls off fast, no hard edge
                px[y * d + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var s = Sprite.Create(tex, new Rect(0, 0, d, d), new Vector2(0.5f, 0.5f), PPU, 0, SpriteMeshType.FullRect);
            s.name = key;
            _cache[key] = s;
            return s;
        }

        /// <summary>
        /// A capsule / lozenge, for robot limbs, pipes and the hazard-stripe wipe bars.
        /// Pivot is at the LEFT end so it can be rotated and stretched like a bone.
        /// </summary>
        public static Sprite CapsuleLeftPivot(int w, int h)
        {
            float r = h * 0.5f;
            return Build($"caps{w}x{h}", w, h, (x, y) =>
                InRoundedRect(x, y, w, h, r), new Vector2(r / w, 0.5f));
        }
    }
}
