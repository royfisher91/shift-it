using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KT.ShiftIt.Sim;

namespace KT.ShiftIt.Tests
{
    /// <summary>
    /// The shipped balance data and the C# defaults must not drift apart.
    ///
    /// The JSON is the live source of truth at runtime and the C# field initialisers are the
    /// fallback, so if they disagree the game behaves one way in a build and another way in every
    /// test in this suite - and the balance numbers reported to Roy would be measuring a config
    /// the game does not use. That is the "configured but not switched on" failure pointed at data.
    /// </summary>
    public class ConfigDataTests
    {
        static Dictionary<string, FieldInfo> Fields()
        {
            var d = new Dictionary<string, FieldInfo>();
            foreach (var f in typeof(SimConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (f.FieldType == typeof(float) || f.FieldType == typeof(int)) d[f.Name] = f;
            return d;
        }

        [Test]
        public void BalanceJson_ExistsAndParses()
        {
            var ta = Resources.Load<TextAsset>("shiftit_balance");
            Assert.IsNotNull(ta, "Assets/Resources/shiftit_balance.json is missing from the build");
            var cfg = JsonUtility.FromJson<SimConfig>(ta.text);
            Assert.IsNotNull(cfg, "shiftit_balance.json did not parse into a SimConfig");
        }

        [Test]
        public void BalanceJson_CoversEveryTunableField()
        {
            var ta = Resources.Load<TextAsset>("shiftit_balance");
            Assert.IsNotNull(ta);
            var missing = new List<string>();
            foreach (var name in Fields().Keys)
                if (!ta.text.Contains("\"" + name + "\"")) missing.Add(name);
            Assert.IsEmpty(missing, "fields absent from the JSON: " + string.Join(", ", missing));
        }

        [Test]
        public void BalanceJson_AgreesWithTheCSharpDefaults()
        {
            var ta = Resources.Load<TextAsset>("shiftit_balance");
            Assert.IsNotNull(ta);
            var fromJson = JsonUtility.FromJson<SimConfig>(ta.text);
            var defaults = new SimConfig();
            var drift = new List<string>();

            foreach (var kv in Fields())
            {
                object a = kv.Value.GetValue(fromJson);
                object b = kv.Value.GetValue(defaults);
                bool same = kv.Value.FieldType == typeof(int)
                    ? (int)a == (int)b
                    : Mathf.Abs((float)a - (float)b) < 1e-5f;
                if (!same) drift.Add(string.Format("{0}: json={1} code={2}", kv.Key, a, b));
            }
            Assert.IsEmpty(drift, "the JSON and the C# defaults have drifted:\n  " +
                                  string.Join("\n  ", drift));
        }

        [Test]
        public void EveryCC0AudioFileIsPresentAndLoadable()
        {
            // "An asset existing is not an asset playing" (KT_QUALITY_LESSONS A2). This at least
            // proves the files are in the build and decode; ViewTests proves they are wired.
            string[] names =
            {
                "Audio/music_zone1_factory",
                "Audio/music_zone2_rooftop",
                "Audio/music_zone3_scrapyard",
                "Audio/music_zone4_launchpad",
                "Audio/sfx_explosion",
            };
            var missing = new List<string>();
            foreach (var n in names)
            {
                var clip = Resources.Load<AudioClip>(n);
                if (clip == null) { missing.Add(n + " (missing)"); continue; }
                if (clip.length <= 0.05f) missing.Add(n + " (loaded but " + clip.length + "s long)");
            }
            Assert.IsEmpty(missing, "CC0 audio not usable: " + string.Join(", ", missing));
        }

        [Test]
        public void MusicLoopsAreLongEnoughToLoopWithoutGrating()
        {
            foreach (var n in new[] { "music_zone1_factory", "music_zone2_rooftop",
                                      "music_zone3_scrapyard", "music_zone4_launchpad" })
            {
                var clip = Resources.Load<AudioClip>("Audio/" + n);
                Assert.IsNotNull(clip, n + " is missing");
                Assert.Greater(clip.length, 10f, n + " is only " + clip.length + "s - too short to loop");
            }
        }

        [Test]
        public void ShippedConfig_IsSaneAndSafe()
        {
            var c = new SimConfig();
            Assert.Greater(c.fuseStart, c.fuseMin, "the fuse must shorten, not lengthen");
            Assert.Greater(c.fuseMin, 0f);
            Assert.Greater(c.returnDelayStart, c.returnDelayMin);
            Assert.Greater(c.bandHighStart, c.bandLow, "the PERFECT band must have width");
            Assert.Greater(c.bandHighEnd, c.bandLow, "the band must still have width when narrowed");
            Assert.Less(c.bandHighEnd, c.bandHighStart, "the band must narrow, not widen");
            Assert.Less(c.maxHoldSeconds, 8f,
                "Windows opens Filter Keys at 8 s of held Right Shift - this must stay well under it");
            Assert.Greater(c.holdWarningSeconds, 0f);
            Assert.Less(c.holdWarningSeconds, 8f, "the warning must arrive before Windows acts");
            Assert.AreEqual(4, c.zoneCount, "there are four zones");
            Assert.Greater(c.zoneEveryPasses, 0);
            Assert.Greater(c.modeASlots, 2, "Mode A needs an arc of robots");
            Assert.Less(c.modeASlotHalfWidthDeg * 2f * c.modeASlots, c.modeAArcHalfDeg * 2f + 1f,
                "the robot windows must fit inside the arc with gaps left between them");
        }
    }
}
