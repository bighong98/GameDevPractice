using UnityEngine;
using System;
using UnityEngine.InputSystem;

namespace TH.Core
{
    public partial class InputManager
    {
        #region Rebinding
        // 입력 리바인딩 흐름을 관리하고 바인딩 변경을 알린다.
        private InputActionRebindingExtensions.RebindingOperation rebindOperation;
        private bool cancelRebindQueued;
        public bool IsRebindInProgress => rebindOperation != null;

        // 리바인딩 시작/완료/취소 시 UI 동기화를 위해 호출된다.
        public event Action<RebindResult> OnRebindStarted;
        public event Action<RebindResult> OnRebindCompleted;
        public event Action OnRebindCanceled;

        // 특정 바인딩 ID에 대한 인터랙티브 리바인딩을 시작한다.
        public bool TryStartRebind(string actionMapName, string actionName, string bindingId, RebindOptions options = null)
        {
            if (IsRebindInProgress)
                return false;

            if (!TryResolveBinding(actionMapName, actionName, bindingId, out var action, out var bindingIndex))
                return false;

            if (!IsRebindableBinding(action, bindingIndex))
                return false;

            StartRebindInternal(action, bindingIndex, options);
            return true;
        }

        public void CancelRebind()
        {
            rebindOperation?.Cancel();
        }

        // 입력 업데이트 이후로 취소를 미뤄 업데이트 타이밍 오류를 피한다.
        public void CancelRebindDeferred()
        {
            if (!IsRebindInProgress || cancelRebindQueued)
                return;

            cancelRebindQueued = true;
            InputSystem.onAfterUpdate += CancelRebindAfterUpdate;
        }

        private void CancelRebindAfterUpdate()
        {
            InputSystem.onAfterUpdate -= CancelRebindAfterUpdate;
            cancelRebindQueued = false;

            if (IsRebindInProgress)
                CancelRebind();
        }

        public bool TryGetBindingDisplayString(string actionMapName, string actionName, string bindingId,
            out string displayString, out string deviceLayoutName, out string controlPath,
            InputBinding.DisplayStringOptions options = default)
        {
            displayString = string.Empty;
            deviceLayoutName = null;
            controlPath = null;

            if (!TryResolveBinding(actionMapName, actionName, bindingId, out var action, out var bindingIndex))
                return false;

            displayString = action.GetBindingDisplayString(bindingIndex, out deviceLayoutName, out controlPath, options);
            return true;
        }

        public bool TryApplyBindingOverride(string actionMapName, string actionName, string bindingId, string overridePath)
        {
            if (!TryResolveBinding(actionMapName, actionName, bindingId, out var action, out var bindingIndex))
                return false;

            action.ApplyBindingOverride(bindingIndex, overridePath);
            return true;
        }

        // 단일 바인딩의 오버라이드를 제거해 기본값으로 복원한다.
        public bool TryClearBindingOverride(string actionMapName, string actionName, string bindingId)
        {
            if (!TryResolveBinding(actionMapName, actionName, bindingId, out var action, out var bindingIndex))
                return false;

            action.RemoveBindingOverride(bindingIndex);
            NotifyBindingChanged(action, bindingIndex);
            return true;
        }

        // 단일 바인딩을 비운다(언바인드).
        public bool TryClearBinding(string actionMapName, string actionName, string bindingId)
        {
            if (!TryResolveBinding(actionMapName, actionName, bindingId, out var action, out var bindingIndex))
                return false;

            action.ApplyBindingOverride(bindingIndex, string.Empty);
            NotifyBindingChanged(action, bindingIndex);
            return true;
        }

        public void ClearAllBindingOverrides()
        {
            UserInput.asset.RemoveAllBindingOverrides();
        }

        // UI 라벨 갱신을 위해 완료 이벤트를 발생시킨다.
        private void NotifyBindingChanged(InputAction action, int bindingIndex)
        {
            var result = BuildRebindResult(action, bindingIndex);
            OnRebindCompleted?.Invoke(result);
        }

        private void StartRebindInternal(InputAction action, int bindingIndex, RebindOptions options)
        {
            // 액션 맵을 잠시 비활성화하고 인터랙티브 리바인딩을 구성/시작한다.
            CancelRebind();
            options ??= new RebindOptions();

            var actionMap = action.actionMap;
            var actionMapWasEnabled = actionMap.enabled;
            if (actionMapWasEnabled)
                actionMap.Disable();

            rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithActionEventNotificationsBeingSuppressed();

            if (options.timeoutSeconds > 0f)
                rebindOperation.WithTimeout(options.timeoutSeconds);
            if (!string.IsNullOrEmpty(options.cancelBinding))
                rebindOperation.WithCancelingThrough(options.cancelBinding);
            if (options.excludeMouse)
                rebindOperation.WithControlsExcluding("<Mouse>");
            if (options.excludeKeyboard)
                rebindOperation.WithControlsExcluding("<Keyboard>");
            if (options.excludeGamepad)
                rebindOperation.WithControlsExcluding("<Gamepad>");

            var startResult = BuildRebindResult(action, bindingIndex);
            OnRebindStarted?.Invoke(startResult);

            rebindOperation.OnCancel(_ =>
                {
                    // 취소 시 리소스를 정리하고 UI에 취소를 알린다.
                    Cleanup();
                    OnRebindCanceled?.Invoke();
                })
                .OnComplete(_ =>
                {
                    // 완료 시 결과를 만들고 UI에 완료를 알린다.
                    var result = BuildRebindResult(action, bindingIndex);
                    Cleanup();
                    OnRebindCompleted?.Invoke(result);
                });

            rebindOperation.Start();

            void Cleanup()
            {
                rebindOperation?.Dispose();
                rebindOperation = null;
                if (actionMapWasEnabled)
                    actionMap.Enable();
            }
        }

        private bool TryResolveBinding(string actionMapName, string actionName, string bindingId,
            out InputAction action, out int bindingIndex)
        {
            // 액션맵/액션/바인딩 ID를 검증하고 대상 바인딩 인덱스를 찾는다.
            action = null;
            bindingIndex = -1;

            if (string.IsNullOrEmpty(actionMapName) || string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(bindingId))
                return false;

            var map = UserInput.asset.FindActionMap(actionMapName, false);
            if (map == null)
                return false;

            action = map.FindAction(actionName, false);
            if (action == null)
                return false;

            bindingIndex = FindBindingIndex(action, bindingId);
            return bindingIndex >= 0;
        }

        private static bool IsRebindableBinding(InputAction action, int bindingIndex)
        {
            // 복합 바인딩/비버튼 타입을 제외해 리바인딩 가능한 항목만 허용한다.
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return false;

            var binding = action.bindings[bindingIndex];
            if (binding.isComposite || binding.isPartOfComposite)
                return false;

            var expectedControlType = action.expectedControlType;
            if (!string.IsNullOrEmpty(expectedControlType) &&
                !string.Equals(expectedControlType, "Button", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private static int FindBindingIndex(InputAction action, string bindingId)
        {
            if (action == null || string.IsNullOrEmpty(bindingId))
                return -1;

            return action.bindings.IndexOf(x => x.id.ToString() == bindingId);
        }

        private static RebindResult BuildRebindResult(InputAction action, int bindingIndex)
        {
            var displayString = string.Empty;
            var deviceLayoutName = default(string);
            var controlPath = default(string);
            var bindingId = string.Empty;
            var actionMapName = string.Empty;
            var actionName = string.Empty;

            if (action != null && bindingIndex >= 0 && bindingIndex < action.bindings.Count)
            {
                actionMapName = action.actionMap != null ? action.actionMap.name : string.Empty;
                actionName = action.name;
                bindingId = action.bindings[bindingIndex].id.ToString();
                displayString = action.GetBindingDisplayString(bindingIndex, out deviceLayoutName, out controlPath);
            }

            return new RebindResult(actionMapName, actionName, bindingId, displayString, deviceLayoutName, controlPath);
        }

        #endregion
    }

    [Serializable]
    public sealed class RebindOptions
    {
        public float timeoutSeconds;
        public string cancelBinding = "<Keyboard>/escape";
        public bool excludeMouse;
        public bool excludeKeyboard;
        public bool excludeGamepad;
    }

    public readonly struct RebindResult
    {
        public readonly string ActionMap;
        public readonly string Action;
        public readonly string BindingId;
        public readonly string DisplayString;
        public readonly string DeviceLayout;
        public readonly string ControlPath;

        public RebindResult(string actionMap, string action, string bindingId, string displayString,
            string deviceLayout, string controlPath)
        {
            ActionMap = actionMap;
            Action = action;
            BindingId = bindingId;
            DisplayString = displayString;
            DeviceLayout = deviceLayout;
            ControlPath = controlPath;
        }
    }

    [Serializable]
    public sealed class KeyRebindTarget
    {
        public InputActionReference actionReference;
        public string label;
        public string actionMap;
        public string actionName;
        public string bindingId;
    }
}

