using UnityEngine;

namespace KT.ShiftIt.Game
{
    /// <summary>
    /// Camera shake and particles, pooled.
    ///
    /// Two rules from the pattern pack are load-bearing here:
    ///   - Cosmetic randomness gets its OWN generator (quality lesson D9), so an art tweak can never
    ///     change a gameplay roll or break a seeded balance run.
    ///   - Pool every particle and floating-text object; polish that costs frames is not polish.
    /// </summary>
    public sealed class Juice : MonoBehaviour
    {
        const int PoolSize = 96;

        struct P
        {
            public Transform T;
            public SpriteRenderer R;
            public Vector3 Vel;
            public float Life, MaxLife, Spin, StartScale, EndScale;
            public Color From, To;
            public bool Active;
        }

        P[] _pool;
        Transform _camera;
        Vector3 _camHome;
        float _shake, _shakeDecay = 3.2f;
        System.Random _rng;          // cosmetic stream only
        float _noiseSeed;

        public static Juice Create(Transform parent, Transform cam)
        {
            var go = new GameObject("Juice");
            go.transform.SetParent(parent, false);
            var j = go.AddComponent<Juice>();
            j._camera = cam;
            j._camHome = cam.localPosition;
            j._rng = new System.Random(987654321);
            j._noiseSeed = 13.7f;
            j.BuildPool();
            return j;
        }

        void BuildPool()
        {
            _pool = new P[PoolSize];
            var smoke = Shapes.Circle(48);
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("p" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = smoke;
                sr.sortingOrder = 40;
                go.SetActive(false);
                _pool[i] = new P { T = go.transform, R = sr, Active = false };
            }
        }

        float Rand() => (float)_rng.NextDouble();
        float Range(float a, float b) => a + (b - a) * Rand();

        int Take()
        {
            for (int i = 0; i < _pool.Length; i++) if (!_pool[i].Active) return i;
            return -1;      // pool exhausted: drop the particle rather than allocate
        }

        void Spawn(Vector3 pos, Vector3 vel, float life, float s0, float s1,
                   Color from, Color to, Sprite sprite, float spin)
        {
            int i = Take();
            if (i < 0) return;
            ref var p = ref _pool[i];
            p.Active = true;
            p.Life = 0f; p.MaxLife = life;
            p.Vel = vel; p.Spin = spin;
            p.StartScale = s0; p.EndScale = s1;
            p.From = from; p.To = to;
            p.R.sprite = sprite;
            p.R.color = from;
            p.T.localPosition = pos;
            p.T.localScale = Vector3.one * s0;
            p.T.localRotation = Quaternion.Euler(0, 0, Range(0f, 360f));
            p.T.gameObject.SetActive(true);
        }

        // ---- public effects ----

        public void Shake(float amount)
        {
            _shake = Mathf.Max(_shake, amount);
        }

        /// <summary>The big one: smoke, fire and debris for the game-over explosion.</summary>
        public void Explosion(Vector3 pos)
        {
            var smoke = Shapes.Circle(48);
            var spark = Shapes.Spark(40);
            for (int i = 0; i < 26; i++)
            {
                float a = Range(0f, Mathf.PI * 2f);
                float sp = Range(1.4f, 6.5f);
                Spawn(pos, new Vector3(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp + 1.2f, 0f),
                      Range(0.45f, 1.05f), Range(0.25f, 0.7f), Range(0.9f, 1.9f),
                      new Color(1f, Range(0.55f, 0.9f), 0.25f, 0.95f),
                      new Color(0.25f, 0.25f, 0.28f, 0f), smoke, Range(-180f, 180f));
            }
            for (int i = 0; i < 18; i++)
            {
                float a = Range(0f, Mathf.PI * 2f);
                float sp = Range(3.5f, 10f);
                Spawn(pos, new Vector3(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp + 1.5f, 0f),
                      Range(0.3f, 0.7f), Range(0.2f, 0.45f), 0.02f,
                      new Color(1f, 0.95f, 0.6f, 1f), new Color(1f, 0.4f, 0.2f, 0f),
                      spark, Range(-360f, 360f));
            }
            Shake(1f);
        }

        /// <summary>Little sparks off the fuse while it burns.</summary>
        public void FuseSpark(Vector3 pos, float intensity)
        {
            if (Rand() > intensity * 0.5f) return;
            var spark = Shapes.Spark(40);
            float a = Range(Mathf.PI * 0.15f, Mathf.PI * 0.85f);
            float sp = Range(0.8f, 2.4f);
            Spawn(pos, new Vector3(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, 0f),
                  Range(0.18f, 0.38f), Range(0.07f, 0.16f), 0.01f,
                  new Color(1f, 0.92f, 0.5f, 1f), new Color(1f, 0.45f, 0.15f, 0f),
                  spark, Range(-300f, 300f));
        }

        /// <summary>A puff when the bomb lands in a pair of hands.</summary>
        public void CatchPuff(Vector3 pos)
        {
            var smoke = Shapes.Circle(48);
            for (int i = 0; i < 6; i++)
            {
                float a = Range(0f, Mathf.PI * 2f);
                Spawn(pos, new Vector3(Mathf.Cos(a) * Range(0.6f, 1.8f), Mathf.Sin(a) * Range(0.4f, 1.2f), 0f),
                      Range(0.22f, 0.42f), Range(0.12f, 0.3f), Range(0.5f, 0.9f),
                      new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0f), smoke, Range(-90f, 90f));
            }
            Shake(0.18f);
        }

        /// <summary>A green burst for a PERFECT throw.</summary>
        public void PerfectBurst(Vector3 pos)
        {
            var spark = Shapes.Spark(40);
            for (int i = 0; i < 12; i++)
            {
                float a = (i / 12f) * Mathf.PI * 2f + Range(-0.2f, 0.2f);
                float sp = Range(2.2f, 4.2f);
                Spawn(pos, new Vector3(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, 0f),
                      Range(0.26f, 0.5f), Range(0.14f, 0.26f), 0.01f,
                      new Color(0.6f, 1f, 0.65f, 1f), new Color(0.2f, 0.8f, 0.4f, 0f),
                      spark, Range(-200f, 200f));
            }
            Shake(0.22f);
        }

        /// <summary>Sparks off a shield when the bomb bounces.</summary>
        public void ShieldSparks(Vector3 pos)
        {
            var spark = Shapes.Spark(40);
            for (int i = 0; i < 14; i++)
            {
                float a = Range(0f, Mathf.PI * 2f);
                float sp = Range(1.8f, 5f);
                Spawn(pos, new Vector3(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, 0f),
                      Range(0.2f, 0.45f), Range(0.1f, 0.22f), 0.01f,
                      new Color(0.6f, 0.95f, 1f, 1f), new Color(0.2f, 0.6f, 1f, 0f),
                      spark, Range(-260f, 260f));
            }
            Shake(0.4f);
        }

        // ---- update ----

        public void Tick(float dt)
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                ref var p = ref _pool[i];
                if (!p.Active) continue;
                p.Life += dt;
                float u = p.Life / p.MaxLife;
                if (u >= 1f)
                {
                    p.Active = false;
                    p.T.gameObject.SetActive(false);
                    continue;
                }
                p.Vel += new Vector3(0f, -7.5f * dt, 0f);      // gravity
                p.Vel *= 1f - 2.2f * dt;                        // drag
                p.T.localPosition += p.Vel * dt;
                p.T.localRotation *= Quaternion.Euler(0f, 0f, p.Spin * dt);
                p.T.localScale = Vector3.one * Mathf.Lerp(p.StartScale, p.EndScale, u);
                p.R.color = Color.Lerp(p.From, p.To, u);
            }

            // Camera shake, decaying, using Perlin noise rather than the RNG so it is smooth.
            if (_shake > 0.0005f)
            {
                _shake = Mathf.MoveTowards(_shake, 0f, dt * _shakeDecay);
                float m = _shake * 0.42f;
                float t = Time.unscaledTime * 26f;
                float x = (Mathf.PerlinNoise(t, _noiseSeed) - 0.5f) * 2f * m;
                float y = (Mathf.PerlinNoise(_noiseSeed, t) - 0.5f) * 2f * m;
                float rz = (Mathf.PerlinNoise(t * 0.7f, _noiseSeed * 2f) - 0.5f) * 2f * m * 2.2f;
                _camera.localPosition = _camHome + new Vector3(x, y, 0f);
                _camera.localRotation = Quaternion.Euler(0f, 0f, rz);
            }
            else if (_camera.localPosition != _camHome)
            {
                _camera.localPosition = _camHome;
                _camera.localRotation = Quaternion.identity;
            }
        }

        public int ActiveParticles
        {
            get { int n = 0; for (int i = 0; i < _pool.Length; i++) if (_pool[i].Active) n++; return n; }
        }
    }
}
