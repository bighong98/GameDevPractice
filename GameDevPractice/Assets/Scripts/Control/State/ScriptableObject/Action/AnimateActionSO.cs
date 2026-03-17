using System.Collections.Generic;
using System;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateActionSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateActionSO")]
    public sealed class AnimateActionSO : CharacterActionSO
    {
        #region Enums

        private enum ControllerMatchMode
        {
            Any,
            RuntimeController,
            BaseController
        }

        private enum StatePlayMode
        {
            CrossFade,
            Play
        }

        private enum ParameterApplyOrder
        {
            BeforeState,
            AfterState
        }

        private enum ParameterOperation
        {
            SetFloat,
            SetInt,
            SetBool,
            SetTrigger,
            ResetTrigger
        }

        #endregion

        #region Inner structure (ParameterCommand)

        [Serializable]
        private struct ParameterCommand
        {
            public bool enabled;
            public ParameterOperation operation;
            public string parameterName;
            public float floatValue;
            public int intValue;
            public bool boolValue;
        }

        #endregion

        // [Header("Animator Target")]
        [SerializeField] private RuntimeAnimatorController targetAnimatorController;
        [SerializeField] private ControllerMatchMode controllerMatchMode = ControllerMatchMode.Any;
        [SerializeField] private bool searchInChildrenWhenControllerSpecified = false;
        

        [Header("Animator Override")]
        [SerializeField] private bool applyOverrideController = false;
        [SerializeField] private AnimatorOverrideController overrideController;
        [SerializeField] private bool requireMatchingBaseController = false;
        [SerializeField] private bool skipWhenOverrideControllerMissing = true;
        [SerializeField] private bool skipWhenBaseControllerMismatch = true;
        [SerializeField] private bool skipWhenControllerMismatch = true;

        [Header("State")]
        [SerializeField] private bool playState = true;
        [SerializeField] private string stateName = "";
        [SerializeField, Min(0)] private int layerIndex = AnimatorBaseLayer;
        [SerializeField] private StatePlayMode statePlayMode = StatePlayMode.CrossFade;
        [SerializeField, Min(0f)] private float transitionDuration = 0.1f;
        [SerializeField] private bool useNormalizedTimeOffset = false;
        [SerializeField, Range(0f, 1f)] private float normalizedTimeOffset = 0f;
        [SerializeField] private bool skipWhenAlreadyInState = false;
        [SerializeField] private bool skipWhenStateNotFound = true;

        [Header("Parameters")]
        [SerializeField] private bool applyParameters = true;
        [SerializeField] private ParameterApplyOrder parameterApplyOrder = ParameterApplyOrder.BeforeState;
        [SerializeField] private bool skipMissingOrInvalidParameter = true;
        [SerializeField] private List<ParameterCommand> parameterCommands = new();

        [Header("Debug")]
        
        [NonSerialized] private static readonly Dictionary<string, int> RuntimeStringHashCache = new(StringComparer.Ordinal);
        [NonSerialized] private static readonly object RuntimeStringHashCacheLock = new();
        [SerializeField] private bool enableDebugLog = false;

        public override void Execute(IActionStateController controller)
        {
            if (!TryResolveAnimator(controller, out Animator animator))
            {
                if (enableDebugLog)
                {
                    Logg.LogWarning("[AnimateActionSO] Animator not found on controller.", this);
                }
                return;
            }

            if (!IsControllerMatched(animator))
            {
                if (enableDebugLog)
                {
                    string runtimeName = animator.runtimeAnimatorController != null
                        ? animator.runtimeAnimatorController.name
                        : "null";
                    string targetName = targetAnimatorController != null
                        ? targetAnimatorController.name
                        : "null";
                    Logg.LogWarning($"[AnimateActionSO] Controller mismatch. runtime={runtimeName}, target={targetName}, mode={controllerMatchMode}", this);
                }

                if (skipWhenControllerMismatch)
                {
                    return;
                }
            }

            if (!ApplyOverrideControllerIfNeeded(animator))
            {
                return;
            }

            if (applyParameters && parameterApplyOrder == ParameterApplyOrder.BeforeState && !ApplyParameterCommands(animator))
            {
                return;
            }

            if (playState && !PlayState(animator))
            {
                return;
            }

            if (applyParameters && parameterApplyOrder == ParameterApplyOrder.AfterState)
            {
                ApplyParameterCommands(animator);
            }
        }

        private bool TryResolveAnimator(IActionStateController controller, out Animator animator)
        {
            animator = null;
            if (controller == null)
            {
                return false;
            }

            if (targetAnimatorController != null && searchInChildrenWhenControllerSpecified)
            {
                if (controller.Components.TryGet(out Transform rootTransform) && rootTransform != null)
                {
                    Animator[] animators = rootTransform.GetComponentsInChildren<Animator>(true);
                    for (int i = 0; i < animators.Length; i++)
                    {
                        Animator candidate = animators[i];
                        if (candidate != null && IsControllerMatched(candidate))
                        {
                            animator = candidate;
                            return true;
                        }
                    }
                }
            }

            if (controller.Components.TryGet(out Animator defaultAnimator) && defaultAnimator != null)
            {
                animator = defaultAnimator;
                return true;
            }

            return false;
        }

        private bool IsControllerMatched(Animator animator)
        {
            if (animator == null)
            {
                return false;
            }

            if (controllerMatchMode == ControllerMatchMode.Any || targetAnimatorController == null)
            {
                return true;
            }

            RuntimeAnimatorController runtimeController = animator.runtimeAnimatorController;
            if (runtimeController == null)
            {
                return false;
            }

            switch (controllerMatchMode)
            {
                case ControllerMatchMode.RuntimeController:
                    return ReferenceEquals(runtimeController, targetAnimatorController);

                case ControllerMatchMode.BaseController:
                    return ReferenceEquals(
                        ResolveBaseController(runtimeController),
                        ResolveBaseController(targetAnimatorController));

                default:
                    return true;
            }
        }

        private static RuntimeAnimatorController ResolveBaseController(RuntimeAnimatorController controller)
        {
            if (controller is AnimatorOverrideController overrideController &&
                overrideController.runtimeAnimatorController != null)
            {
                return overrideController.runtimeAnimatorController;
            }

            return controller;
        }

        private bool ApplyOverrideControllerIfNeeded(Animator animator)
        {
            if (!applyOverrideController)
            {
                return true;
            }

            if (animator == null)
            {
                return false;
            }

            if (overrideController == null)
            {
                if (enableDebugLog)
                {
                    Logg.LogWarning("[AnimateActionSO] Override application is enabled but overrideController is null.", this);
                }

                return skipWhenOverrideControllerMissing;
            }

            RuntimeAnimatorController currentController = animator.runtimeAnimatorController;
            if (ReferenceEquals(currentController, overrideController))
            {
                return true;
            }

            if (requireMatchingBaseController)
            {
                RuntimeAnimatorController currentBase = ResolveBaseController(currentController);
                RuntimeAnimatorController overrideBase = ResolveBaseController(overrideController);
                bool baseMatched = currentBase != null && overrideBase != null && ReferenceEquals(currentBase, overrideBase);
                if (!baseMatched)
                {
                    if (enableDebugLog)
                    {
                        string currentName = currentController != null ? currentController.name : "null";
                        string overrideName = overrideController.name;
                        Logg.LogWarning($"[AnimateActionSO] Override base controller mismatch. current={currentName}, override={overrideName}", this);
                    }

                    return skipWhenBaseControllerMismatch;
                }
            }

            animator.runtimeAnimatorController = overrideController;

            if (enableDebugLog)
            {
                string controllerName = overrideController != null ? overrideController.name : "null";
                Logg.Log($"[AnimateActionSO] Applied AnimatorOverrideController: {controllerName}", Logg.LoggingMode.InProgress, this);
            }

            return true;
        }


        private bool PlayState(Animator animator)
        {
            if (animator == null)
            {
                return false;
            }

            int safeLayerIndex = ResolveLayerIndex(animator, layerIndex);
            if (safeLayerIndex < 0)
            {
                if (enableDebugLog)
                {
                    Logg.LogWarning($"[AnimateActionSO] Invalid layer index: {layerIndex}", this);
                }
                return false;
            }

            if (!TryResolveStateHash(animator, safeLayerIndex, stateName, out int stateHash))
            {
                if (enableDebugLog)
                {
                    Logg.LogWarning($"[AnimateActionSO] Failed to resolve state '{stateName}' at layer {safeLayerIndex}.", this);
                }
                return skipWhenStateNotFound;
            }

            if (skipWhenAlreadyInState && IsCurrentOrNextState(animator, safeLayerIndex, stateHash))
            {
                if (enableDebugLog)
                {
                    Logg.Log($"[AnimateActionSO] Skip state play because already in state(hash={stateHash}).", Logg.LoggingMode.InProgress, this);
                }
                return true;
            }

            float clampedTransitionDuration = Mathf.Max(0f, transitionDuration);
            float clampedNormalizedTime = Mathf.Clamp01(normalizedTimeOffset);

            switch (statePlayMode)
            {
                case StatePlayMode.CrossFade:
                    if (useNormalizedTimeOffset)
                    {
                        animator.CrossFade(stateHash, clampedTransitionDuration, safeLayerIndex, clampedNormalizedTime);
                    }
                    else
                    {
                        animator.CrossFade(stateHash, clampedTransitionDuration, safeLayerIndex);
                    }
                    break;

                case StatePlayMode.Play:
                    if (useNormalizedTimeOffset)
                    {
                        animator.Play(stateHash, safeLayerIndex, clampedNormalizedTime);
                    }
                    else
                    {
                        animator.Play(stateHash, safeLayerIndex);
                    }
                    break;
            }

            if (enableDebugLog)
            {
                string mode = statePlayMode.ToString();
                Logg.Log($"[AnimateActionSO] {mode} -> stateHash={stateHash}, layer={safeLayerIndex}, transition={clampedTransitionDuration:0.000}, normalizedOffset={clampedNormalizedTime:0.000}", Logg.LoggingMode.InProgress, this);
            }

            return true;
        }

        private bool ApplyParameterCommands(Animator animator)
        {
            if (animator == null || parameterCommands == null || parameterCommands.Count == 0)
            {
                return true;
            }

            for (int i = 0; i < parameterCommands.Count; i++)
            {
                ParameterCommand command = parameterCommands[i];
                if (!command.enabled)
                {
                    continue;
                }

                if (!TryGetParameter(animator, command.parameterName, out AnimatorControllerParameter parameter))
                {
                    if (!HandleParameterFailure($"[AnimateActionSO] Parameter not found: '{command.parameterName}'"))
                    {
                        return false;
                    }
                    continue;
                }

                if (!ApplySingleParameter(animator, parameter, command, i))
                {
                    return false;
                }
            }

            return true;
        }

        private bool ApplySingleParameter(Animator animator, AnimatorControllerParameter parameter, ParameterCommand command, int index)
        {
            switch (command.operation)
            {
                case ParameterOperation.SetFloat:
                    if (parameter.type != AnimatorControllerParameterType.Float)
                    {
                        return HandleParameterFailure($"[AnimateActionSO] Parameter type mismatch at index {index}. Expected Float: '{command.parameterName}'");
                    }
                    animator.SetFloat(parameter.nameHash, command.floatValue);
                    return true;

                case ParameterOperation.SetInt:
                    if (parameter.type != AnimatorControllerParameterType.Int)
                    {
                        return HandleParameterFailure($"[AnimateActionSO] Parameter type mismatch at index {index}. Expected Int: '{command.parameterName}'");
                    }
                    animator.SetInteger(parameter.nameHash, command.intValue);
                    return true;

                case ParameterOperation.SetBool:
                    if (parameter.type != AnimatorControllerParameterType.Bool)
                    {
                        return HandleParameterFailure($"[AnimateActionSO] Parameter type mismatch at index {index}. Expected Bool: '{command.parameterName}'");
                    }
                    animator.SetBool(parameter.nameHash, command.boolValue);
                    return true;

                case ParameterOperation.SetTrigger:
                    if (parameter.type != AnimatorControllerParameterType.Trigger)
                    {
                        return HandleParameterFailure($"[AnimateActionSO] Parameter type mismatch at index {index}. Expected Trigger: '{command.parameterName}'");
                    }
                    animator.SetTrigger(parameter.nameHash);
                    return true;

                case ParameterOperation.ResetTrigger:
                    if (parameter.type != AnimatorControllerParameterType.Trigger)
                    {
                        return HandleParameterFailure($"[AnimateActionSO] Parameter type mismatch at index {index}. Expected Trigger: '{command.parameterName}'");
                    }
                    animator.ResetTrigger(parameter.nameHash);
                    return true;

                default:
                    return true;
            }
        }

        private bool HandleParameterFailure(string message)
        {
            if (enableDebugLog)
            {
                Logg.LogWarning(message, this);
            }

            return skipMissingOrInvalidParameter;
        }

        private static bool TryGetParameter(Animator animator, string parameterName, out AnimatorControllerParameter parameter)
        {
            parameter = null;
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            int hash = GetCachedStringHash(parameterName);
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter candidate = parameters[i];
                if (candidate.nameHash == hash)
                {
                    parameter = candidate;
                    return true;
                }
            }

            return false;
        }

        private static int ResolveLayerIndex(Animator animator, int requestedLayerIndex)
        {
            if (animator == null || animator.layerCount <= 0)
            {
                return -1;
            }

            if (requestedLayerIndex < 0 || requestedLayerIndex >= animator.layerCount)
            {
                return -1;
            }

            return requestedLayerIndex;
        }

        private static bool TryResolveStateHash(Animator animator, int targetLayerIndex, string stateName, out int stateHash)
        {
            stateHash = 0;
            if (animator == null || string.IsNullOrWhiteSpace(stateName))
            {
                return false;
            }

            if (targetLayerIndex < 0 || targetLayerIndex >= animator.layerCount)
            {
                return false;
            }

            if (TryGetStateHashInternal(animator, targetLayerIndex, stateName, out stateHash))
            {
                return true;
            }

            if (stateName.Contains("."))
            {
                return false;
            }

            string layerQualifiedStateName = $"{animator.GetLayerName(targetLayerIndex)}.{stateName}";
            return TryGetStateHashInternal(animator, targetLayerIndex, layerQualifiedStateName, out stateHash);
        }

        private static bool TryGetStateHashInternal(Animator animator, int targetLayerIndex, string stateName, out int stateHash)
        {
            stateHash = 0;
            int hash = GetCachedStringHash(stateName);
            if (!animator.HasState(targetLayerIndex, hash))
            {
                return false;
            }

            stateHash = hash;
            return true;
        }

        private static int GetCachedStringHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return 0;
            }

            lock (RuntimeStringHashCacheLock)
            {
                if (RuntimeStringHashCache.TryGetValue(value, out int cachedHash))
                {
                    return cachedHash;
                }

                int hash = Animator.StringToHash(value);
                RuntimeStringHashCache[value] = hash;
                return hash;
            }
        }

        private static bool IsCurrentOrNextState(Animator animator, int targetLayerIndex, int stateHash)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(targetLayerIndex);
            if (currentState.shortNameHash == stateHash || currentState.fullPathHash == stateHash)
            {
                return true;
            }

            if (!animator.IsInTransition(targetLayerIndex))
            {
                return false;
            }

            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(targetLayerIndex);
            return nextState.shortNameHash == stateHash || nextState.fullPathHash == stateHash;
        }
    }
}
