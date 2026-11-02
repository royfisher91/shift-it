using System.Collections.Generic;
using UnityEngine;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// The four zones, built from generated shapes, with light ambient motion in each.
    ///
    /// Everything here is BEHIND the play area and deliberately low contrast: the brief's rule is
    /// "light parallax/ambient motion in every zone, but nothing that distracts from the bomb", so
    /// no backdrop element is brighter or faster-moving than the bomb and its fuse ring.
    ///
    /// One zone is live at a time; switching hides one root and shows another, so the cost of four
    /// zones is one zone's worth of draw calls.
    /// </summary>
    public sealed class ZoneBackdrop : MonoBehaviour
    {
        const int Zones = 4;

        readonly Transform[] _roots = new Transform[Zones];
        Camera _cam;
        int _current = -1;
        System.Random _rng;          // cosmetic stream only

        // animated bits, per zone
        readonly List<SpriteRenderer> _lamps = new List<SpriteRenderer>();
        readonly List<Transform> _fans = new List<Transform>();
        readonly List<Transform> _clouds = new List<Transform>();
        readonly List<Transform> _rain = new List<Transform>();
        readonly List<SpriteRenderer> _stars = new List<SpriteRenderer>();
        readonly List<Transform> _beams = new List<Transform>();
        SpriteRenderer _lightning;
        float _lightningT, _nextLightning = 3f;

        static readonly Color[] SkyTop =
        {
            new Color(0.17f, 0.24f, 0.26f),   // factory: teal-grey
            new Color(0.32f, 0.17f, 0.46f),   // rooftop: purple
            new Color(0.26f, 0.28f, 0.32f),   // scrapyard: storm grey
            new Color(0.04f, 0.05f, 0.13f),   // launchpad: night
        };
        static readonly Color[] SkyBottom =
        {
            new Color(0.24f, 0.32f, 0.34f),
            new Color(1.00f, 0.52f, 0.28f),   // rooftop: orange horizon
            new Color(0.42f, 0.42f, 0.44f),
            new Color(0.10f, 0.12f, 0.26f),
        };
        static readonly Color[] FloorColour =
        {
            new Color(0.20f, 0.23f, 0.25f),
            new Color(0.24f, 0.18f, 0.28f),
            new Color(0.22f, 0.19f, 0.16f),   // scrapyard mud
            new Color(0.13f, 0.14f, 0.18f),
        };

        public int Current => _current;
        public Color CameraColour => _current >= 0 ? SkyTop[_current] : Color.black;

        public static ZoneBackdrop Create(Transform parent, Camera cam)
        {
            var go = new GameObject("Zones");
            go.transform.SetParent(parent, false);
            var z = go.AddComponent<ZoneBackdrop>();
            z._cam = cam;
            z._rng = new System.Random(5150);
            for (int i = 0; i < Zones; i++)
            {
                var r = new GameObject("Zone" + (i + 1)).transform;
                r.SetParent(go.transform, false);
                r.gameObject.SetActive(false);
                z._roots[i] = r;
            }
            z.BuildFactory(z._roots[0]);
            z.BuildRooftop(z._roots[1]);
            z.BuildScrapyard(z._roots[2]);
            z.BuildLaunchpad(z._roots[3]);
            z.Show(0);
            return z;
        }

        float Rand() => (float)_rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * Rand();

        public void Show(int zone)
        {
            zone = Mathf.Clamp(zone, 0, Zones - 1);
            if (zone == _current) return;
            for (int i = 0; i < Zones; i++) _roots[i].gameObject.SetActive(i == zone);
            _current = zone;
            if (_cam != null) _cam.backgroundColor = SkyTop[zone];
        }

        // =====================================================================
        //  Shared builders
        // =====================================================================

        SpriteRenderer Add(Transform parent, string name, Sprite s, Color c, int order,
                           Vector3 pos, Vector3 scale, float rotZ = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.color = c; sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A two-stop vertical gradient, as a 1x64 generated texture stretched wide.</summary>
        void AddSky(Transform parent, Color top, Color bottom)
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "sky" };
            for (int y = 0; y < 64; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, y / 63f));
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 1, 64), new Vector2(0.5f, 0.5f), 10f, 0,
                                       SpriteMeshType.FullRect);
            Add(parent, "Sky", sprite, Color.white, -100, new Vector3(0f, 0.6f, 0f),
                new Vector3(260f, 2.0f, 1f));
        }

        void AddFloor(Transform parent, int zone)
        {
            Add(parent, "Floor", Shapes.Block(), FloorColour[zone], -60,
                new Vector3(0f, -6.2f, 0f), new Vector3(60f, 5f, 1f));
            // Hazard stripes along the floor edge - the factory's are the brightest.
            float a = zone == 0 ? 1f : 0.35f;
            for (int i = -14; i <= 14; i++)
            {
                bool yellow = (i & 1) == 0;
                Add(parent, "Hz" + i, Shapes.Block(),
                    yellow ? new Color(0.95f, 0.78f, 0.12f, a) : new Color(0.08f, 0.08f, 0.09f, a),
                    -58, new Vector3(i * 1.4f, -3.78f, 0f), new Vector3(0.36f, 0.22f, 1f), 0f);
            }
        }

        // =====================================================================
        //  Zone 1 - FACTORY FLOOR: KT Robotics bomb-testing bay
        // =====================================================================

        void BuildFactory(Transform r)
        {
            AddSky(r, SkyTop[0], SkyBottom[0]);

            // Wall panels with visible seams.
            for (int x = -5; x <= 5; x++)
            for (int y = 0; y < 3; y++)
                Add(r, "Panel", Shapes.RoundedRect(120, 90, 8),
                    new Color(0.22f, 0.29f, 0.31f), -90,
                    new Vector3(x * 2.0f, -1.2f + y * 1.9f, 0f), new Vector3(1.9f, 1.8f, 1f));

            // Overhead pipes.
            for (int i = 0; i < 3; i++)
                Add(r, "Pipe", Shapes.RoundedRect(600, 26, 13), new Color(0.30f, 0.36f, 0.37f), -80,
                    new Vector3(0f, 4.0f + i * 0.44f, 0f), new Vector3(6f, 1f, 1f));

            // Three hanging warm lamps. They flicker slightly.
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 5.6f;
                Add(r, "Flex", Shapes.Block(), new Color(0.12f, 0.13f, 0.14f), -79,
                    new Vector3(x, 4.35f, 0f), new Vector3(0.06f, 1.4f, 1f));
                Add(r, "Shade", Shapes.TriangleUp(90, 50), new Color(0.34f, 0.31f, 0.24f), -78,
                    new Vector3(x, 3.75f, 0f), new Vector3(1.1f, 1f, 1f), 180f);
                _lamps.Add(Add(r, "Glow", Shapes.Glow(256), new Color(1f, 0.82f, 0.45f, 0.30f), -77,
                    new Vector3(x, 3.1f, 0f), new Vector3(3.2f, 3.2f, 1f)));
            }

            // The safety sign.
            // The sign is a joke the player has to be able to READ. At the first size it was an
            // illegible yellow smudge, so it is bigger, higher-contrast and spaced out.
            Add(r, "SignPlate", Shapes.RoundedRect(300, 130, 12), new Color(0.93f, 0.88f, 0.26f), -70,
                new Vector3(-6.9f, 2.25f, 0f), new Vector3(2.15f, 1.75f, 1f));
            Add(r, "SignEdge", Shapes.RoundedRectOutline(300, 130, 12, 8), new Color(0.12f, 0.11f, 0.07f), -69,
                new Vector3(-6.9f, 2.25f, 0f), new Vector3(2.15f, 1.75f, 1f));
            var l1 = Label.CreateWorld(r, "SignTop", new Vector3(-6.9f, 2.78f, 0f), 54,
                                       new Color(0.10f, 0.09f, 0.06f));
            l1.SetText("DAYS WITHOUT");
            var l2 = Label.CreateWorld(r, "SignMid", new Vector3(-6.9f, 2.40f, 0f), 54,
                                       new Color(0.10f, 0.09f, 0.06f));
            l2.SetText("AN EXPLOSION");
            var l3 = Label.CreateWorld(r, "SignNum", new Vector3(-6.9f, 1.82f, 0f), 104,
                                       new Color(0.70f, 0.10f, 0.08f));
            l3.SetText("0");
        }

        // =====================================================================
        //  Zone 2 - ROOFTOP at sunset
        // =====================================================================

        void BuildRooftop(Transform r)
        {
            AddSky(r, SkyTop[1], SkyBottom[1]);

            // Big low sun.
            Add(r, "SunGlow", Shapes.Glow(256), new Color(1f, 0.72f, 0.35f, 0.5f), -95,
                new Vector3(-3.2f, -0.6f, 0f), new Vector3(14f, 14f, 1f));
            Add(r, "Sun", Shapes.Circle(96), new Color(1f, 0.88f, 0.55f), -94,
                new Vector3(-3.2f, -0.6f, 0f), new Vector3(2.6f, 2.6f, 1f));

            // Skyline silhouettes, two parallax layers, with lit windows.
            for (int layer = 0; layer < 2; layer++)
            {
                float depth = layer == 0 ? 0.55f : 1f;
                var col = Color.Lerp(new Color(0.20f, 0.12f, 0.26f), new Color(0.10f, 0.06f, 0.16f), depth);
                for (int i = -7; i <= 7; i++)
                {
                    float h = Range(1.6f, 4.6f) * (layer == 0 ? 0.8f : 1f);
                    float w = Range(0.9f, 1.8f);
                    float x = i * 1.55f + Range(-0.3f, 0.3f);
                    float baseY = layer == 0 ? -1.4f : -1.9f;
                    Add(r, "Tower", Shapes.Block(), col, -90 + layer,
                        new Vector3(x, baseY + h * 0.5f, 0f), new Vector3(w, h, 1f));
                    int rows = Mathf.Max(1, (int)(h * 2.2f));
                    for (int wy = 0; wy < rows; wy++)
                    for (int wx = 0; wx < 2; wx++)
                    {
                        if (Rand() > 0.45f) continue;
                        Add(r, "Win", Shapes.Block(), new Color(1f, 0.85f, 0.45f, Range(0.4f, 0.95f)),
                            -88 + layer,
                            new Vector3(x + (wx - 0.5f) * w * 0.45f, baseY + 0.25f + wy * 0.42f, 0f),
                            new Vector3(w * 0.16f, 0.14f, 1f));
                    }
                }
            }

            // Rooftop ledge.
            Add(r, "Ledge", Shapes.Block(), new Color(0.30f, 0.22f, 0.30f), -62,
                new Vector3(0f, -3.55f, 0f), new Vector3(60f, 0.5f, 1f));
            AddFloor(r, 1);

            // AC units with spinning fans.
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -7.6f : 7.4f;
                Add(r, "AC", Shapes.RoundedRect(180, 130, 14), new Color(0.44f, 0.42f, 0.46f), -61,
                    new Vector3(x, -2.85f, 0f), new Vector3(1.2f, 1.1f, 1f));
                var fan = new GameObject("Fan").transform;
                fan.SetParent(r, false);
                fan.localPosition = new Vector3(x, -2.85f, 0f);
                for (int b = 0; b < 4; b++)
                    Add(fan, "Blade", Shapes.CapsuleLeftPivot(46, 14), new Color(0.22f, 0.22f, 0.25f), -60,
                        Vector3.zero, Vector3.one, b * 90f);
                _fans.Add(fan);
            }
        }

        // =====================================================================
        //  Zone 3 - SCRAPYARD in a storm
        // =====================================================================

        void BuildScrapyard(Transform r)
        {
            AddSky(r, SkyTop[2], SkyBottom[2]);

            // Drifting dark clouds.
            for (int i = 0; i < 7; i++)
            {
                var c = new GameObject("Cloud").transform;
                c.SetParent(r, false);
                c.localPosition = new Vector3(Range(-12f, 12f), Range(2.2f, 4.8f), 0f);
                int lumps = 3 + (int)(Rand() * 3);
                for (int j = 0; j < lumps; j++)
                    Add(c, "Lump", Shapes.Circle(96), new Color(0.17f, 0.18f, 0.21f, 0.92f), -92,
                        new Vector3(j * Range(0.7f, 1.1f) - lumps * 0.4f, Range(-0.2f, 0.2f), 0f),
                        Vector3.one * Range(1.4f, 2.6f));
                _clouds.Add(c);
            }

            // Lightning: one big flash sprite, driven on a timer.
            _lightning = Add(r, "Lightning", Shapes.Block(), new Color(1f, 1f, 1f, 0f), -91,
                             new Vector3(0f, 0.6f, 0f), new Vector3(60f, 24f, 1f));

            // Rusty crane.
            Add(r, "CraneMast", Shapes.Block(), new Color(0.44f, 0.26f, 0.16f), -85,
                new Vector3(7.4f, 0.4f, 0f), new Vector3(0.42f, 8f, 1f));
            Add(r, "CraneJib", Shapes.Block(), new Color(0.44f, 0.26f, 0.16f), -85,
                new Vector3(4.6f, 3.9f, 0f), new Vector3(6.6f, 0.3f, 1f));
            Add(r, "CraneCable", Shapes.Block(), new Color(0.2f, 0.18f, 0.16f), -84,
                new Vector3(2.2f, 2.9f, 0f), new Vector3(0.06f, 2f, 1f));
            Add(r, "CraneHook", Shapes.RoundedRect(60, 60, 10), new Color(0.35f, 0.33f, 0.3f), -84,
                new Vector3(2.2f, 1.8f, 0f), new Vector3(0.5f, 0.5f, 1f));

            // Scrap piles with tyres.
            for (int i = 0; i < 5; i++)
            {
                float x = Range(-9f, 9f);
                Add(r, "Scrap", Shapes.TriangleUp(260, 150), new Color(0.33f, 0.28f, 0.24f), -70,
                    new Vector3(x, -3.0f, 0f), new Vector3(Range(1f, 1.9f), Range(0.8f, 1.5f), 1f));
                for (int t = 0; t < 2; t++)
                    Add(r, "Tyre", Shapes.Ring(64, 16), new Color(0.13f, 0.13f, 0.14f), -69,
                        new Vector3(x + Range(-0.9f, 0.9f), -3.3f + t * 0.42f, 0f),
                        Vector3.one * Range(0.7f, 1.05f));
            }

            AddFloor(r, 2);
            // Puddles catching the sky.
            for (int i = 0; i < 6; i++)
                Add(r, "Puddle", Shapes.RoundedRect(200, 34, 17), new Color(0.45f, 0.5f, 0.55f, 0.45f), -57,
                    new Vector3(Range(-9f, 9f), Range(-3.95f, -3.6f), 0f),
                    new Vector3(Range(0.7f, 1.6f), 1f, 1f));

            // Rain: a few columns of streaks, recycled as they fall.
            for (int i = 0; i < 46; i++)
            {
                var d = Add(r, "Rain", Shapes.Block(), new Color(0.75f, 0.82f, 0.9f, 0.33f), -50,
                            new Vector3(Range(-12f, 12f), Range(-4f, 6f), 0f),
                            new Vector3(0.035f, Range(0.4f, 0.8f), 1f), 8f).transform;
                _rain.Add(d);
            }
        }

        // =====================================================================
        //  Zone 4 - LAUNCHPAD at night
        // =====================================================================

        void BuildLaunchpad(Transform r)
        {
            AddSky(r, SkyTop[3], SkyBottom[3]);

            // Twinkling stars.
            for (int i = 0; i < 80; i++)
            {
                var s = Add(r, "Star", Shapes.Circle(16), new Color(1f, 1f, 1f, Range(0.25f, 0.9f)), -95,
                            new Vector3(Range(-13f, 13f), Range(-0.5f, 6.0f), 0f),
                            Vector3.one * Range(0.1f, 0.26f));
                _stars.Add(s);
            }

            // Floodlight beams that sway.
            for (int i = 0; i < 3; i++)
            {
                var b = new GameObject("Beam").transform;
                b.SetParent(r, false);
                b.localPosition = new Vector3((i - 1) * 7.5f, -3.4f, 0f);
                Add(b, "Cone", Shapes.TriangleUp(240, 900), new Color(1f, 0.96f, 0.75f, 0.10f), -90,
                    new Vector3(0f, 4.4f, 0f), new Vector3(1f, 1f, 1f));
                Add(b, "Lamp", Shapes.Glow(128), new Color(1f, 0.96f, 0.8f, 0.6f), -88,
                    Vector3.zero, Vector3.one * 1.1f);
                _beams.Add(b);
            }

            // Red lattice gantry: uprights, cross-braces and X bracing.
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -8.5f : -4.3f;
                Add(r, "Upright", Shapes.Block(), new Color(0.62f, 0.16f, 0.14f), -80,
                    new Vector3(x, 0.6f, 0f), new Vector3(0.22f, 9f, 1f));
                for (int k = 0; k < 7; k++)
                {
                    Add(r, "Brace", Shapes.Block(), new Color(0.55f, 0.15f, 0.13f), -80,
                        new Vector3(x + (side == 0 ? 0.55f : -0.55f), -3.2f + k * 1.3f, 0f),
                        new Vector3(1.1f, 0.12f, 1f));
                    Add(r, "XBrace", Shapes.Block(), new Color(0.48f, 0.13f, 0.12f), -81,
                        new Vector3(x + (side == 0 ? 0.55f : -0.55f), -2.55f + k * 1.3f, 0f),
                        new Vector3(1.5f, 0.09f, 1f), side == 0 ? 40f : -40f);
                }
            }

            // The rocket, marked KT-1.
            Add(r, "Rocket", Shapes.RoundedRect(150, 620, 70), new Color(0.94f, 0.94f, 0.96f), -78,
                new Vector3(-6.4f, 1.3f, 0f), new Vector3(1.05f, 1.02f, 1f));
            Add(r, "Nose", Shapes.TriangleUp(150, 170), new Color(0.90f, 0.91f, 0.94f), -78,
                new Vector3(-6.4f, 4.55f, 0f), new Vector3(1.05f, 1f, 1f));
            Add(r, "FinL", Shapes.TriangleUp(90, 150), new Color(0.72f, 0.16f, 0.14f), -79,
                new Vector3(-7.3f, -1.3f, 0f), new Vector3(1f, 1f, 1f), 28f);
            Add(r, "FinR", Shapes.TriangleUp(90, 150), new Color(0.72f, 0.16f, 0.14f), -79,
                new Vector3(-5.5f, -1.3f, 0f), new Vector3(1f, 1f, 1f), -28f);
            Add(r, "Band", Shapes.Block(), new Color(0.72f, 0.16f, 0.14f), -77,
                new Vector3(-6.4f, 2.5f, 0f), new Vector3(1.55f, 0.3f, 1f));
            var tag = Label.CreateWorld(r, "RocketTag", new Vector3(-6.4f, 0.9f, 0f), 72,
                                        new Color(0.2f, 0.22f, 0.26f));
            tag.SetText("KT-1");

            AddFloor(r, 3);
            // Painted landing circle.
            Add(r, "LandRing", Shapes.Ring(400, 22), new Color(0.92f, 0.92f, 0.5f, 0.45f), -56,
                new Vector3(0f, -3.72f, 0f), new Vector3(2.6f, 0.55f, 1f));
            Add(r, "LandRing2", Shapes.Ring(260, 18), new Color(0.92f, 0.92f, 0.5f, 0.35f), -56,
                new Vector3(0f, -3.72f, 0f), new Vector3(2.6f, 0.55f, 1f));
        }

        // =====================================================================

        public void Tick(float dt)
        {
            float t = Time.time;
            switch (_current)
            {
                case 0:
                    // Lamp flicker: mostly steady, with an occasional dip. Subtle on purpose.
                    for (int i = 0; i < _lamps.Count; i++)
                    {
                        float n = Mathf.PerlinNoise(t * 2.3f + i * 7.3f, 0.5f);
                        float dip = n > 0.82f ? 0.55f : 1f;
                        var c = _lamps[i].color;
                        _lamps[i].color = new Color(c.r, c.g, c.b,
                            Mathf.Lerp(c.a, 0.30f * dip * (0.9f + n * 0.2f), dt * 8f));
                    }
                    break;

                case 1:
                    for (int i = 0; i < _fans.Count; i++)
                        _fans[i].localRotation *= Quaternion.Euler(0f, 0f, -260f * dt);
                    break;

                case 2:
                    for (int i = 0; i < _clouds.Count; i++)
                    {
                        var p = _clouds[i].localPosition;
                        p.x += dt * (0.35f + (i % 3) * 0.12f);
                        if (p.x > 15f) p.x = -15f;
                        _clouds[i].localPosition = p;
                    }
                    for (int i = 0; i < _rain.Count; i++)
                    {
                        var p = _rain[i].localPosition;
                        p.y -= dt * 17f;
                        p.x -= dt * 2.4f;
                        if (p.y < -4.2f) { p.y = 6.2f; p.x = Range(-12f, 12f); }
                        _rain[i].localPosition = p;
                    }
                    _nextLightning -= dt;
                    if (_nextLightning <= 0f) { _lightningT = 1f; _nextLightning = Range(3.5f, 9f); }
                    if (_lightningT > 0f)
                    {
                        _lightningT = Mathf.MoveTowards(_lightningT, 0f, dt * 5.5f);
                        // Double flash reads as real lightning.
                        float a = _lightningT * (Mathf.Repeat(_lightningT * 6f, 1f) > 0.45f ? 0.22f : 0.06f);
                        _lightning.color = new Color(1f, 1f, 1f, a);
                    }
                    break;

                case 3:
                    for (int i = 0; i < _stars.Count; i++)
                    {
                        var c = _stars[i].color;
                        float tw = 0.35f + 0.55f * Mathf.PerlinNoise(t * 1.4f + i * 3.1f, i * 0.7f);
                        _stars[i].color = new Color(c.r, c.g, c.b, tw);
                    }
                    for (int i = 0; i < _beams.Count; i++)
                        _beams[i].localRotation = Quaternion.Euler(0f, 0f,
                            Mathf.Sin(t * 0.35f + i * 2.1f) * 11f);
                    break;
            }
        }
    }
}
