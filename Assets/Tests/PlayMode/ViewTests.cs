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
    /// Checks that the SCREEN reflects the simulation.
    ///
    /// This is the file that guards against KT's most expensive recurring failure: something
    /// configured and never switched on (quality lessons A1 and A2 - Chippy's post-processing that
    /// was never enabled and its complete UI sound set that never played once). A test that reads
    /// the simulation back to itself cannot catch that. Every assertion here reads a renderer, a
    /// fill amount or the actual string a player would read.
    ///
    /// It is also how the blocker tells are verified. A tell that does not render is not a missing
    /// art asset, it is an unfair death.
    /// </summary>
    public class ViewTests : InputTestFixture
    {
        Keyboard _kb;
        GameRoot _root;

        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
            GameRoot.TestMode = true;
            GameRoot.ClearSavedProgress();
            Capture.Session = "viewtests";
        }

        public override void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _root = null;
            GameRoot.ConfigOverride = null;
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

        static SimConfig ForcedBlocker(Blocker kind)
        {
            var c = Plain();
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f; c.blockerRampStartPass = 0;
            c.unlockShield = kind == Blocker.Shield ? 0 : int.MaxValue;
            c.unlockAsleep = kind == Blocker.Asleep ? 0 : int.MaxValue;
            c.unlockOnBreak = kind == Blocker.OnBreak ? 0 : int.MaxValue;
            return c;
        }

        GameRoot Boot(SimConfig cfg, ControlMode mode, int seed = 9090)
        {
            GameRoot.ConfigOverride = cfg;
            var go = new GameObject("ViewRoot");
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

        /// <summary>The receiver carrying the forced blocker.</summary>
        static RobotView Blocked(GameRoot root)
        {
            int i = root.Sim.Turn.Slots[0] != Blocker.None ? 0 : 1;
            return root.Board.Receivers[i];
        }

        static RobotView Open(GameRoot root)
        {
            int i = root.Sim.Turn.Slots[0] != Blocker.None ? 1 : 0;
            return root.Board.Receivers[i];
        }

        // =====================================================================
        //  The HUD shows the real numbers
        // =====================================================================

        [UnityTest]
        public IEnumerator TheRenderedFrameIsNotBlank_InEveryPhaseAndEveryZone()
        {
            // Reads the PIXELS, which is the only check that can catch a dead camera, a canvas
            // rendering nowhere, or a font that silently failed. Every other test in this file
            // stands on this one.
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(4);

            float title = Capture.FrameContrast(root.Cam);
            Debug.Log("[SHIFTIT] frame contrast, title screen: " + title.ToString("0.0000"));
            Assert.Greater(title, 0.02f, "the title screen rendered as a flat colour");

            yield return Tap(_kb.leftShiftKey);
            yield return Frames(4);
            float play = Capture.FrameContrast(root.Cam);
            Debug.Log("[SHIFTIT] frame contrast, in play:     " + play.ToString("0.0000"));
            Assert.Greater(play, 0.02f, "the playfield rendered as a flat colour");

            // And every zone must draw something, not just zone 1.
            for (int z = 1; z < 4; z++)
            {
                root.Zones.Show(z);
                yield return Frames(3);
                float c = Capture.FrameContrast(root.Cam);
                Debug.Log("[SHIFTIT] frame contrast, zone " + (z + 1) + ":      " + c.ToString("0.0000"));
                Assert.Greater(c, 0.02f, "zone " + (z + 1) + " rendered as a flat colour");
            }
        }

        [UnityTest]
        public IEnumerator AFontActuallyLoaded_OrEveryTextCheckIsWorthless()
        {
            // UI Text with a null font renders nothing at all while `text` still returns the
            // right string, so every other assertion in this file would pass over a blank
            // screen. This test is what makes the rest of them mean anything.
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            Assert.IsTrue(Label.FontLoaded,
                "no font loaded - all UI text would be invisible. Source: " + Label.FontSource);
            Assert.IsNotNull(root.Hud.ScoreText);
            Debug.Log("[SHIFTIT] font in use: " + Label.FontSource);
        }

        [UnityTest]
        public IEnumerator Hud_ShowsTheActualScoreComboPassesAndZone()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            Assert.IsTrue(root.Hud.TitleShown, "the title panel is not on screen at boot");

            yield return Tap(_kb.leftShiftKey);
            yield return Frames(2);
            Assert.IsFalse(root.Hud.TitleShown, "the title panel did not go away");
            Assert.IsTrue(root.Hud.ScoreShown, "the score is not on screen during play");
            Assert.AreEqual("0", root.Hud.ScoreText, "the score should read 0 at the start");
            Assert.AreEqual("FACTORY FLOOR", root.Hud.ZoneText, "zone 1 is the factory floor");
            Assert.AreEqual("", root.Hud.ComboText, "there is no combo yet, so none should be shown");

            // A PERFECT must move the score AND raise a combo readout.
            yield return Until(() => root.Sim.FuseFraction <= root.Sim.BandHigh, 5f, "the green band");
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(2);
            Assert.AreEqual("2", root.Hud.ScoreText, "a first PERFECT is 2 points and the HUD must say so");
            Assert.AreEqual("COMBO x1", root.Hud.ComboText, "the combo readout did not appear");
            Assert.AreEqual("1 PASS", root.Hud.PassesText, "the passes readout is wrong");
            Assert.AreEqual("BEST 0", root.Hud.BestText);
        }

        [UnityTest]
        public IEnumerator Hud_ShowsTheFailureReasonThePlayerActuallyCaused()
        {
            var root = Boot(ForcedBlocker(Blocker.Shield), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(4);

            int blocked = root.Sim.Turn.Slots[0] != Blocker.None ? 0 : 1;
            yield return Tap(blocked == 0 ? _kb.leftShiftKey : _kb.rightShiftKey);
            yield return Until(() => root.Hud.GameOverShown, 4f, "the game-over card");

            Assert.AreEqual(HudView.ReasonLine(FailReason.ThrewAtShield), root.Hud.ReasonText,
                "the game-over card must name the real cause");
            StringAssert.Contains("shield", root.Hud.ReasonText.ToLowerInvariant());
        }

        [UnityTest]
        public IEnumerator Hud_FirstCatchHintIsActuallyOnScreen()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(3);
            Assert.AreEqual("LEFT or RIGHT SHIFT!", root.Hud.HintText,
                "the first-catch hint is not visible in Mode B");
        }

        // =====================================================================
        //  Every blocker tell actually renders, and renders differently
        // =====================================================================

        [UnityTest]
        public IEnumerator Tell_Shield_RendersAnArcAndRedXEyes()
        {
            var root = Boot(ForcedBlocker(Blocker.Shield), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(20);                       // the arc fades in

            var bad = Blocked(root);
            var good = Open(root);
            Assert.Greater(bad.ShieldAlpha, 0.5f, "the shield arc is not visible on the blocked robot");
            Assert.Less(good.ShieldAlpha, 0.1f, "the OPEN robot is showing a shield arc");
            Assert.Greater(bad.EyeColour.r, 0.8f, "shielded eyes should be red");
            Assert.Less(bad.EyeColour.g, 0.6f, "shielded eyes should be red, not white");
            Assert.AreEqual("SHIELD", bad.SignText);
            Assert.IsTrue(bad.BodyVisible, "a shielded robot is still there");
        }

        [UnityTest]
        public IEnumerator Tell_Asleep_ShutsTheEyesShowsZzzAndStopsTheBob()
        {
            var root = Boot(ForcedBlocker(Blocker.Asleep), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(20);

            var bad = Blocked(root);
            var good = Open(root);
            Assert.Less(bad.EyeScaleY, 0.4f, "a sleeping robot's eyes must be shut");
            Assert.Greater(good.EyeScaleY, 0.6f, "the awake robot's eyes must be open");
            Assert.Less(bad.ShieldAlpha, 0.1f, "a sleeping robot has no shield");

            // The Zzz marks pulse, so watch for a peak rather than sampling one frame.
            float peak = 0f;
            for (int i = 0; i < 90; i++)
            {
                peak = Mathf.Max(peak, bad.ZzzAlpha);
                yield return null;
            }
            Assert.Greater(peak, 0.3f, "the floating Zzz never became visible");

            // The absence of the idle bob is part of the tell, so prove the body is still.
            float y0 = bad.transform.localPosition.y;
            float drift = 0f;
            for (int i = 0; i < 40; i++)
            {
                drift = Mathf.Max(drift, Mathf.Abs(bad.transform.localPosition.y - y0));
                yield return null;
            }
            Assert.Less(drift, 0.01f, "a sleeping robot must not bob; it drifted " + drift);
        }

        [UnityTest]
        public IEnumerator Tell_OnBreak_HidesTheRobotAndShowsADashedPadAndSign()
        {
            var root = Boot(ForcedBlocker(Blocker.OnBreak), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(20);

            var bad = Blocked(root);
            var good = Open(root);
            Assert.IsFalse(bad.BodyVisible, "an ON BREAK spot must be empty - the robot is still drawn");
            Assert.IsTrue(good.BodyVisible, "the other robot should still be there");
            Assert.Greater(bad.EmptyPadAlpha, 0.5f, "the dashed pad outline is not visible");
            Assert.AreEqual("ON BREAK", bad.SignText);
        }

        [UnityTest]
        public IEnumerator Tell_Mirrored_SwapsTheKeyLabelsAndTurnsThemPink()
        {
            var c = Plain();
            c.unlockMirrored = 0; c.chanceMirrored = 1f;
            var root = Boot(c, ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(4);

            Assert.IsTrue(root.Sim.Turn.Mirrored, "MIRRORED was not forced");
            // The brief: the labels swap and turn pink. On a mirrored turn the LEFT key throws
            // right, so the left-hand robot must be labelled R-SHIFT.
            Assert.AreEqual("R-SHIFT", root.Board.KeyLabelText(0),
                "the left robot's label did not swap on a mirrored turn");
            Assert.AreEqual("L-SHIFT", root.Board.KeyLabelText(1),
                "the right robot's label did not swap on a mirrored turn");
            var col = root.Board.KeyLabelColour(0);
            Assert.Greater(col.r, 0.8f, "a mirrored label should be pink");
            Assert.Less(col.g, 0.7f, "a mirrored label should be pink, not white");
            Assert.AreEqual("MIRRORED", root.Board.MirrorTagText, "the MIRRORED tag is not on screen");
        }

        [UnityTest]
        public IEnumerator KeyLabels_AreAlwaysShownInModeB_AndNormallyUnswapped()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(4);
            Assert.AreEqual("L-SHIFT", root.Board.KeyLabelText(0), "the left robot must be labelled L-SHIFT");
            Assert.AreEqual("R-SHIFT", root.Board.KeyLabelText(1), "the right robot must be labelled R-SHIFT");
            Assert.AreEqual("", root.Board.MirrorTagText, "no MIRRORED tag on an ordinary turn");
        }

        // =====================================================================
        //  The bomb and the fuse ring
        // =====================================================================

        [UnityTest]
        public IEnumerator FuseRing_TracksTheFuseAndPaintsTheBand()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(3);

            var ring = root.Board.Ring;
            Assert.Greater(ring.Alpha, 0.5f, "the fuse ring is not visible while holding the bomb");
            Assert.IsTrue(ring.BandShown, "the green band is not drawn");
            Assert.AreEqual(root.Sim.BandHigh - root.Sim.BandLow, ring.BandFill, 0.01f,
                "the painted band is not the width of the PERFECT window");

            // The arc must follow the fuse down, not sit still.
            float first = ring.FuseFill;
            yield return Frames(30);
            Assert.Less(ring.FuseFill, first, "the fuse arc is not shrinking");
            Assert.AreEqual(root.Sim.FuseFraction, ring.FuseFill, 0.02f,
                "the fuse arc does not match the real fuse");
        }

        [UnityTest]
        public IEnumerator DudBomb_LooksGreen_SaysDud_AndShowsNoBand()
        {
            var c = Plain(); c.unlockDud = 0; c.chanceDud = 1f;
            var root = Boot(c, ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(4);

            Assert.AreEqual("DUD", root.Board.BombTagText, "the dud is not labelled");
            var col = root.Board.BombColour;
            Assert.Greater(col.g, col.r, "a dud bomb must read green");
            Assert.Greater(col.g, col.b, "a dud bomb must read green");
            Assert.IsFalse(root.Board.Ring.BandShown,
                "a dud must show NO green band: the correct play is to do nothing");
        }

        [UnityTest]
        public IEnumerator HotBomb_LooksRedAndSaysHot()
        {
            var c = Plain(); c.unlockHot = 0; c.chanceHot = 1f;
            var root = Boot(c, ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(4);

            Assert.AreEqual("HOT", root.Board.BombTagText, "the hot bomb is not labelled");
            var col = root.Board.BombColour;
            Assert.Greater(col.r, col.g, "a hot bomb must read red");
            Assert.Greater(col.r, col.b, "a hot bomb must read red");
            Assert.IsTrue(root.Board.Ring.BandShown, "a hot bomb still has a PERFECT band");
        }

        // =====================================================================
        //  Mode A: the arrow tells you before you commit
        // =====================================================================

        [UnityTest]
        public IEnumerator ModeA_AimArrowTurnsRedOverAGapAndGreenOverARobot()
        {
            var root = Boot(Plain(), ControlMode.HoldRelease);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);

            Press(_kb.leftShiftKey);
            yield return Frames(3);
            Assert.IsTrue(root.Board.AimArrowShown, "the aim arrow is not drawn while holding");

            yield return Until(() => root.Sim.SlotForAim(root.Sim.AimDeg) >= 0 ||
                                     root.Sim.Phase != Phase.PlayerHolding, 3f, "the arrow over a robot");
            yield return null;
            var onRobot = root.Board.AimArrowColour;

            yield return Until(() => root.Sim.SlotForAim(root.Sim.AimDeg) < 0 ||
                                     root.Sim.Phase != Phase.PlayerHolding, 3f, "the arrow over a gap");
            yield return null;
            var onGap = root.Board.AimArrowColour;
            Release(_kb.leftShiftKey);

            Assert.Greater(onRobot.g, onRobot.r, "over a robot the arrow should read green");
            Assert.Greater(onGap.r, onGap.g, "over a gap the arrow should read red");
        }

        [UnityTest]
        public IEnumerator ModeA_HasFiveRobotsInAnArcAndNoKeyLabels()
        {
            var root = Boot(Plain(), ControlMode.HoldRelease);
            yield return Frames(4);
            Assert.AreEqual(5, root.Board.Receivers.Length, "Mode A needs five robots");
            Assert.AreEqual("", root.Board.KeyLabelText(0),
                "the L-SHIFT / R-SHIFT labels belong to Mode B only");

            // They must form an arc: the middle robot sits highest.
            float midY = root.Board.Receivers[2].Home.y;
            Assert.Greater(midY, root.Board.Receivers[0].Home.y, "the robots are not in an arc");
            Assert.Greater(midY, root.Board.Receivers[4].Home.y, "the robots are not in an arc");
            Assert.Less(root.Board.Receivers[0].Home.x, root.Board.Receivers[4].Home.x,
                "the robots are not ordered left to right");
        }

        // =====================================================================
        //  Zones
        // =====================================================================

        [UnityTest]
        public IEnumerator Zones_SwitchTheBackdropAndTheHudName()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(2);
            Assert.AreEqual(0, root.Zones.Current, "zone 1 should be live at the start");

            // Throw cleanly until the zone changes.
            float t = 0f;
            while (root.Sim.Zone == 0 && t < 90f && root.Sim.Phase != Phase.GameOver)
            {
                if (root.Sim.Phase == Phase.PlayerHolding && root.Sim.FuseFraction < 0.6f)
                    yield return Tap(_kb.leftShiftKey);
                t += Time.deltaTime;
                yield return null;
            }
            Assert.AreNotEqual(Phase.GameOver, root.Sim.Phase, "died before the first zone change");
            yield return Frames(4);

            Assert.AreEqual(1, root.Zones.Current, "the backdrop did not switch to zone 2");
            Assert.AreEqual("ROOFTOP", root.Hud.ZoneText, "the HUD zone name did not change");
        }
    }
}
