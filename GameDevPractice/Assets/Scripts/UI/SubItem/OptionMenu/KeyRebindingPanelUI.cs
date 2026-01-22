using System;
using System.Collections.Generic;
using TH.Core;
using TH.Core.Service;
using TH.Utils;
using TH.UI.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TH.UI
{
    // 키 리바인딩 옵션 패널. 항목을 생성하고 바인딩 라벨을 동기화한다.
    public class KeyRebindingPanelUI : OptionPanelUIBase
    {
        [SerializeField] private KeyRebindingOptionItemUI optionItemTemplate;
        [SerializeField] private Transform optionItemParent;

        private IReadOnlyCollection<KeyRebindTarget> rebindTargets;

        private readonly Dictionary<string, KeyRebindingOptionItemUI> itemsByBindingId = new();

        private RebindableActionCatalogSO rebindableActionCatalogSO;
        private const string rebindableActionCatalogSOKey = "RebindableActionCatalogSO";

        private string pendingBindingDisplay;
        private string currentBindingId;

        private bool EnsureRebindTargets()
        {
            if (rebindTargets != null)
                return true;

            ResourceManager.Instance.TryLoad(rebindableActionCatalogSOKey, out rebindableActionCatalogSO);
            if (rebindableActionCatalogSO == null)
            {
                Logg.LogWarning("[KeyRebindingPanelUI] rebindableActionCatalogSO is not set");
                return false;
            }

            rebindTargets = rebindableActionCatalogSO.RebindableActions;
            if (rebindTargets == null)
            {
                Logg.LogWarning("[KeyRebindingPanelUI] rebindableActionCatalogSO.RebindableActions is null");
                return false;
            }

            return true;
        }

        private void Awake()
        {
            CreateItems();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            InputManager.Instance.OnRebindStarted += HandleRebindStarted;
            InputManager.Instance.OnRebindCompleted += HandleRebindCompleted;
            InputManager.Instance.OnRebindCanceled += HandleRebindCanceled;
        }

        private void OnDisable()
        {
            // 리바인딩 중 패널이 비활성화되면 이전 표시를 복원하고 안전하게 취소한다.
            CancelRebindWithRestore();
            InputManager.Instance.OnRebindStarted -= HandleRebindStarted;
            InputManager.Instance.OnRebindCompleted -= HandleRebindCompleted;
            InputManager.Instance.OnRebindCanceled -= HandleRebindCanceled;
        }

        protected override void OnMenuClosed()
        {
            // 메뉴 닫힘 경로도 비활성화와 동일한 폴백을 사용한다.
            CancelRebindWithRestore();
        }

        private void CancelRebindWithRestore()
        {
            if (InputManager.Instance == null || !InputManager.Instance.IsRebindInProgress)
                return;

            RestorePendingBinding();
            currentBindingId = null;
            pendingBindingDisplay = null;
            InputManager.Instance.CancelRebindDeferred();
        }

        protected override void SyncFromSettings()
        {
            if (InputManager.Instance == null)
                return;

            if (!EnsureRebindTargets())
                return;

            foreach (var target in rebindTargets)
            {
                if (target == null || string.IsNullOrEmpty(target.bindingId))
                    continue;

                UpdateItemDisplay(target);
            }
        }

        protected override void ResetToDefaults()
        {
            if (InputManager.Instance == null)
                return;

            if (!EnsureRebindTargets())
                return;

            foreach (var target in rebindTargets)
            {
                if (target == null || string.IsNullOrEmpty(target.bindingId))
                    continue;

                if (InputManager.Instance.TryClearBindingOverride(target.actionMap, target.actionName, target.bindingId))
                    UpdateItemDisplay(target);
            }
        }

        private void CreateItems()
        {
            // 타겟 목록을 기반으로 옵션 항목 UI를 생성하고 이벤트를 연결한다.
            if (!EnsureRebindTargets())
                return;

            if (optionItemTemplate == null)
            {
                Logg.LogError("[KeyRebindingPanelUI] optionItemTemplate is not set");
                return;
            }

            Transform parent = optionItemParent != null ? optionItemParent : transform;
            itemsByBindingId.Clear();

            foreach (var target in rebindTargets)
            {
                if (target == null || string.IsNullOrEmpty(target.bindingId))
                    continue;

                var item = Instantiate(optionItemTemplate, parent);
                item.gameObject.name = $"{target.actionName}_RebindOption";
                item.gameObject.SetActive(true);

                var label = string.IsNullOrEmpty(target.label) ? target.actionName : target.label;
                var bindingDisplay = GetBindingDisplayString(target);

                var localTarget = target;
                item.Initialize(label, bindingDisplay,
                    () => StartRebind(localTarget),
                    () => ResetBinding(localTarget),
                    () => ResetBindingToDefault(localTarget));

                itemsByBindingId[target.bindingId] = item;
            }
        }

        private void StartRebind(KeyRebindTarget target)
        {
            // 리바인딩을 시작하고 대기 상태 UI로 전환한다.
            if (!InputManager.Instance.TryStartRebind(target.actionMap, target.actionName, target.bindingId))
                return;

            currentBindingId = target.bindingId;
            // 리바인딩이 중단되면 복원할 수 있도록 현재 라벨을 보관한다.
            pendingBindingDisplay = GetBindingDisplayString(target);
            if (itemsByBindingId.TryGetValue(target.bindingId, out var item))
            {
                item.SetInteractable(false);
                item.SetBindingText("<Waiting...>");
            }
        }

        private void ResetBinding(KeyRebindTarget target)
        {
            // 현재 바인딩을 비워(언바인드) 표시를 갱신한다.
            if (InputManager.Instance.TryClearBinding(target.actionMap, target.actionName, target.bindingId))
                UpdateItemDisplay(target);
        }

        private void ResetBindingToDefault(KeyRebindTarget target)
        {
            // 오버라이드를 제거해 기본값으로 되돌리고 표시를 갱신한다.
            if (InputManager.Instance.TryClearBindingOverride(target.actionMap, target.actionName, target.bindingId))
                UpdateItemDisplay(target);
        }

        private void HandleRebindStarted(RebindResult result)
        {
            currentBindingId = result.BindingId;
        }

        private void HandleRebindCompleted(RebindResult result)
        {
            // 완료된 바인딩 결과를 UI에 반영하고 버튼을 다시 활성화한다.
            if (itemsByBindingId.TryGetValue(result.BindingId, out var item))
            {
                var target = FindTarget(result.BindingId);
                if (target != null)
                    item.SetBindingText(GetBindingDisplayString(target));
                item.SetInteractable(true);
            }

            currentBindingId = null;
            pendingBindingDisplay = null;
        }

        private void HandleRebindCanceled()
        {
            // 취소 시 리바인딩 전 라벨을 복원한다.
            if (string.IsNullOrEmpty(currentBindingId))
                return;

            RestorePendingBinding();
            currentBindingId = null;
            pendingBindingDisplay = null;
        }

        private void RestorePendingBinding()
        {
            // 리바인딩 시작 전 라벨을 복구해 UI 상태를 되돌린다.
            if (string.IsNullOrEmpty(currentBindingId))
                return;

            if (itemsByBindingId.TryGetValue(currentBindingId, out var item))
            {
                if (!string.IsNullOrEmpty(pendingBindingDisplay))
                    item.SetBindingText(pendingBindingDisplay);
                else
                {
                    var target = FindTarget(currentBindingId);
                    if (target != null)
                        item.SetBindingText(GetBindingDisplayString(target));
                }

                item.SetInteractable(true);
            }
        }

        private void UpdateItemDisplay(KeyRebindTarget target)
        {
            // 현재 바인딩 문자열을 다시 읽어 UI 라벨을 갱신한다.
            if (itemsByBindingId.TryGetValue(target.bindingId, out var item))
                item.SetBindingText(GetBindingDisplayString(target));
        }

        private string GetBindingDisplayString(KeyRebindTarget target)
        {
            if (InputManager.Instance.TryGetBindingDisplayString(
                    target.actionMap,
                    target.actionName,
                    target.bindingId,
                    out var displayString,
                    out _,
                    out _))
                return string.IsNullOrEmpty(displayString) ? "<Unbound>" : displayString;

            return "<Unbound>";
        }

        private KeyRebindTarget FindTarget(string bindingId)
        {
            if (!EnsureRebindTargets())
                return null;

            foreach (var target in rebindTargets)
            {
                if (target != null && target.bindingId == bindingId)
                    return target;
            }

            return null;
        }

        private static int FindFirstRebindableBindingIndex(InputAction action)
        {
            if (action == null)
                return -1;

            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite)
                    continue;

                var expectedControlType = action.expectedControlType;
                if (!string.IsNullOrEmpty(expectedControlType) &&
                    !string.Equals(expectedControlType, "Button", StringComparison.OrdinalIgnoreCase))
                    continue;

                return i;
            }

            return -1;
        }
    }
}