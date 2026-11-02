using System.Collections.Generic;
using UnityEngine;

namespace KT.ShiftIt.Game
{
    /// <summary>Named feedback events. Gameplay says Play(Sfx.Perfect) and never touches a clip.</summary>
    public enum Sfx
    {
        Tick, Throw, Catch, Perfect, ShieldClang, SwapClang, OpenPop,
        DudFizzle, FailBuzz, Explosion, ZoneJingle, UiHover, UiPress,
    }

    /// <summary>
    /// Sound, behind a named-event API so no gameplay code ever references a clip
    /// (pattern 10's first red flag).
    ///
    /// STAGE 1 generates every sound procedurally. That is deliberate, for three reasons:
    ///   1. It proves the wiring is actually live. KT_QUALITY_LESSONS A2: Chippy shipped a complete
    ///      UI sound set that never played once. A silent stub would have the same bug and look fine.
    ///   2. Roy's library has no explosion and no music at all (see Docs/AUDIO_NEEDS.md), so the
    ///      most important sound in this game could not be wired from it today regardless.
    ///   3. Generated audio is original, so there is no licence question for the public jam repo -
    ///      which matters, because the library's clips are NOT cleared for redistribution.
    /// Stage 2 swaps real clips in behind this same API; the call sites do not change.
    ///
    /// Audio stops when the window loses focus and on quit, which is a standing KT requirement.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int Rate = 44100;

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        AudioSource[] _voices;
        int _next;
        System.Random _cosmeticRng;      // never the gameplay stream (quality lesson D9)
        bool _muted;

        public bool Muted
        {
            get => _muted;
            set { _muted = value; AudioListener.volume = value ? 0f : 1f; }
        }

        public static AudioService Create(Transform parent, bool muted)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AudioService>();
            a._cosmeticRng = new System.Random(20261102);
            a.Build();
            a.Muted = muted;
            return a;
        }

        void Build()
        {
            // A small pool of voices, so nothing ever allocates a source mid-play.
            _voices = new AudioSource[10];
            for (int i = 0; i < _voices.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _voices[i] = src;
            }

            _clips[Sfx.Tick] = Tick();
            _clips[Sfx.Throw] = Whoosh();
            _clips[Sfx.Catch] = Thunk();
            _clips[Sfx.Perfect] = Ding();
            _clips[Sfx.ShieldClang] = Clang(1f);
            _clips[Sfx.SwapClang] = Clang(1.45f);
            _clips[Sfx.OpenPop] = Pop();
            _clips[Sfx.DudFizzle] = Fizzle();
            _clips[Sfx.FailBuzz] = Buzz();
            _clips[Sfx.Explosion] = Explosion();
            _clips[Sfx.ZoneJingle] = Jingle();
            _clips[Sfx.UiHover] = Tick();
            _clips[Sfx.UiPress] = Pop();
        }

        /// <summary>Play a named event. Pitch varies slightly so a repeated sound never grates.</summary>
        public void Play(Sfx s, float pitch = 1f, float volume = 1f)
        {
            if (_muted) return;
            if (!_clips.TryGetValue(s, out var clip) || clip == null) return;
            var src = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            src.clip = clip;
            // Pattern 10 item 6: vary, do not repeat.
            float jitter = (float)(_cosmeticRng.NextDouble() - 0.5) * 0.06f;
            src.pitch = Mathf.Clamp(pitch + jitter, 0.25f, 3f);
            src.volume = Mathf.Clamp01(volume);
            src.Play();
        }

        public void StopAll()
        {
            for (int i = 0; i < _voices.Length; i++) if (_voices[i] != null) _voices[i].Stop();
        }

        void OnApplicationFocus(bool focused)
        {
            // Standing KT rule: game sound never plays when the window is not focused.
            AudioListener.pause = !focused;
            if (!focused) StopAll();
        }

        void OnApplicationQuit()
        {
            StopAll();
            AudioListener.volume = 0f;
        }

        // =====================================================================
        //  Tiny procedural synth. Everything below returns an AudioClip.
        // =====================================================================

        static AudioClip Make(string name, float seconds, System.Func<float, float, float> sample)
        {
            int n = Mathf.Max(1, (int)(seconds * Rate));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                data[i] = Mathf.Clamp(sample(t, t / seconds), -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(ref uint state)
        {
            state ^= state << 13; state ^= state >> 17; state ^= state << 5;
            return (state / (float)uint.MaxValue) * 2f - 1f;
        }

        static AudioClip Tick()
        {
            uint st = 12345;
            return Make("tick", 0.055f, (t, u) =>
            {
                float env = Mathf.Exp(-u * 26f);
                float body = Mathf.Sin(2f * Mathf.PI * 1800f * t);
                return (body * 0.5f + Noise(ref st) * 0.5f) * env * 0.5f;
            });
        }

        static AudioClip Whoosh()
        {
            uint st = 999;
            float lp = 0f;
            return Make("throw", 0.30f, (t, u) =>
            {
                float env = Mathf.Sin(u * Mathf.PI);
                float n = Noise(ref st);
                // A one-pole low-pass that opens up as the throw accelerates.
                float k = Mathf.Lerp(0.04f, 0.45f, u);
                lp += (n - lp) * k;
                return lp * env * 0.55f;
            });
        }

        static AudioClip Thunk()
        {
            uint st = 4242;
            return Make("catch", 0.16f, (t, u) =>
            {
                float env = Mathf.Exp(-u * 11f);
                float low = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(190f, 90f, u) * t);
                float click = Noise(ref st) * Mathf.Exp(-u * 60f);
                return (low * 0.75f + click * 0.3f) * env * 0.8f;
            });
        }

        static AudioClip Ding()
        {
            return Make("perfect", 0.42f, (t, u) =>
            {
                float env = Mathf.Exp(-u * 6.5f);
                float a = Mathf.Sin(2f * Mathf.PI * 1318.5f * t);          // E6
                float b = Mathf.Sin(2f * Mathf.PI * 1976f * t) * 0.45f;    // B6
                float c = Mathf.Sin(2f * Mathf.PI * 2637f * t) * 0.2f;
                return (a + b + c) * env * 0.33f;
            });
        }

        static AudioClip Clang(float scale)
        {
            // Inharmonic partials are what make metal sound like metal.
            float[] f = { 520f * scale, 1210f * scale, 1870f * scale, 2560f * scale };
            return Make("clang" + scale, 0.45f, (t, u) =>
            {
                float s = 0f;
                for (int i = 0; i < f.Length; i++)
                    s += Mathf.Sin(2f * Mathf.PI * f[i] * t) * Mathf.Exp(-u * (5f + i * 3.5f));
                return s * 0.22f;
            });
        }

        static AudioClip Pop()
        {
            return Make("pop", 0.14f, (t, u) =>
            {
                float env = Mathf.Exp(-u * 13f);
                float pitch = Mathf.Lerp(420f, 1050f, u);
                return Mathf.Sin(2f * Mathf.PI * pitch * t) * env * 0.55f;
            });
        }

        static AudioClip Fizzle()
        {
            uint st = 7777;
            float lp = 0f;
            return Make("fizzle", 0.75f, (t, u) =>
            {
                float env = (1f - u) * (1f - u);
                lp += (Noise(ref st) - lp) * 0.6f;
                return lp * env * 0.4f;
            });
        }

        static AudioClip Buzz()
        {
            return Make("buzz", 0.34f, (t, u) =>
            {
                float env = u < 0.85f ? 1f : (1f - u) / 0.15f;
                float sq = Mathf.Sin(2f * Mathf.PI * 118f * t) >= 0f ? 1f : -1f;
                float sq2 = Mathf.Sin(2f * Mathf.PI * 59f * t) >= 0f ? 1f : -1f;
                return (sq * 0.5f + sq2 * 0.5f) * env * 0.33f;
            });
        }

        static AudioClip Explosion()
        {
            uint st = 31337;
            float lp = 0f;
            return Make("explosion", 1.25f, (t, u) =>
            {
                float crack = Mathf.Exp(-u * 30f);
                float body = Mathf.Exp(-u * 3.2f);
                float n = Noise(ref st);
                lp += (n - lp) * Mathf.Lerp(0.5f, 0.03f, u);     // darkens as it decays
                float rumble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 32f, u) * t);
                return (n * crack * 0.6f + lp * body * 0.8f + rumble * body * 0.5f) * 0.72f;
            });
        }

        static AudioClip Jingle()
        {
            // Three rising notes: the SHIFT! sting.
            float[] notes = { 523.25f, 659.25f, 880f };     // C5 E5 A5
            const float step = 0.13f;
            return Make("jingle", step * 3f + 0.25f, (t, u) =>
            {
                float s = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    float start = i * step;
                    if (t < start) continue;
                    float lt = t - start;
                    float env = Mathf.Exp(-lt * 7f);
                    s += Mathf.Sin(2f * Mathf.PI * notes[i] * lt) * env;
                }
                return s * 0.28f;
            });
        }
    }
}
