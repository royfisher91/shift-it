using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using KT.ShiftIt.Sim;
using KT.ShiftIt.Game;

namespace KT.ShiftIt.PlayTests
{
    /// <summary>
    /// Produces the visual-review evidence: the stills for the contact sheets and the frame
    /// sequences the GIFs are built from.
    ///
    /// These are captures, not assertions about fun - but they still assert that each capture
    /// actually happened, because a review sheet assembled from missing images is the "check that
    /// reported success without looking at the screen" failure with a picture attached.
    ///
    /// Every scenario is forced through the real CONFIG, so what is photographed is the real game
    /// in a real state, not a mock-up.
    /// </summary>
    public class CaptureTests : InputTestFixture
    {
        Keyboard _kb;
        GameRoot _root;

        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
            GameRoot.TestMode = true;
            GameRoot.ClearSavedProgress();
        }

        public override void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _root = null;
            GameRoot.ConfigOverride = null;
            Time.captureDeltaTime = 0f;
            base.TearDown();
        }

        static SimConfig Plain()
        {
            var c = new SimConfig();
            c.chanceDud = 0f; c.chanceHot = 0f; c.chanceMirrored = 0f;
            c.chanceBothBlocked = 0f; c.chanceSwap = 0f;
            c.blockerChanceAtUnlock = 0f; c.blockerChanceAtEnd = 0f;
            return c;
        }

        GameRoot Boot(SimConfig cfg, ControlMode mode, int seed = 1111)
        {
            GameRoot.ConfigOverride = cfg;
            var go = new GameObject("CaptureRoot");
            go.SetActive(false);
            var root = go.AddComponent<GameRoot>();
            root.Seed = seed;
            root.StartMuted = true;
            go.SetActive(true);
            root.SetMode(mode);
            _root = root;
            return root;
        }

        IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>
        /// Advance n frames while playing properly: throw at an open robot once the fuse is past
        /// halfway, hold duds until they fizzle, and wait out a both-blocked turn until it opens.
        /// Used wherever a capture needs the game to still be alive when the shutter falls.
        /// </summary>
        IEnumerator PlayFrames(int n)
        {
            for (int i = 0; i < n; i++)
            {
                var sim = _root.Sim;
                if (sim.Phase == Phase.PlayerHolding && sim.Bomb != BombKind.Dud)
                {
                    int open = sim.Turn.FirstOpenSlot();
                    if (open >= 0 && sim.FuseFraction < 0.55f)
                    {
                        int logical = sim.Turn.Mirrored ? 1 - open : open;
                        yield return Tap(logical == 0 ? _kb.leftShiftKey : _kb.rightShiftKey);
                        i += 2;
                        continue;
                    }
                }
                else if (sim.Phase == Phase.GameOver)
                {
                    yield return Tap(_kb.leftShiftKey);
                    i += 2;
                    continue;
                }
                yield return null;
            }
        }

        IEnumerator Tap(ButtonControl k)
        {
            Press(k); yield return null;
            Release(k); yield return null;
        }

        IEnumerator Until(System.Func<bool> cond, float timeout, string what)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(cond(), "timed out waiting for: " + what);
        }

        void Shoot(string name, string caption)
        {
            string path = Capture.Shot(_root.Cam, name);
            Assert.IsNotNull(path, "capture failed: " + name);
            System.IO.File.WriteAllText(
                System.IO.Path.ChangeExtension(path, ".txt"), caption);
        }

        // =====================================================================
        //  Contact sheet 1 - overview: every zone, both bombs, every blocker tell
        // =====================================================================

        [UnityTest]
        public IEnumerator Capture_AllFourZones()
        {
            Capture.Session = "sheet_zones";
            // Zone changes are driven by passes, so walk the game forward by throwing cleanly.
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);

            string[] names = { "zone1_factory_floor", "zone2_rooftop", "zone3_scrapyard", "zone4_launchpad" };
            string[] caps =
            {
                "ZONE 1 FACTORY FLOOR (passes 0-14) - teal panels, pipes, 3 flickering lamps, hazard floor, safety sign",
                "ZONE 2 ROOFTOP (15-29) - sunset gradient, low sun, lit skyline, ledge, spinning AC fans",
                "ZONE 3 SCRAPYARD (30-44) - storm sky, drifting cloud, rain, lightning, rusty crane, tyres, puddles",
                "ZONE 4 LAUNCHPAD (45+) - night sky, twinkling stars, floodlight beams, red gantry, KT-1 rocket",
            };

            for (int z = 0; z < 4; z++)
            {
                yield return Until(() => root.Sim.Zone == z || root.Sim.Phase == Phase.GameOver,
                                   90f, "zone " + z);
                Assert.AreNotEqual(Phase.GameOver, root.Sim.Phase,
                    "the capture run died before reaching zone " + (z + 1));
                // The wipe runs 0.85 s and the banner 1.5 s after it, so the shot has to wait for
                // both - but the game does not stop while it waits. Waiting with Frames() let the
                // fuse run out and killed the run before zone 4, which the before/after sheet
                // caught as a zone 4 image that had not changed at all.
                yield return PlayFrames(170);
                Shoot(names[z], caps[z] + "  [pass " + root.Sim.Passes + "]");

                if (z == 3) break;
                int guard = 0;
                while (root.Sim.Zone == z && root.Sim.Phase != Phase.GameOver && guard++ < 3000)
                    yield return PlayFrames(1);
            }
        }

        [UnityTest]
        public IEnumerator Capture_EveryBlockerTellAndBothSpecialBombs()
        {
            Capture.Session = "sheet_tells";

            yield return ShootForcedBlocker(Blocker.Shield, "tell_shield",
                "SHIELD - cyan energy arc in front, red X eyes, SHIELD sign. Bomb bounces back.");
            yield return ShootForcedBlocker(Blocker.Asleep, "tell_asleep",
                "ASLEEP - eyes shut, floating Zzz, and the idle bob STOPS. Bomb bounces back.");
            yield return ShootForcedBlocker(Blocker.OnBreak, "tell_on_break",
                "ON BREAK - robot gone, dashed pad outline and ON BREAK sign. Bomb hits the floor.");

            // HOT bomb
            var c = Plain(); c.unlockHot = 0; c.chanceHot = 1f;
            var root = Boot(c, ControlMode.LeftRight, 222);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(6);
            Assert.AreEqual(BombKind.Hot, root.Sim.Bomb, "the HOT bomb was not forced");
            Shoot("bomb_hot", "RED HOT bomb - glowing red, HOT label, HALF the fuse, DOUBLE points.");
            Object.DestroyImmediate(root.gameObject); _root = null;

            // DUD bomb
            c = Plain(); c.unlockDud = 0; c.chanceDud = 1f;
            root = Boot(c, ControlMode.LeftRight, 333);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(6);
            Assert.AreEqual(BombKind.Dud, root.Sim.Bomb, "the DUD bomb was not forced");
            Shoot("bomb_dud", "GREEN DUD - DUD label, no green band on the ring, and it does not tick. Hold it: +2.");
        }

        IEnumerator ShootForcedBlocker(Blocker kind, string name, string caption)
        {
            var c = Plain();
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f; c.blockerRampStartPass = 0;
            c.unlockShield = kind == Blocker.Shield ? 0 : int.MaxValue;
            c.unlockAsleep = kind == Blocker.Asleep ? 0 : int.MaxValue;
            c.unlockOnBreak = kind == Blocker.OnBreak ? 0 : int.MaxValue;

            var root = Boot(c, ControlMode.LeftRight, 777);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(10);
            int blocked = root.Sim.Turn.Slots[0] != Blocker.None ? 0 : 1;
            Assert.AreEqual(kind, root.Sim.Turn.Slots[blocked], "forced blocker " + kind + " did not appear");
            Shoot(name, caption);
            Object.DestroyImmediate(root.gameObject); _root = null;
        }

        [UnityTest]
        public IEnumerator Capture_MirroredAndBothBlockedAndModeA()
        {
            Capture.Session = "sheet_tells";

            // MIRRORED
            var c = Plain(); c.unlockMirrored = 0; c.chanceMirrored = 1f;
            var root = Boot(c, ControlMode.LeftRight, 444);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(8);
            Assert.IsTrue(root.Sim.Turn.Mirrored, "MIRRORED was not forced");
            Shoot("tell_mirrored",
                  "MIRRORED - the L-SHIFT / R-SHIFT labels have SWAPPED sides and turned pink, plus a MIRRORED tag.");
            Object.DestroyImmediate(root.gameObject); _root = null;

            // BOTH BLOCKED, before and after the open
            c = Plain(); c.unlockBothBlocked = 0; c.chanceBothBlocked = 1f;
            root = Boot(c, ControlMode.LeftRight, 555);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(8);
            Assert.IsTrue(root.Sim.Turn.BothBlocked, "BOTH BLOCKED was not forced");
            Shoot("tell_both_blocked", "BOTH SIDES BLOCKED - two shields up. You cannot throw yet; you have to wait.");
            yield return Until(() => root.Sim.Turn.Opened || root.Sim.Phase != Phase.PlayerHolding,
                               6f, "one side to open");
            yield return Frames(3);
            Shoot("tell_both_blocked_open", "...and one side OPENS part way through the fuse, with an OPEN! pop and a clang.");
            Object.DestroyImmediate(root.gameObject); _root = null;

            // MODE A arc and aim arrow
            root = Boot(Plain(), ControlMode.HoldRelease, 666);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            Press(_kb.leftShiftKey);
            yield return Frames(14);
            Shoot("modeA_aim_arrow",
                  "MODE A - five robots in an arc; the arrow sweeps and turns GREEN over a catchable robot, RED over a gap.");
            yield return Until(() => root.Sim.SlotForAim(root.Sim.AimDeg) < 0, 3f, "the arrow to reach a gap");
            yield return Frames(1);
            Shoot("modeA_aim_over_gap", "MODE A - the same arrow over a GAP between two robots: red, and releasing here loses.");
            Release(_kb.leftShiftKey);
            yield return Frames(2);
        }

        // =====================================================================
        //  Contact sheet 2 - close-ups of the moments
        // =====================================================================

        [UnityTest]
        public IEnumerator Capture_ThrowCatchPerfectExplosionUi()
        {
            Capture.Session = "sheet_moments";
            var root = Boot(Plain(), ControlMode.LeftRight, 888);
            yield return Frames(3);
            Shoot("ui_title", "TITLE - logo, tagline, the live mode, blinking PRESS SHIFT TO START, Sticky Keys tip.");

            yield return Tap(_kb.leftShiftKey);
            yield return Frames(2);
            Shoot("moment_catch", "CATCH - bomb in hand, fuse ring full, HUD: score top-left, BEST top-right, zone top-centre.");

            yield return Until(() => root.Sim.FuseFraction <= root.Sim.BandHigh, 5f, "the green band");
            Shoot("moment_in_band", "IN THE GREEN - the ring tip has entered the band, which brightens and pops. Throw NOW.");

            yield return Tap(_kb.leftShiftKey);
            yield return Frames(2);
            Shoot("moment_perfect", "PERFECT - green burst, PERFECT! pop, floating +2, combo counter appears under the score.");
            yield return Frames(6);
            Shoot("moment_throw_arc", "THROW - the bomb arcs across to the receiver, which is already panicking.");

            // Build a combo so the ladder is visible.
            for (int i = 0; i < 4; i++)
            {
                yield return Until(() => root.Sim.Phase == Phase.PlayerHolding, 5f, "the bomb back");
                yield return Until(() => root.Sim.FuseFraction <= root.Sim.BandHigh, 5f, "the band");
                yield return Tap(_kb.leftShiftKey);
            }
            yield return Frames(2);
            Shoot("moment_combo", "COMBO - five PERFECTs in a row: COMBO x5 and a +6 pop, the maximum.");

            // Die, and photograph the explosion and the card.
            yield return Until(() => root.Sim.Phase == Phase.PlayerHolding, 5f, "the bomb back");
            yield return Until(() => root.Sim.Phase == Phase.Failing, 6f, "the fuse to run out");
            yield return Frames(9);     // 3 frames in, nothing had expanded yet
            Shoot("moment_explosion", "EXPLOSION - smoke and fire particles, white flash, screen shake, dead X-eyed robots.");
            yield return Until(() => root.Sim.Phase == Phase.GameOver, 4f, "the game-over card");
            yield return Frames(2);
            Shoot("ui_game_over",
                  "GAME OVER - BOOM!, the reason line, score and passes, personal best, blinking PRESS SHIFT TO RETRY.");
        }

        // =====================================================================
        //  GIF frame sequences, one per mode
        // =====================================================================

        [UnityTest]
        public IEnumerator Capture_GifFrames_ModeB()
        {
            yield return GifRun(ControlMode.LeftRight, "gif_modeB", 150);
        }

        [UnityTest]
        public IEnumerator Capture_GifFrames_ModeA()
        {
            yield return GifRun(ControlMode.HoldRelease, "gif_modeA", 150);
        }

        /// <summary>
        /// Plays the real game through the real key path and captures a fixed-timestep frame
        /// sequence. Time.captureDeltaTime pins the game clock to the GIF's frame rate, so the
        /// result plays back at true speed however slowly the capture itself runs.
        /// </summary>
        IEnumerator GifRun(ControlMode mode, string session, int frames)
        {
            Capture.Session = session;
            var cfg = new SimConfig();
            var root = Boot(cfg, mode, mode == ControlMode.LeftRight ? 2026 : 1102);
            yield return Frames(3);

            Time.captureDeltaTime = 1f / 20f;               // 20 fps GIF
            int shot = 0;
            bool started = false;
            int modeAHoldFor = 0;

            for (int f = 0; f < frames; f++)
            {
                var sim = root.Sim;

                if (!started && sim.Phase == Phase.Title)
                {
                    yield return Tap(_kb.leftShiftKey);
                    started = true;
                }
                else if (sim.Phase == Phase.GameOver)
                {
                    yield return Tap(_kb.leftShiftKey);
                }
                else if (sim.Phase == Phase.PlayerHolding)
                {
                    if (mode == ControlMode.LeftRight)
                    {
                        int open = sim.Turn.FirstOpenSlot();
                        bool dud = sim.Bomb == BombKind.Dud;
                        if (!dud && open >= 0 && sim.FuseFraction <= sim.BandHigh)
                        {
                            int logical = sim.Turn.Mirrored ? 1 - open : open;
                            yield return Tap(logical == 0 ? _kb.leftShiftKey : _kb.rightShiftKey);
                        }
                    }
                    else
                    {
                        // Mode A: start holding once the fuse has burned a little, then release
                        // when the arrow is over an open robot.
                        if (!sim.Holding && sim.Bomb != BombKind.Dud && sim.FuseFraction < 0.75f)
                        {
                            Press(_kb.leftShiftKey);
                            modeAHoldFor = 0;
                        }
                        else if (sim.Holding)
                        {
                            modeAHoldFor++;
                            int over = sim.SlotForAim(sim.AimDeg);
                            bool good = over >= 0 && over < sim.Turn.SlotCount &&
                                        sim.Turn.Slots[over] == Blocker.None;
                            if (good || modeAHoldFor > 40) Release(_kb.leftShiftKey);
                        }
                    }
                }

                yield return null;
                string p = Capture.Shot(root.Cam, string.Format("f{0:000}", shot), 640, 360);
                Assert.IsNotNull(p, "gif frame " + shot + " failed to capture");
                shot++;
            }

            Time.captureDeltaTime = 0f;
            Debug.Log("[SHIFTIT] gif frames captured: " + shot + " into " + Capture.SessionDir +
                      " (reached pass " + root.Sim.Passes + ")");
            Assert.Greater(shot, 100, "too few frames for a usable GIF");
        }
    }
}
