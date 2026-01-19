using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace TouchEffectSystem
{
    /// <summary>
    /// Unified input controller that provides consistent interface for both Legacy Input Manager
    /// and New Input System. Caches input states each frame for reliable access across multiple components.
    /// Supports both single-touch/mouse and multi-touch input patterns.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        #region Enums

        /// <summary>
        /// Defines which Unity input system to use for input detection.
        /// </summary>
        public enum InputSystemType
        {
            /// <summary>Legacy Input Manager (Input.GetMouseButton, Input.touches).</summary>
            Old,
            /// <summary>New Input System (UnityEngine.InputSystem package).</summary>
            New
        }

        #endregion

        #region Serialized Fields

        /// <summary>
        /// Currently selected input system type. Determines which input processing method to use.
        /// Change this to switch between Legacy Input Manager and New Input System at runtime.
        /// </summary>
        public InputSystemType inputSystemType;

        #endregion

        #region Private Fields - Cached Input States

        /// <summary>
        /// Indicates if input just started this frame (button/touch down event).
        /// Cached in Update() for frame-consistent access by all dependent components.
        /// </summary>
        private bool cachedInputDown = false;

        /// <summary>
        /// Indicates if input just ended this frame (button/touch up event).
        /// Cached in Update() for frame-consistent access by all dependent components.
        /// </summary>
        private bool cachedInputUp = false;

        /// <summary>
        /// Indicates if input is currently active (button held or touch ongoing).
        /// Cached in Update() for frame-consistent access by all dependent components.
        /// </summary>
        private bool cachedInputMoving = false;

        /// <summary>
        /// Current primary input position in screen coordinates (pixels).
        /// For touch: position of first touch point. For mouse: cursor position.
        /// Cached in Update() for frame-consistent access by all dependent components.
        /// </summary>
        private Vector2 cachedInputPosition = Vector2.zero;

        /// <summary>
        /// Array of all currently active touch points for multi-touch support.
        /// Contains unified TouchData for both Legacy and New Input Systems.
        /// Empty array when no touches are active or when using mouse-only input.
        /// </summary>
        private TouchData[] cachedTouches = new TouchData[0];

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Updates cached input values every frame based on the selected input system type.
        /// This ensures all components reading input data get consistent values throughout the frame.
        /// Called once per frame by Unity.
        /// </summary>
        void Update()
        {
            if (inputSystemType == InputSystemType.Old)
            {
                ProcessOldInputSystem();
            }
#if ENABLE_INPUT_SYSTEM
            else
            {
                ProcessNewInputSystem();
            }
#endif
        }

        #endregion

        #region Legacy Input System Processing

        /// <summary>
        /// Processes input using Unity's Legacy Input Manager (Input class).
        /// Detects mouse clicks and touch events, caching all input states for the current frame.
        /// Prioritizes touch input over mouse when both are available.
        /// </summary>
        private void ProcessOldInputSystem()
        {
            // Detect input down event (first frame of button press or touch)
            cachedInputDown = Input.GetMouseButtonDown(0) ||
                             (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began);

            // Detect input up event (button release or touch end/cancel)
            cachedInputUp = Input.GetMouseButtonUp(0) ||
                           (Input.touchCount > 0 && (Input.GetTouch(0).phase == UnityEngine.TouchPhase.Ended ||
                                                    Input.GetTouch(0).phase == UnityEngine.TouchPhase.Canceled));

            // Detect ongoing input (button held or touch active)
            cachedInputMoving = Input.GetMouseButton(0) ||
                               (Input.touchCount > 0 && (Input.GetTouch(0).phase == UnityEngine.TouchPhase.Moved ||
                                                        Input.GetTouch(0).phase == UnityEngine.TouchPhase.Stationary));

            // Cache primary input position (touch has priority over mouse)
            if (Input.touchCount > 0)
                cachedInputPosition = Input.GetTouch(0).position;
            else
                cachedInputPosition = Input.mousePosition;

            // Cache all active touch points for multi-touch support
            cachedTouches = new TouchData[Input.touchCount];
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                cachedTouches[i] = new TouchData
                {
                    id = touch.fingerId,
                    position = touch.position,
                    phase = ConvertTouchPhase(touch.phase)
                };
            }
        }

        #endregion

#if ENABLE_INPUT_SYSTEM
        #region New Input System Processing

        /// <summary>
        /// Processes input using Unity's New Input System (InputSystem package).
        /// Detects both mouse and touch events, caching all input states for the current frame.
        /// Prioritizes touch input over mouse when both are available.
        /// Only compiled when ENABLE_INPUT_SYSTEM is defined.
        /// </summary>
        private void ProcessNewInputSystem()
        {
            // Detect input down event from both mouse and touch
            bool mouseDown = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            bool touchDown = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            cachedInputDown = mouseDown || touchDown;

            // Detect input up event from both mouse and touch
            bool mouseUp = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
            bool touchUp = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
            cachedInputUp = mouseUp || touchUp;

            // Detect ongoing input from both mouse and touch
            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
            bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed;
            cachedInputMoving = mousePressed || touchPressed;

            // Cache primary input position with priority for touch over mouse
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                cachedInputPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            else if (Mouse.current != null)
                cachedInputPosition = Mouse.current.position.ReadValue();
            else
                cachedInputPosition = Vector2.zero;

            // Cache all active multi-touch points
            CacheNewInputSystemTouches();
        }

        /// <summary>
        /// Caches all active touch points from the New Input System's touchscreen.
        /// Iterates through up to 10 possible simultaneous touches and stores active ones.
        /// Determines touch phase for each active touch based on press state and movement delta.
        /// </summary>
        private void CacheNewInputSystemTouches()
        {
            if (Touchscreen.current != null)
            {
                var activeTouches = new System.Collections.Generic.List<TouchData>();

                // Process all possible touch points (New Input System supports up to 10)
                for (int i = 0; i < 10; i++)
                {
                    TouchControl touch = GetTouchByIndex(i);

                    // Only process if touch exists and is currently pressed
                    if (touch != null && touch.press.isPressed)
                    {
                        TouchPhase phase = DetermineTouchPhase(touch);

                        activeTouches.Add(new TouchData
                        {
                            id = touch.touchId.ReadValue(),
                            position = touch.position.ReadValue(),
                            phase = phase
                        });
                    }
                }

                cachedTouches = activeTouches.ToArray();
            }
            else
            {
                // No touchscreen available, return empty array
                cachedTouches = new TouchData[0];
            }
        }

        /// <summary>
        /// Retrieves a TouchControl by index from the New Input System's touchscreen.
        /// Index 0 returns primaryTouch, indices 1-9 return additional touches from the touches collection.
        /// </summary>
        /// <param name="index">Touch index (0-9). Index 0 is the primary touch.</param>
        /// <returns>TouchControl at the specified index, or null if not available.</returns>
        private TouchControl GetTouchByIndex(int index)
        {
            if (Touchscreen.current == null) return null;

            switch (index)
            {
                case 0: return Touchscreen.current.primaryTouch;
                case 1: return Touchscreen.current.touches.Count > 1 ? Touchscreen.current.touches[1] : null;
                case 2: return Touchscreen.current.touches.Count > 2 ? Touchscreen.current.touches[2] : null;
                case 3: return Touchscreen.current.touches.Count > 3 ? Touchscreen.current.touches[3] : null;
                case 4: return Touchscreen.current.touches.Count > 4 ? Touchscreen.current.touches[4] : null;
                case 5: return Touchscreen.current.touches.Count > 5 ? Touchscreen.current.touches[5] : null;
                case 6: return Touchscreen.current.touches.Count > 6 ? Touchscreen.current.touches[6] : null;
                case 7: return Touchscreen.current.touches.Count > 7 ? Touchscreen.current.touches[7] : null;
                case 8: return Touchscreen.current.touches.Count > 8 ? Touchscreen.current.touches[8] : null;
                case 9: return Touchscreen.current.touches.Count > 9 ? Touchscreen.current.touches[9] : null;
                default: return null;
            }
        }

        /// <summary>
        /// Determines the current phase of a touch from New Input System's TouchControl.
        /// Analyzes press state and movement delta to categorize touch phase.
        /// </summary>
        /// <param name="touch">TouchControl to analyze.</param>
        /// <returns>Unified TouchPhase representing the current state of the touch.</returns>
        private TouchPhase DetermineTouchPhase(TouchControl touch)
        {
            if (touch.press.wasPressedThisFrame)
                return TouchPhase.Began;
            else if (touch.press.wasReleasedThisFrame)
                return TouchPhase.Ended;
            else if (touch.delta.ReadValue().magnitude > 0.1f)
                return TouchPhase.Moved;
            else
                return TouchPhase.Stationary;
        }

        #endregion
#endif

        #region Public API

        /// <summary>
        /// Returns true on the frame when input starts (mouse button down or touch begins).
        /// Useful for detecting click/tap initiation.
        /// </summary>
        /// <returns>True if input just started this frame, false otherwise.</returns>
        public bool GetInputDown() { return cachedInputDown; }

        /// <summary>
        /// Returns true on the frame when input ends (mouse button up or touch ends).
        /// Useful for detecting click/tap completion.
        /// </summary>
        /// <returns>True if input just ended this frame, false otherwise.</returns>
        public bool GetInputUp() { return cachedInputUp; }

        /// <summary>
        /// Legacy method name for backward compatibility with older code.
        /// Returns true while input is active (button held or touch ongoing).
        /// </summary>
        /// <returns>True if input is currently active, false otherwise.</returns>
        public bool GetInputL() { return cachedInputMoving; }

        /// <summary>
        /// Returns true while input is active (button held or touch ongoing).
        /// Useful for detecting drag operations or continuous input.
        /// </summary>
        /// <returns>True if input is currently active, false otherwise.</returns>
        public bool IsInputMoving() { return cachedInputMoving; }

        /// <summary>
        /// Returns the current primary input position in screen coordinates (pixels).
        /// For touch: position of the first touch point. For mouse: cursor position.
        /// </summary>
        /// <returns>Vector2 position in screen space (pixels).</returns>
        public Vector2 GetInputPosition() { return cachedInputPosition; }

        /// <summary>
        /// Returns all currently active touch points for multi-touch support.
        /// Array is empty when no touches are active or when using mouse-only input.
        /// Each TouchData contains id, position, and phase information.
        /// </summary>
        /// <returns>Array of TouchData for all active touches.</returns>
        public TouchData[] GetAllTouches() { return cachedTouches; }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Converts Unity's Legacy Input Manager TouchPhase to the unified TouchPhase enum.
        /// Ensures consistent touch phase representation across different input systems.
        /// </summary>
        /// <param name="phase">TouchPhase from Unity's Legacy Input Manager.</param>
        /// <returns>Unified TouchPhase enum value.</returns>
        private TouchPhase ConvertTouchPhase(UnityEngine.TouchPhase phase)
        {
            switch (phase)
            {
                case UnityEngine.TouchPhase.Began: return TouchPhase.Began;
                case UnityEngine.TouchPhase.Moved: return TouchPhase.Moved;
                case UnityEngine.TouchPhase.Ended: return TouchPhase.Ended;
                case UnityEngine.TouchPhase.Canceled: return TouchPhase.Canceled;
                default: return TouchPhase.Stationary;
            }
        }

        #endregion

        #region Data Structures

        /// <summary>
        /// Unified touch data structure providing consistent touch information
        /// across both Legacy Input Manager and New Input System.
        /// Used for multi-touch support and touch tracking.
        /// </summary>
        [System.Serializable]
        public struct TouchData
        {
            /// <summary>
            /// Unique identifier for this touch point.
            /// Persists throughout the lifetime of a single touch.
            /// </summary>
            public int id;

            /// <summary>
            /// Current screen position of the touch in pixels.
            /// Origin (0,0) is at bottom-left corner of the screen.
            /// </summary>
            public Vector2 position;

            /// <summary>
            /// Current phase/state of this touch (Began, Moved, Stationary, Ended, Canceled).
            /// </summary>
            public TouchPhase phase;
        }

        /// <summary>
        /// Unified touch phase enum providing consistent touch state representation
        /// across both Legacy Input Manager and New Input System.
        /// Matches the behavior of Unity's native TouchPhase but works with both input systems.
        /// </summary>
        public enum TouchPhase
        {
            /// <summary>Touch just started this frame - finger made contact with screen.</summary>
            Began,
            /// <summary>Touch moved significantly - finger is moving across screen.</summary>
            Moved,
            /// <summary>Touch is held in place - finger is stationary on screen.</summary>
            Stationary,
            /// <summary>Touch ended normally - finger lifted from screen.</summary>
            Ended,
            /// <summary>Touch was canceled by the system (e.g., interrupted by system UI).</summary>
            Canceled
        }

        #endregion
    }
}