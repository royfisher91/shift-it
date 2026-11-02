using UnityEngine;
using KT.ShiftIt.Sim;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KT.ShiftIt.Game
{
    /// <summary>Which code path is reading the keyboard. Switchable at runtime so the WebGL
    /// browser check can compare them side by side rather than guessing.</summary>
    public enum InputBackend
    {
        /// <summary>com.unity.inputsystem: Keyboard.current.leftShiftKey / rightShiftKey.</summary>
        InputSystem = 0,
        /// <summary>UnityEngine.Input: KeyCode.LeftShift / KeyCode.RightShift.</summary>
        Legacy = 1,
    }

    /// <summary>
    /// The only thing in the game that touches a keyboard, a mouse or a screen.
    ///
    /// ONE KEY TYPE ONLY: the two Shift keys run the whole game. Nothing here reads arrows, WASD
    /// or Space, by design - there is no binding for them to be bound to.
    ///
    /// Roy's brief asks specifically that left and right Shift be told apart via the Input System
    /// and that it be confirmed in WebGL on Chrome and Edge. Both backends are built and either can
    /// be selected at runtime, so the browser check produces evidence instead of an assumption.
    /// Project is set to activeInputHandler = Both so both compile.
    /// </summary>
    public sealed class ShiftInput
    {
        public InputBackend Backend = InputBackend.InputSystem;

        /// <summary>True while that Shift key is physically down, by whichever backend is live.</summary>
        public bool LeftDown { get; private set; }
        public bool RightDown { get; private set; }

        /// <summary>Set for one frame when the mode toggle was asked for (Tab, or the title button).</summary>
        public bool ToggleModePressed { get; private set; }

        /// <summary>Diagnostics for the WebGL report: have we ever seen each key, on each backend?</summary>
        public bool EverSawLeft { get; private set; }
        public bool EverSawRight { get; private set; }
        public bool EverSawBothDistinctly { get; private set; }

        // Touch: a tap on the left or right half of the screen is the left or right Shift key.
        bool _touchLeft, _touchRight;

        /// <summary>
        /// Read the devices. Call once per frame, before the simulation ticks.
        /// </summary>
        public void Poll()
        {
            ToggleModePressed = false;
            bool kbLeft = false, kbRight = false, toggle = false;

            switch (Backend)
            {
                case InputBackend.InputSystem:
#if ENABLE_INPUT_SYSTEM
                    var kb = Keyboard.current;
                    if (kb != null)
                    {
                        kbLeft = kb.leftShiftKey.isPressed;
                        kbRight = kb.rightShiftKey.isPressed;
                        toggle = kb.tabKey.wasPressedThisFrame;
                    }
#endif
                    break;

                case InputBackend.Legacy:
#if ENABLE_LEGACY_INPUT_MANAGER
                    kbLeft = Input.GetKey(KeyCode.LeftShift);
                    kbRight = Input.GetKey(KeyCode.RightShift);
                    toggle = Input.GetKeyDown(KeyCode.Tab);
#endif
                    break;
            }

            ReadTouch();

            LeftDown = kbLeft || _touchLeft;
            RightDown = kbRight || _touchRight;
            ToggleModePressed = toggle;

            if (LeftDown) EverSawLeft = true;
            if (RightDown) EverSawRight = true;
            // Proof the two keys are genuinely distinguishable: one down while the other is not.
            if (kbLeft != kbRight) EverSawBothDistinctly = true;
        }

        void ReadTouch()
        {
            _touchLeft = _touchRight = false;

#if ENABLE_INPUT_SYSTEM
            if (Backend == InputBackend.InputSystem)
            {
                var ts = UnityEngine.InputSystem.Touchscreen.current;
                if (ts != null)
                {
                    foreach (var t in ts.touches)
                    {
                        if (!t.press.isPressed) continue;
                        float x = t.position.ReadValue().x;
                        if (x < Screen.width * 0.5f) _touchLeft = true; else _touchRight = true;
                    }
                }
                return;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == UnityEngine.TouchPhase.Canceled || t.phase == UnityEngine.TouchPhase.Ended) continue;
                if (t.position.x < Screen.width * 0.5f) _touchLeft = true; else _touchRight = true;
            }
#endif
        }

        /// <summary>
        /// Push this frame's key state into the simulation. The simulation does its own edge
        /// detection, so a key latched down by Windows Sticky Keys is one throw, not a stream.
        /// </summary>
        public void Apply(ShiftSim sim)
        {
            sim.SetKey(Side.Left, LeftDown);
            sim.SetKey(Side.Right, RightDown);
        }

        /// <summary>Drop every key, e.g. when the window loses focus mid-hold.</summary>
        public void Clear(ShiftSim sim)
        {
            LeftDown = RightDown = false;
            _touchLeft = _touchRight = false;
            sim.ReleaseAll();
        }

        public string DiagnosticLine()
        {
            return string.Format("backend={0} left={1} right={2} sawLeft={3} sawRight={4} distinct={5}",
                Backend, LeftDown, RightDown, EverSawLeft, EverSawRight, EverSawBothDistinctly);
        }

        /// <summary>Is the selected backend actually able to see a keyboard right now?</summary>
        public bool BackendHasKeyboard()
        {
#if ENABLE_INPUT_SYSTEM
            if (Backend == InputBackend.InputSystem) return Keyboard.current != null;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Backend == InputBackend.Legacy) return true;
#endif
            return false;
        }
    }
}
