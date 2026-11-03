using System.Collections.Generic;
using UnityEngine;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// The chunky display face, drawn rather than downloaded.
    ///
    /// Every glyph is a 5x7 grid of cells, and each lit cell is rendered as a rounded square with a
    /// gap around it, so the letters read as big soft pixels. That suits a game whose characters are
    /// rounded-rectangle robots with bold outlines, and it means the typeface is original by
    /// construction - the same reason every sprite in this project is generated. There is no font
    /// licence to argue about and nothing extra to ship.
    ///
    /// Glyphs are cached per (character, cell size), so the whole alphabet costs a few hundred KB
    /// once and nothing thereafter.
    /// </summary>
    public static class BlockFont
    {
        public const int Cols = 5;
        public const int Rows = 7;

        /// <summary>Width of one glyph in cells, including the space that follows it.</summary>
        public const int AdvanceCols = 6;

        static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // 5x7, top row first. '#' is a lit cell.
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#...#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#...#", "#.#.#", "##.##", "#...#" },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },

            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },

            [' '] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." },
            ['!'] = new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            ['.'] = new[] { ".....", ".....", ".....", ".....", ".....", ".##..", ".##.." },
            [','] = new[] { ".....", ".....", ".....", ".....", ".##..", ".##..", ".#..." },
            [':'] = new[] { ".....", ".##..", ".##..", ".....", ".##..", ".##..", "....." },
            ['-'] = new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            ['('] = new[] { "...#.", "..#..", ".#...", ".#...", ".#...", "..#..", "...#." },
            [')'] = new[] { ".#...", "..#..", "...#.", "...#.", "...#.", "..#..", ".#..." },
            ['>'] = new[] { "#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#...." },
            ['<'] = new[] { "....#", "...#.", "..#..", ".#...", "..#..", "...#.", "....#" },
            ['%'] = new[] { "##..#", "##.#.", "..#..", ".#...", "#..##", "..###", "....." },
            ['\''] = new[] { "..#..", "..#..", ".....", ".....", ".....", ".....", "....." },
            ['x'] = new[] { ".....", ".....", "#...#", ".#.#.", "..#..", ".#.#.", "#...#" },
        };

        public static bool Has(char c) => Glyphs.ContainsKey(Normalise(c));

        /// <summary>
        /// Upper-cases everything except a lower-case 'x', which is kept as its own glyph: the
        /// multiplication sign in "COMBO x5" should be a small cross, not the letter X.
        /// </summary>
        static char Normalise(char c) => c == 'x' ? 'x' : char.ToUpperInvariant(c);

        /// <summary>
        /// One glyph, drawn white so the renderer can tint it. cell is the size of one grid square
        /// in pixels; the sprite is 5 x 7 cells.
        /// </summary>
        public static Sprite Glyph(char c, int cell)
        {
            char k = Normalise(c);
            if (!Glyphs.TryGetValue(k, out var rows)) return null;
            if (k == ' ') return null;

            string key = "bf" + k + "_" + cell;
            if (_cache.TryGetValue(key, out var hit) && hit != null) return hit;

            int w = Cols * cell, h = Rows * cell;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "glyph_" + k,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[w * h];

            // Each lit cell is a rounded square inset by a hair, which gives the face its soft
            // pixel look instead of a hard blocky one.
            float inset = cell * 0.06f;
            float radius = cell * 0.26f;
            const int SS = 3;
            float inv = 1f / (SS * SS);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int cx = x / cell;
                int cyFromTop = Rows - 1 - (y / cell);     // texture y is bottom-up
                bool lit = rows[cyFromTop][cx] == '#';
                if (!lit) { px[y * w + x] = new Color32(255, 255, 255, 0); continue; }

                float lx = x - cx * cell;
                float ly = y - (y / cell) * cell;
                int hits = 0;
                for (int sy = 0; sy < SS; sy++)
                for (int sx = 0; sx < SS; sx++)
                {
                    float fx = lx + (sx + 0.5f) / SS;
                    float fy = ly + (sy + 0.5f) / SS;
                    if (InRounded(fx, fy, inset, cell - inset, radius)) hits++;
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(hits * inv * 255f));
            }

            tex.SetPixels32(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                                       SpriteMeshType.FullRect);
            sprite.name = key;
            _cache[key] = sprite;
            return sprite;
        }

        static bool InRounded(float x, float y, float lo, float hi, float r)
        {
            if (x < lo || y < lo || x > hi || y > hi) return false;
            float cx = Mathf.Clamp(x, lo + r, hi - r);
            float cy = Mathf.Clamp(y, lo + r, hi - r);
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r + 1e-4f;
        }

        /// <summary>
        /// Build every glyph up front, at boot, where sprite generation belongs.
        ///
        /// Without this the first PERFECT!, the first blocker sign and each new zone name each
        /// built their glyphs the moment they were first drawn - and that showed up as a single
        /// 161 ms hitch in the middle of play, which the per-frame budget test caught. Generating
        /// the whole alphabet once costs a few milliseconds at startup, where the cost is paid
        /// before the player is doing anything.
        /// </summary>
        public static void Warm(int cell)
        {
            foreach (var kv in Glyphs)
            {
                if (kv.Key == ' ') continue;
                Glyph(kv.Key, cell);
            }
        }

        public static int CachedGlyphs => _cache.Count;
    }
}
