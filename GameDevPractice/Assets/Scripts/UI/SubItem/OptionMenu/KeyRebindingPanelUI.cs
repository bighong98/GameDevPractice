using System;
using System.Collections.Generic;
using TH.Core;
using TH.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyRebindingPanelUI : MonoBehaviour
{
    [Serializable]
    public sealed class RebindTarget
    {
        public InputActionReference actionReference;
        public string label;
        public string actionMap;
        public string actionName;
        public string bindingId;
    }

    [SerializeField] private KeyRebindingOptionItemUI optionItemTemplate;
    [SerializeField] private Transform optionItemParent;
    [SerializeField] private List<RebindTarget> rebindTargets = new();

    private readonly Dictionary<string, KeyRebindingOptionItemUI> itemsByBindingId = new();
    private InputManager inputManager;
    private string currentBindingId;

    private void Awake()
    {
        inputManager = InputManager.Instance;
        CreateItems();
    }

    private void OnEnable()
    {
        if (inputManager == null)
            inputManager = InputManager.Instance;

        inputManager.OnRebindStarted += HandleRebindStarted;
        inputManager.OnRebindCompleted += HandleRebindCompleted;
        inputManager.OnRebindCanceled += HandleRebindCanceled;
    }

    private void OnDisable()
    {
        if (inputManager == null)
            return;

        inputManager.OnRebindStarted -= HandleRebindStarted;
        inputManager.OnRebindCompleted -= HandleRebindCompleted;
        inputManager.OnRebindCanceled -= HandleRebindCanceled;
    }

    private void CreateItems()
    {
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
                () => ResetBinding(localTarget));

            itemsByBindingId[target.bindingId] = item;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (rebindTargets == null)
            return;

        foreach (var target in rebindTargets)
        {
            if (target == null || target.actionReference == null || target.actionReference.action == null)
                continue;

            var action = target.actionReference.action;
            target.actionName = action.name;
            target.actionMap = action.actionMap != null ? action.actionMap.name : string.Empty;
            target.label = action.name;
            var bindingIndex = FindFirstRebindableBindingIndex(action);
            if (bindingIndex >= 0)
                target.bindingId = action.bindings[bindingIndex].id.ToString();
        }
    }
#endif

    private void StartRebind(RebindTarget target)
    {
        if (inputManager == null)
            return;

        if (!inputManager.TryStartRebind(target.actionMap, target.actionName, target.bindingId))
            return;

        currentBindingId = target.bindingId;
        if (itemsByBindingId.TryGetValue(target.bindingId, out var item))
        {
            item.SetInteractable(false);
            item.SetBindingText("<Waiting...>");
        }
    }

    private void ResetBinding(RebindTarget target)
    {
        if (inputManager == null)
            return;

        if (inputManager.TryClearBindingOverride(target.actionMap, target.actionName, target.bindingId))
            UpdateItemDisplay(target);
    }

    private void HandleRebindStarted(InputManager.RebindResult result)
    {
        currentBindingId = result.BindingId;
    }

    private void HandleRebindCompleted(InputManager.RebindResult result)
    {
        if (itemsByBindingId.TryGetValue(result.BindingId, out var item))
        {
            var target = FindTarget(result.BindingId);
            if (target != null)
                item.SetBindingText(GetBindingDisplayString(target));
            item.SetInteractable(true);
        }

        currentBindingId = null;
    }

    private void HandleRebindCanceled()
    {
        if (string.IsNullOrEmpty(currentBindingId))
            return;

        var target = FindTarget(currentBindingId);
        if (target != null && itemsByBindingId.TryGetValue(currentBindingId, out var item))
        {
            item.SetBindingText(GetBindingDisplayString(target));
            item.SetInteractable(true);
        }

        currentBindingId = null;
    }

    private void UpdateItemDisplay(RebindTarget target)
    {
        if (itemsByBindingId.TryGetValue(target.bindingId, out var item))
            item.SetBindingText(GetBindingDisplayString(target));
    }

    private string GetBindingDisplayString(RebindTarget target)
    {
        if (inputManager == null)
            return "<Unbound>";

        if (inputManager.TryGetBindingDisplayString(
                target.actionMap,
                target.actionName,
                target.bindingId,
                out var displayString,
                out _,
                out _))
            return displayString;

        return "<Unbound>";
    }

    private RebindTarget FindTarget(string bindingId)
    {
        for (int i = 0; i < rebindTargets.Count; i++)
        {
            var target = rebindTargets[i];
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
