using System;
using UnityEngine;
using Unity.Cinemachine;

#if CINEMACHINE_UNITY_INPUTSYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
#endif

namespace TH.Core.Input
{
    public interface IInputAxisInputBlocker
    {
        bool IsInputBlocked();
    }

    [ExecuteAlways]
    [SaveDuringPlay]
    [AddComponentMenu("Cinemachine/Helpers/Filtered Input Axis Controller")]
    public class FilteredCinemachineInputAxisController
        : InputAxisControllerBase<FilteredCinemachineInputAxisController.Reader>
    {
#if CINEMACHINE_UNITY_INPUTSYSTEM
        [Tooltip("Leave this at -1 for single-player games.  "
            + "For multi-player games, set this to be the player index, and the actions will "
            + "be read from that player's controls")]
        public int PlayerIndex = -1;

        [Tooltip("If set, Input Actions will be auto-enabled at start")]
        public bool AutoEnableInputs = true;
#endif
        [Header("Input Filtering")]
        [Tooltip("Optional behaviour that decides whether input is blocked.")]
        public MonoBehaviour InputBlockerBehaviour;

        [Tooltip("Block input when Time.timeScale is 0 during play mode.")]
        public bool BlockWhenTimeScaleZero = true;

        private IInputAxisInputBlocker blocker;

#if CINEMACHINE_UNITY_INPUTSYSTEM
        public Reader.ControlValueReader ReadControlValueOverride;
#endif
        internal delegate void SetControlDefaultsForAxis(
            in IInputAxisOwner.AxisDescriptor axis, ref Controller controller);
        internal static SetControlDefaultsForAxis SetControlDefaults;

#if CINEMACHINE_UNITY_INPUTSYSTEM
        protected override void Reset()
        {
            base.Reset();
            PlayerIndex = -1;
            AutoEnableInputs = true;
        }
#endif
        private void Awake()
        {
            if (InputBlockerBehaviour != null)
                blocker = InputBlockerBehaviour as IInputAxisInputBlocker;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            if (InputBlockerBehaviour != null)
                blocker = InputBlockerBehaviour as IInputAxisInputBlocker;
            else
                blocker = null;
        }

        protected override void InitializeControllerDefaultsForAxis(
            in IInputAxisOwner.AxisDescriptor axis, Controller controller)
        {
            SetControlDefaults?.Invoke(axis, ref controller);
        }

        private bool IsInputBlocked()
        {
            if (Application.isPlaying && BlockWhenTimeScaleZero && Time.timeScale <= 0f)
                return true;

            return blocker != null && blocker.IsInputBlocked();
        }

        void Update()
        {
            if (Application.isPlaying)
                UpdateControllers();
        }

        [Serializable]
        public sealed class Reader : IInputAxisReader
        {
#if CINEMACHINE_UNITY_INPUTSYSTEM
            [Tooltip("Action mapping for the Input package.")]
            public InputActionReference InputAction;

            [Tooltip("The input value is multiplied by this amount prior to processing.  "
                + "Controls the input power.  Set it to a negative value to invert the input")]
            public float Gain = 1;

            [NonSerialized] internal InputAction m_CachedAction;

            public delegate float ControlValueReader(
                InputAction action, IInputAxisOwner.AxisDescriptor.Hints hint, UnityEngine.Object context,
                ControlValueReader defaultReader);
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            [InputAxisNameProperty]
            [Tooltip("Axis name for the Legacy Input system (if used).  "
                + "This value will be used to control the axis.")]
            public string LegacyInput;

            [Tooltip("The LegacyInput value is multiplied by this amount prior to processing.  "
                + "Controls the input power.  Set it to a negative value to invert the input")]
            public float LegacyGain = 1;
#endif

            [Tooltip("Enable this if the input value is inherently dependent on frame time.  "
                + "For example, mouse deltas will naturally be bigger for longer frames, so "
                + "in this case the default deltaTime scaling should be canceled.")]
            public bool CancelDeltaTime = false;

            public float GetValue(
                UnityEngine.Object context,
                IInputAxisOwner.AxisDescriptor.Hints hint)
            {
                if (context is FilteredCinemachineInputAxisController filtered && filtered.IsInputBlocked())
                    return 0f;

                float inputValue = 0;
#if CINEMACHINE_UNITY_INPUTSYSTEM
                if (InputAction != null)
                {
                    if (context is FilteredCinemachineInputAxisController c)
                        inputValue = ResolveAndReadInputAction(c, hint) * Gain;
                }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
                if (inputValue == 0 && !string.IsNullOrEmpty(LegacyInput))
                {
                    try { inputValue = CinemachineCore.GetInputAxis(LegacyInput) * LegacyGain; }
                    catch (ArgumentException) {}
                }
#endif
                return (Time.deltaTime > 0 && CancelDeltaTime) ? inputValue / Time.deltaTime : inputValue;
            }

#if CINEMACHINE_UNITY_INPUTSYSTEM
            float ResolveAndReadInputAction(
                FilteredCinemachineInputAxisController context,
                IInputAxisOwner.AxisDescriptor.Hints hint)
            {
                if (m_CachedAction != null && InputAction.action.id != m_CachedAction.id)
                    m_CachedAction = null;
                if (m_CachedAction == null)
                {
                    m_CachedAction = InputAction.action;
                    if (context.PlayerIndex != -1)
                        m_CachedAction = GetFirstMatch(InputUser.all[context.PlayerIndex], InputAction);
                    if (context.AutoEnableInputs && m_CachedAction != null)
                        m_CachedAction.Enable();

                    static InputAction GetFirstMatch(in InputUser user, InputActionReference aRef)
                    {
                        var iter = user.actions.GetEnumerator();
                        while (iter.MoveNext())
                            if (iter.Current.id == aRef.action.id)
                                return iter.Current;
                        return null;
                    }
                }

                if (m_CachedAction != null && m_CachedAction.enabled != InputAction.action.enabled)
                {
                    if (InputAction.action.enabled)
                        m_CachedAction.Enable();
                    else
                        m_CachedAction.Disable();
                }

                if (m_CachedAction != null)
                {
                    if (context.ReadControlValueOverride != null)
                        return context.ReadControlValueOverride.Invoke(m_CachedAction, hint, context, ReadInput);
                    return ReadInput(m_CachedAction, hint, context, null);
                }
                return 0;
            }

            float ReadInput(
                InputAction action, IInputAxisOwner.AxisDescriptor.Hints hint,
                UnityEngine.Object context, ControlValueReader defaultReader)
            {
                if (action.activeValueType != null)
                {
                    if (action.activeValueType == typeof(Vector2))
                    {
                        var value = action.ReadValue<Vector2>();
                        return hint == IInputAxisOwner.AxisDescriptor.Hints.Y ? value.y : value.x;
                    }
                    if (action.activeValueType == typeof(float))
                        return action.ReadValue<float>();

                    Debug.LogError($"{context.name} - {action.name}: FilteredCinemachineInputAxisController.Reader can only read "
                        + "actions of type float or Vector2.  To read other types you can install a custom handler for "
                        + "FilteredCinemachineInputAxisController.ReadControlValueOverride.");
                }
                return 0f;
            }
#endif
        }
    }
}
