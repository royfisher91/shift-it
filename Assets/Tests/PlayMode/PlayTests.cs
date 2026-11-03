using System.Collections;
using System.Collections.Generic;
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
    /// Automated playtests. These drive the REAL game through the REAL Input System keyboard -
    /// Press(keyboard.leftShiftKey) - so they exercise exactly the path a player's key press takes,
    /// including the left/right Shift distinction the brief asks to be proven.
    ///
    /// "A bot can only ask for what a player can ask for": nothing here calls into the simulation to
    /// apply a throw. Every action is a key press.
    /// </summary>
    public class PlayTests : InputTestFixture
    {
        Keyboard _kb;
        GameRoot _root;
        float _bootedAt;

        public override void Setup()
        {
            base.Setup();
            _kb = InputSystem.AddDevice<Keyboard>();
            GameRoot.TestMode = true;
            GameRoot.ClearSavedProgress();
            Capture.Session = "playtests";
        }

        public override void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root.gameObject);
            _root = null;
            GameRoot.ConfigOverride = null;
            GameRoot.ForceStickyTipPlatform = null;
            base.TearDown();
        }

        // =====================================================================
        //  helpers
        // =====================================================================

        /// <summary>A config where nothing random can happen, so a test asserts one thing.</summary>
        static SimConfig Plain()
        {
            var c = new SimConfig();
            c.chanceDud = 0f; c.chanceHot = 0f; c.chanceMirrored = 0f;
            c.chanceBothBlocked = 0f; c.chanceSwap = 0f;
            c.blockerChanceAtUnlock = 0f; c.blockerChanceAtEnd = 0f;
            return c;
        }

        GameRoot Boot(SimConfig cfg, ControlMode mode, int seed = 4321)
        {
            GameRoot.ConfigOverride = cfg;
            var go = new GameObject("TestGameRoot");
            go.SetActive(false);                       // so fields can be set before Awake
            var root = go.AddComponent<GameRoot>();
            root.Seed = seed;
            root.StartMuted = true;
            _bootedAt = Time.realtimeSinceStartup;
            go.SetActive(true);                        // Awake runs here
            root.SetMode(mode);
            _root = root;
            return root;
        }

        IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>Tap a Shift key the way a player does: down one frame, up the next.</summary>
        IEnumerator Tap(ButtonControl key)
        {
            Press(key);
            yield return null;
            Release(key);
            yield return null;
        }

        /// <summary>Let real frames run until a predicate holds, or fail loudly on timeout.</summary>
        IEnumerator Until(System.Func<bool> cond, float timeout, string what)
        {
            float t = 0f;
            while (!cond() && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(cond(), "timed out after " + timeout + "s waiting for: " + what);
        }

        // =====================================================================
        //  Boot and title
        // =====================================================================

        [UnityTest]
        public IEnumerator Boot_ShowsTitleAndBuildsItsOwnScene()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);

            Assert.AreEqual(Phase.Title, root.Sim.Phase, "the game must open on the title screen");
            Assert.IsNotNull(root.Cam, "no camera was built");
            Assert.IsNotNull(root.Board, "no board was built");
            Assert.IsNotNull(root.Hud, "no HUD was built");
            Assert.Greater(Shapes.CachedCount, 5, "the generated sprite set is empty");
            Assert.IsNotNull(root.Board.Player, "the player robot was not built");
            Assert.AreEqual(2, root.Board.Receivers.Length, "Mode B needs exactly two receivers");

            Assert.IsNotNull(Capture.Shot(root.Cam, "01_title_modeB"), "title screenshot failed");
        }

        [UnityTest]
        public IEnumerator Title_ShiftStartsTheGame_AndFirstCatchIsImmediate()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);

            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase, "Shift must start the game");
            Assert.Greater(root.Sim.Fuse, 0f, "the first bomb must be live");
            // Feel gate item 8: time to core play, measured from THIS game being constructed.
            // The first version used Time.timeSinceLevelLoad, which in a PlayMode suite has been
            // counting since the run began - it read 250 s and measured nothing about the game.
            float toPlay = Time.realtimeSinceStartup - _bootedAt;
            Debug.Log("[SHIFTIT] time from boot to the first catch: " + toPlay.ToString("0.00") + "s");
            Assert.Less(toPlay, 30f, "the player must reach core play within 30 s");
            Assert.IsNotNull(Capture.Shot(root.Cam, "02_first_catch_modeB"));
        }

        [UnityTest]
        public IEnumerator Title_TabNoLongerOffersModeA_ButTheCodeStillWorks()
        {
            // Roy picked Mode B at the Stage 1 gate, so the player's route into Mode A is closed.
            // The flag shuts the DOOR, not the room: SetMode still works, which is why every Mode A
            // test in this suite keeps running and the code cannot quietly rot.
            Assert.IsFalse(GameRoot.ModeAEnabled, "Mode A must not ship");

            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            Assert.AreEqual(ControlMode.LeftRight, root.Sim.Mode);

            yield return Tap(_kb.tabKey);
            yield return Frames(2);
            Assert.AreEqual(ControlMode.LeftRight, root.Sim.Mode,
                "Tab must no longer switch mode in the shipping build");
            Assert.AreEqual(2, root.Board.Receivers.Length, "still the two-robot board");

            // ...and the mode itself is still reachable programmatically, so it stays tested.
            root.SetMode(ControlMode.HoldRelease);
            yield return Frames(2);
            Assert.AreEqual(5, root.Board.Receivers.Length,
                "SetMode must still build Mode A, or its tests are testing nothing");
        }

        // =====================================================================
        //  Settings
        // =====================================================================

        [UnityTest]
        public IEnumerator Settings_OpensFromTheTitleAndShowsTheLiveValues()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            Assert.IsFalse(root.Settings.Shown, "settings should start closed");

            root.Hud.SettingsButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(root.Settings.Shown, "the SETTINGS button must open it");
            Assert.AreEqual(root.MusicLevel.ToString(), root.Settings.MusicValueText);
            Assert.AreEqual(root.SfxLevel.ToString(), root.Settings.SfxValueText);
            Assert.AreEqual(root.FullscreenWanted ? "ON" : "OFF", root.Settings.FullscreenValueText);

            root.Settings.CloseButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsFalse(root.Settings.Shown, "BACK must close it");
        }

        [UnityTest]
        public IEnumerator Settings_VolumesClampAndReachTheAudioService()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            root.Hud.SettingsButton.onClick.Invoke();
            yield return Frames(2);

            // All the way down, past the end.
            for (int i = 0; i < SettingsView.Steps + 4; i++) root.Settings.MusicDown.onClick.Invoke();
            yield return Frames(2);
            Assert.AreEqual(0, root.MusicLevel, "music must clamp at 0");
            Assert.AreEqual("0", root.Settings.MusicValueText);
            Assert.AreEqual(0f, root.Audio.MusicVolume, 0.001f, "0 must actually silence the music");

            // And all the way up, past the end.
            for (int i = 0; i < SettingsView.Steps + 4; i++) root.Settings.MusicUp.onClick.Invoke();
            yield return Frames(2);
            Assert.AreEqual(SettingsView.Steps, root.MusicLevel, "music must clamp at the top");
            Assert.Greater(root.Audio.MusicVolume, 0f);

            for (int i = 0; i < SettingsView.Steps + 4; i++) root.Settings.SfxDown.onClick.Invoke();
            yield return Frames(2);
            Assert.AreEqual(0, root.SfxLevel, "sound must clamp at 0");
            Assert.AreEqual(0f, root.Audio.SfxVolume, 0.001f);
        }

        [UnityTest]
        public IEnumerator Settings_RememberSettingsAcrossABoot()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            root.Hud.SettingsButton.onClick.Invoke();
            for (int i = 0; i < 3; i++) root.Settings.MusicDown.onClick.Invoke();
            root.Settings.FullscreenToggle.onClick.Invoke();
            int music = root.MusicLevel;
            bool full = root.FullscreenWanted;
            root.Settings.CloseButton.onClick.Invoke();
            yield return Frames(2);

            Object.DestroyImmediate(root.gameObject);
            _root = null;
            var again = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            Assert.AreEqual(music, again.MusicLevel, "the music level must survive a restart");
            Assert.AreEqual(full, again.FullscreenWanted, "the fullscreen choice must survive too");
        }

        [UnityTest]
        public IEnumerator Settings_SwallowsShiftSoNoRunStartsBehindIt()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            root.Hud.SettingsButton.onClick.Invoke();
            yield return Frames(2);

            yield return Tap(_kb.leftShiftKey);
            yield return Frames(2);
            Assert.AreEqual(Phase.Title, root.Sim.Phase,
                "Shift must not start a run while the settings panel is open");

            // Escape closes it, and then Shift works again.
            yield return Tap(_kb.escapeKey);
            yield return Frames(2);
            Assert.IsFalse(root.Settings.Shown);
            yield return Tap(_kb.leftShiftKey);
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase);
        }

        // =====================================================================
        //  The Sticky Keys tip for browser players
        // =====================================================================

        [UnityTest]
        public IEnumerator StickyKeysTip_ShowsForBrowserPlayers_AndStaysDismissed()
        {
            GameRoot.ForceStickyTipPlatform = true;          // the editor can never be a browser
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);

            Assert.IsTrue(root.Hud.StickyTipShown, "a first-time browser player should see the tip");
            string tip = root.Hud.StickyTipText;
            StringAssert.Contains("beep", tip.ToLowerInvariant(), "the tip must name the beep");
            StringAssert.Contains("Sticky Keys", tip, "...and say what is beeping");
            StringAssert.Contains("Accessibility", tip, "...and how to turn it off");
            StringAssert.Contains("Keyboard", tip);
            Assert.Less(tip.Length, 220, "the tip is meant to be one friendly line, not a paragraph");

            // GOT IT, through the real button wiring.
            root.Hud.TipDismissButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsFalse(root.Hud.StickyTipShown, "GOT IT must put the tip away");

            // And it must stay away for this player, across a fresh boot.
            Object.DestroyImmediate(root.gameObject);
            _root = null;
            var again = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            Assert.IsFalse(again.Hud.StickyTipShown, "the dismissal must be remembered");
        }

        [UnityTest]
        public IEnumerator StickyKeysTip_IsNotShownOnDesktop()
        {
            // The desktop build suppresses the shortcut itself and restores it on quit, so showing
            // a browser-only tip there would just be noise.
            GameRoot.ForceStickyTipPlatform = false;
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            Assert.IsFalse(root.Hud.StickyTipShown, "the tip is for browser players only");
        }

        [UnityTest]
        public IEnumerator StickyKeysTip_DoesNotBlockPlay()
        {
            // "Don't block play": Shift must start the game whether or not the tip has been read.
            GameRoot.ForceStickyTipPlatform = true;
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(3);
            Assert.IsTrue(root.Hud.StickyTipShown);

            yield return Tap(_kb.leftShiftKey);
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase,
                "Shift must start the game with the tip still on screen");

            // It lives on the title panel, so it goes away with it rather than sitting over play.
            yield return Frames(2);
            Assert.IsFalse(root.Hud.TitleShown, "the title screen should be gone");
        }

        [UnityTest]
        public IEnumerator Escape_OpensAConfirmThatPausesTheGame_AndShiftCarriesOn()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);                  // start playing
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase);

            yield return Tap(_kb.escapeKey);
            yield return Frames(2);
            Assert.IsTrue(root.QuitConfirmOpen, "Escape must open the quit confirm");
            Assert.IsTrue(root.Hud.QuitConfirmShown, "...and it must be on screen");

            // The fuse must STOP. A confirm box that lets the bomb keep burning is a trap.
            float fuseAtOpen = root.Sim.Fuse;
            yield return Frames(45);
            Assert.AreEqual(fuseAtOpen, root.Sim.Fuse, 0.0001f,
                "the fuse kept burning while the quit confirm was open");

            // A fresh Shift carries on.
            float t = 0f;
            while (t < 0.4f) { t += Time.deltaTime; yield return null; }
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(2);
            Assert.IsFalse(root.QuitConfirmOpen, "Shift must dismiss the confirm");
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase, "and play must resume");

            yield return Frames(20);
            Assert.Less(root.Sim.Fuse, fuseAtOpen, "the fuse must start burning again");
        }

        [UnityTest]
        public IEnumerator Escape_DoesNotThrowTheBombOrCountAsAPass()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);
            int passes = root.Sim.Passes;

            yield return Tap(_kb.escapeKey);
            yield return Frames(3);
            Assert.AreEqual(passes, root.Sim.Passes, "Escape must not throw the bomb");
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase);
        }

        // =====================================================================
        //  THE KEY REQUIREMENT: the two Shift keys are told apart
        // =====================================================================

        [UnityTest]
        public IEnumerator LeftAndRightShift_AreDetectedSeparately()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);

            Press(_kb.leftShiftKey);
            yield return null;
            Assert.IsTrue(root.Input.LeftDown, "left Shift was not seen");
            Assert.IsFalse(root.Input.RightDown, "right Shift reported down while only left was pressed");
            Release(_kb.leftShiftKey);
            yield return null;

            Press(_kb.rightShiftKey);
            yield return null;
            Assert.IsTrue(root.Input.RightDown, "right Shift was not seen");
            Assert.IsFalse(root.Input.LeftDown, "left Shift reported down while only right was pressed");
            Release(_kb.rightShiftKey);
            yield return null;

            Assert.IsTrue(root.Input.EverSawBothDistinctly,
                "the two Shift keys were never observed independently");
            Debug.Log("[SHIFTIT] input diagnostic: " + root.Input.DiagnosticLine());
        }

        [UnityTest]
        public IEnumerator ModeB_LeftShiftThrowsLeft_RightShiftThrowsRight()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);                  // start
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase);

            yield return Tap(_kb.leftShiftKey);                  // throw left
            Assert.AreEqual(0, root.Sim.ActiveSlot, "LEFT Shift must throw to the LEFT robot");

            yield return Until(() => root.Sim.Phase == Phase.PlayerHolding, 4f, "the bomb to come back");
            yield return Tap(_kb.rightShiftKey);                 // throw right
            Assert.AreEqual(1, root.Sim.ActiveSlot, "RIGHT Shift must throw to the RIGHT robot");
            Assert.AreEqual(2, root.Sim.Passes);
        }

        // =====================================================================
        //  Simulated timings Roy asked for: early / perfect / late / too long / wrong side
        // =====================================================================

        [UnityTest]
        public IEnumerator EarlyThrow_ScoresOneAndBreaksNoCombo()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);
            yield return Tap(_kb.leftShiftKey);                  // immediately: well before the band
            Assert.AreEqual(1, root.Sim.Score, "an early throw is worth 1");
            Assert.AreEqual(0, root.Sim.Combo);
        }

        [UnityTest]
        public IEnumerator PerfectThrow_InsideTheGreenBand_ScoresCombo()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);

            var sim = root.Sim;
            float mid = (sim.BandLow + sim.BandHigh) * 0.5f;
            yield return Until(() => sim.FuseFraction <= sim.BandHigh && sim.FuseFraction >= sim.BandLow,
                               5f, "the fuse to enter the green band");
            Assert.IsNotNull(Capture.Shot(root.Cam, "04_fuse_in_green_band"));

            yield return Tap(_kb.leftShiftKey);
            Assert.AreEqual(1, sim.Combo, "a throw inside the band is a PERFECT");
            Assert.AreEqual(2, sim.Score, "first PERFECT scores 1 + combo = 2");
            yield return Frames(2);
            Assert.IsNotNull(Capture.Shot(root.Cam, "05_perfect_throw"));
        }

        [UnityTest]
        public IEnumerator HoldingTooLong_RunsTheFuseOutAndEndsTheGame()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);

            yield return Until(() => root.Sim.Phase == Phase.Failing, 6f, "the fuse to run out");
            Assert.AreEqual(FailReason.FuseRanOut, root.Sim.Fail);
            yield return Frames(4);
            Assert.IsNotNull(Capture.Shot(root.Cam, "06_explosion"));

            yield return Until(() => root.Sim.Phase == Phase.GameOver, 4f, "the game-over card");
            yield return Frames(2);
            Assert.IsNotNull(Capture.Shot(root.Cam, "07_game_over_fuse_ran_out"));
        }

        [UnityTest]
        public IEnumerator WrongSideThrow_AtAShield_FailsWithTheShieldReason()
        {
            // Force a shield on one side from pass 0, and nothing else.
            var c = Plain();
            c.blockerChanceAtUnlock = 1f; c.blockerChanceAtEnd = 1f;
            c.blockerRampStartPass = 0;        // or the ramp is still zero at pass 0
            c.unlockShield = 0; c.unlockAsleep = int.MaxValue; c.unlockOnBreak = int.MaxValue;
            var root = Boot(c, ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);
            yield return Frames(2);

            var sim = root.Sim;
            int blocked = sim.Turn.Slots[0] != Blocker.None ? 0 : 1;
            Assert.AreEqual(Blocker.Shield, sim.Turn.Slots[blocked], "the shield did not appear");
            Assert.IsNotNull(Capture.Shot(root.Cam, "08_shield_up"));

            yield return Tap(blocked == 0 ? _kb.leftShiftKey : _kb.rightShiftKey);
            Assert.AreEqual(FailReason.ThrewAtShield, sim.Fail, "throwing at a shield must fail");
            yield return Until(() => sim.Phase == Phase.GameOver, 4f, "the game-over card");
            yield return Frames(2);
            Assert.IsNotNull(Capture.Shot(root.Cam, "09_game_over_shield"));
        }

        [UnityTest]
        public IEnumerator ModeA_HoldSweepsTheArrowAndReleaseThrows()
        {
            var root = Boot(Plain(), ControlMode.HoldRelease);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);                  // start the game
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase);

            Press(_kb.leftShiftKey);                             // begin aiming
            yield return Frames(3);
            Assert.IsTrue(root.Sim.Holding, "pressing Shift must start the hold");
            float a0 = root.Sim.AimDeg;
            yield return Frames(6);
            Assert.AreNotEqual(a0, root.Sim.AimDeg, "the aim arrow must sweep while held");
            Assert.IsNotNull(Capture.Shot(root.Cam, "10_modeA_aiming"));

            // Release while the arrow is WELL INSIDE a robot, not merely touching its edge.
            //
            // Slots are 24 degrees wide with only 4 degree gaps, and the arrow sweeps about 2.3
            // degrees per frame - more on a heavy frame. Waiting for "over a robot" could observe
            // the arrow on an outer edge and then have it drift into the gap during the frame
            // before the release was read, which made this test fail on frame luck rather than on
            // the mechanic. Requiring the arrow near a slot CENTRE leaves room for that one frame
            // of drift. The drift itself is honest: a real player's release is also judged on the
            // following tick.
            yield return Until(() =>
            {
                int s = root.Sim.SlotForAim(root.Sim.AimDeg);
                return s >= 0 && Mathf.Abs(root.Sim.AimDeg - root.Sim.SlotCentreDeg(s)) <= 6f;
            }, 3f, "the arrow to reach the middle of a robot");
            Release(_kb.leftShiftKey);
            yield return null;
            Assert.AreNotEqual(FailReason.ReleasedBetween, root.Sim.Fail,
                "released on a robot but it counted as thin air");
            Assert.AreEqual(1, root.Sim.Passes, "releasing on a robot is a pass");
        }

        [UnityTest]
        public IEnumerator ModeA_ReleasingInAGapHitsTheFloor()
        {
            var root = Boot(Plain(), ControlMode.HoldRelease);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);

            Press(_kb.leftShiftKey);
            // Sweep until the arrow is actually between two robots, then let go there.
            yield return Until(() => root.Sim.SlotForAim(root.Sim.AimDeg) < 0 ||
                                     root.Sim.Phase != Phase.PlayerHolding, 3f, "a gap");
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase, "the fuse ran out before a gap");
            Assert.AreEqual(-1, root.Sim.SlotForAim(root.Sim.AimDeg), "the arrow should be in a gap");
            Release(_kb.leftShiftKey);
            yield return null;
            Assert.AreEqual(FailReason.ReleasedBetween, root.Sim.Fail);
        }

        [UnityTest]
        public IEnumerator ModeA_NoHoldEverApproachesWindowsEightSecondLimit()
        {
            // The hard accessibility requirement. Hold and simply never let go.
            var root = Boot(Plain(), ControlMode.HoldRelease);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);

            Press(_kb.leftShiftKey);
            float worst = 0f, t = 0f;
            while (root.Sim.Phase == Phase.PlayerHolding && t < 9f)
            {
                worst = Mathf.Max(worst, root.Sim.HoldTime);
                t += Time.deltaTime;
                yield return null;
            }
            Release(_kb.leftShiftKey);
            Debug.Log("[SHIFTIT] longest Mode A hold observed: " + worst.ToString("0.00") + "s");
            Assert.Less(worst, 8f, "a hold reached " + worst + "s; Windows opens Filter Keys at 8 s");
            Assert.LessOrEqual(worst, root.Config.maxHoldSeconds + 0.1f, "the 5 s cap was exceeded");
        }

        // =====================================================================
        //  Windows accessibility behaviour
        // =====================================================================

        [UnityTest]
        public IEnumerator AHeldShiftKey_ThrowsExactlyOnce_StickyKeysSafety()
        {
            // Sticky Keys can latch Shift down. A latched key must be one throw, not a stream.
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);                  // start

            Press(_kb.leftShiftKey);                             // and never release
            float t = 0f;
            while (t < 1.5f) { t += Time.deltaTime; yield return null; }
            Release(_kb.leftShiftKey);
            yield return null;

            Assert.AreEqual(1, root.Sim.Passes, "a held key must throw once, not repeatedly");
        }

        [UnityTest]
        public IEnumerator GameOver_IgnoresPanicPressesThenRetriesOnShift()
        {
            var root = Boot(Plain(), ControlMode.LeftRight);
            yield return Frames(2);
            yield return Tap(_kb.leftShiftKey);
            yield return Until(() => root.Sim.Phase == Phase.GameOver, 8f, "game over");

            yield return Tap(_kb.leftShiftKey);                  // inside the lockout
            Assert.AreEqual(Phase.GameOver, root.Sim.Phase,
                "a panic press must not skip the game-over screen");

            float t = 0f;
            while (t < root.Config.retryIgnoreSeconds + 0.15f) { t += Time.deltaTime; yield return null; }
            yield return Tap(_kb.leftShiftKey);
            Assert.AreEqual(Phase.PlayerHolding, root.Sim.Phase, "Shift must retry after the lockout");
            Assert.AreEqual(0, root.Sim.Score, "a retry starts from zero");
        }

        // =====================================================================
        //  Feel gate
        // =====================================================================

        [UnityTest]
        public IEnumerator NoFrameExceeds33Milliseconds()
        {
            // Feel gate item 1, measured per frame rather than as an average, because an average
            // cannot see a hitch. The first few frames are skipped: they include sprite generation
            // and scene construction, which happen once at boot and are reported separately.
            var root = Boot(new SimConfig(), ControlMode.LeftRight);
            yield return Frames(10);

            var times = new List<float>(600);
            float t = 0f;
            bool pressed = false;
            while (t < 6f)
            {
                if (!pressed && root.Sim.Phase == Phase.Title) { pressed = true; yield return Tap(_kb.leftShiftKey); }
                // Keep it alive by throwing whenever it is holding.
                if (root.Sim.Phase == Phase.PlayerHolding && root.Sim.FuseFraction < 0.5f)
                {
                    int open = root.Sim.Turn.FirstOpenSlot();
                    if (open >= 0 && root.Sim.Bomb != BombKind.Dud)
                    {
                        bool mir = root.Sim.Turn.Mirrored;
                        int logical = mir ? 1 - open : open;
                        yield return Tap(logical == 0 ? _kb.leftShiftKey : _kb.rightShiftKey);
                    }
                }
                if (root.Sim.Phase == Phase.GameOver) yield return Tap(_kb.leftShiftKey);
                times.Add(Time.unscaledDeltaTime * 1000f);
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            float worst = 0f; int over = 0;
            foreach (var ms in times) { if (ms > worst) worst = ms; if (ms > 33f) over++; }
            times.Sort();
            float median = times[times.Count / 2];
            Debug.Log(string.Format("[SHIFTIT] frame times over {0} frames: median {1:0.00}ms worst {2:0.00}ms over-33ms {3}",
                times.Count, median, worst, over));
            Assert.AreEqual(0, over, "frames over 33 ms: " + over + " (worst " + worst.ToString("0.0") + "ms)");
        }

        [UnityTest]
        public IEnumerator AFullRunNeverGetsStuck()
        {
            // Every state that takes input away must hand it back. Play a whole run through the
            // real key path and assert it always reaches game over.
            var root = Boot(new SimConfig(), ControlMode.LeftRight);
            yield return Frames(3);
            yield return Tap(_kb.leftShiftKey);

            float t = 0f, lastPassAt = 0f, worstGap = 0f;
            int lastPasses = 0;
            var sim = root.Sim;
            while (sim.Phase != Phase.GameOver && t < 120f)
            {
                if (sim.Passes != lastPasses)
                {
                    lastPasses = sim.Passes;
                    if (t - lastPassAt > worstGap) worstGap = t - lastPassAt;
                    lastPassAt = t;
                }
                if (sim.Phase == Phase.PlayerHolding && sim.FuseFraction < 0.45f && sim.Bomb != BombKind.Dud)
                {
                    int open = sim.Turn.FirstOpenSlot();
                    if (open >= 0)
                    {
                        int logical = sim.Turn.Mirrored ? 1 - open : open;
                        yield return Tap(logical == 0 ? _kb.leftShiftKey : _kb.rightShiftKey);
                    }
                }
                t += Time.deltaTime;
                yield return null;
            }
            // Three outcomes, counted apart: it exploded, it was still scoring when the window
            // closed, or it wedged. Only the third is a defect. The first version of this test
            // asserted "it must reach game over" and failed on a run that reached pass 89 while
            // still playing - which is the game working.
            bool ended = sim.Phase == Phase.GameOver;
            float sinceLastPass = t - lastPassAt;
            Debug.Log("[SHIFTIT] live run: pass " + sim.Passes + " score " + sim.Score +
                      " in " + t.ToString("0.0") + "s, " +
                      (ended ? "died of " + sim.Fail : "still playing at the window's end") +
                      ", longest gap between passes " + worstGap.ToString("0.00") + "s");
            Assert.Less(worstGap, 10f,
                "the game stopped making progress for " + worstGap.ToString("0.0") + "s");
            Assert.IsTrue(ended || sinceLastPass < 10f,
                "the run neither ended nor was still scoring - passes=" + sim.Passes +
                " phase=" + sim.Phase);
        }
    }
}
