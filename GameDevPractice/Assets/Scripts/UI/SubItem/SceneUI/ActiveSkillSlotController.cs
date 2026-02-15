using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Attribute.Stat;
using TH.Combat;
using TH.Core;
using TH.Core.Service;
using TH.Resource;
using TH.UI;
using TH.Utils;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ActiveSkillSlotController : MonoBehaviour
{
    private const string SkillTooltipPrefabKey = "UI_SkillTooltip.prefab";
    private const string DefaultCategorySortProfileAddressKey = "SkillCategorySortProfileSO";
    private const float ModifiedHighlightFadeDuration = 0.5f;
    private const float WarnHighlightFadeDuration = 0.5f;

    [SerializeField] private ActiveSkillSlotPanel panel;
    [SerializeField] private string categorySortProfileAddressKey = DefaultCategorySortProfileAddressKey;

    private IPlayerHolder playerHolder;
    private ISkillController skillController;
    private IAttacker attacker;
    private IStatHolder statHolder;
    private int highlightedSlotIndex = -1;
    private int hoveredSlotIndex = -1;
    private readonly List<SkillTypeSO> displayedSkills = new();
    private readonly List<SkillTypeSO> orderedAvailableSkills = new();
    private readonly Dictionary<SkillTypeSO, int> registeredSkillOrder = new();
    private readonly HashSet<SkillTypeSO> uniqueSkillBuffer = new();
    private readonly Dictionary<SkillCategory, int> categoryPriorityMap = new();
    private bool hasDisplaySkillSnapshot;
    private IResourceLoader resourceLoader;
    private SkillCategorySortProfileSO categorySortProfile;
    private bool isCategorySortProfileLoading;

private void Awake()
    {
        if (panel == null)
            TryGetComponent(out panel);

        if (string.IsNullOrWhiteSpace(categorySortProfileAddressKey))
            categorySortProfileAddressKey = DefaultCategorySortProfileAddressKey;

        resourceLoader = ServiceLocator.Get<IResourceLoader>();
        RebuildCategoryPriorityMap();
    }

    private void OnEnable()
    {
        playerHolder ??= ServiceLocator.Get<IPlayerHolder>();

        if (panel != null)
        {
            panel.OnSlotHovered -= HandleSlotHovered;
            panel.OnSlotHovered += HandleSlotHovered;
            panel.OffSlotHovered -= HandleSlotHoverExited;
            panel.OffSlotHovered += HandleSlotHoverExited;
        }

        if (playerHolder != null)
        {
            playerHolder.OnPlayerInstanceUpdated -= HandlePlayerInstanceUpdated;
            playerHolder.OnPlayerInstanceUpdated += HandlePlayerInstanceUpdated;
            BindSkillController(playerHolder.GetPlayerInstance);
            return;
        }

        BindSkillController(null);
    }

    private IEnumerator Start()
    {
        // One-frame delayed sync to absorb initialization-order differences.
        yield return null;

        TryBindSkillControllerFromPlayerHolder();
        LoadCategorySortProfileAsync().Forget();
        RedrawAllSlots();
    }

    private async UniTaskVoid LoadCategorySortProfileAsync()
    {
        if (isCategorySortProfileLoading || categorySortProfile != null)
            return;

        if (string.IsNullOrWhiteSpace(categorySortProfileAddressKey))
            return;

        isCategorySortProfileLoading = true;
        try
        {
            resourceLoader ??= ServiceLocator.Get<IResourceLoader>();
            if (resourceLoader == null)
                return;

            if (!resourceLoader.TryLoad(categorySortProfileAddressKey, out categorySortProfile))
            {
                categorySortProfile = await resourceLoader.LoadAsync<SkillCategorySortProfileSO>(
                    categorySortProfileAddressKey,
                    destroyCancellationToken);
            }

            if (categorySortProfile == null)
            {
                Logg.LogWarning($"[{nameof(ActiveSkillSlotController)}] failed to load sort profile. key: {categorySortProfileAddressKey}");
                return;
            }

            RebuildCategoryPriorityMap();
            RedrawAllSlots();
        }
        catch (OperationCanceledException)
        {
            // ignore cancellation during destroy
        }
        catch (Exception e)
        {
            Logg.LogWarning($"[{nameof(ActiveSkillSlotController)}] sort profile load failed: {e.Message}");
        }
        finally
        {
            isCategorySortProfileLoading = false;
        }
    }

    private void OnDisable()
    {
        if (playerHolder != null)
            playerHolder.OnPlayerInstanceUpdated -= HandlePlayerInstanceUpdated;

        if (panel != null)
        {
            panel.OnSlotHovered -= HandleSlotHovered;
            panel.OffSlotHovered -= HandleSlotHoverExited;
        }

        UnbindSkillController();
        HideSkillTooltip();
        hoveredSlotIndex = -1;
    }

    private void Update()
    {
        RefreshCooldowns();
    }

    private void HandlePlayerInstanceUpdated(object playerInstance)
    {
        BindSkillController(playerInstance);
    }

    private void BindSkillController(object playerInstance)
    {
        UnbindSkillController();

        if (playerInstance is Component c)
        {
            if (!c.TryGetComponent(out skillController))
                Logg.LogError($"failed to GetComponent for {nameof(skillController)}", context: this);
            if (!c.TryGetComponent(out attacker))
                Logg.LogError($"failed to GetComponent for {nameof(attacker)}", context: this);
            if (!c.TryGetComponent(out statHolder))
                Logg.LogError($"failed to GetComponent for {nameof(statHolder)}", context: this);
        }

        if (skillController != null)
        {
            skillController.OnSkillBookChanged += HandleSkillBookChanged;
            skillController.OnAvailableSkillsChanged += HandleAvailableSkillsChanged;
            skillController.OnActiveSkillChanged += HandleActiveSkillChanged;
            skillController.OnResolvedSkillChanged += HandleResolvedSkillChanged;
            skillController.OnComboStepChanged += HandleComboStepChanged;
            skillController.OnSkillReady += HandleSkillReady;
            skillController.OnSkillSlotHighlightRequested += HandleSkillSlotHighlightRequested;
        }

        RedrawAllSlots();
    }

    private void UnbindSkillController()
    {
        if (skillController != null)
        {
            skillController.OnSkillBookChanged -= HandleSkillBookChanged;
            skillController.OnAvailableSkillsChanged -= HandleAvailableSkillsChanged;
            skillController.OnActiveSkillChanged -= HandleActiveSkillChanged;
            skillController.OnResolvedSkillChanged -= HandleResolvedSkillChanged;
            skillController.OnComboStepChanged -= HandleComboStepChanged;
            skillController.OnSkillReady -= HandleSkillReady;
            skillController.OnSkillSlotHighlightRequested -= HandleSkillSlotHighlightRequested;
        }

        skillController = null;
        attacker = null;
        statHolder = null;
        ClearPanel();
    }

    private void HandleSkillBookChanged()
    {
        RedrawAllSlots();
    }

    private void HandleAvailableSkillsChanged()
    {
        RedrawAllSlots();
    }

    private void HandleActiveSkillChanged(SkillTypeSO _)
    {
        RedrawAllSlots();
    }

    private void HandleResolvedSkillChanged(SkillTypeSO _)
    {
        RefreshResolvedSkillPresentation();
    }

    private void HandleComboStepChanged(SkillTypeSO _, int __, int ___)
    {
        RefreshResolvedSkillPresentation();
    }

    private void HandleSkillReady(SkillTypeSO skill)
    {
        DrawCooldownForSkill(skill);
    }

    private void HandleSkillSlotHighlightRequested(SkillTypeSO skill)
    {
        if (panel == null || skill == null)
            return;

        int slotIndex = FindSkillSlotIndex(skill);
        if (slotIndex < 0)
            return;

        panel.HighlightSlot(slotIndex, (int)SlotHighlightType.Warn);
        panel.UnHighlightSlotWithFade(slotIndex, (int)SlotHighlightType.Warn, WarnHighlightFadeDuration);
    }

    private void RedrawAllSlots()
    {
        if (panel == null)
            return;

        if (skillController == null)
        {
            ClearPanel();
            return;
        }

        RebuildOrderedAvailableSkills();

        int slotCount = panel.SlotCount;
        bool allowModifiedHighlight = hasDisplaySkillSnapshot;

        for (int i = 0; i < slotCount; i++)
        {
            SkillTypeSO baseSkill = i < orderedAvailableSkills.Count ? orderedAvailableSkills[i] : null;
            if (baseSkill == null)
            {
                UpdateDisplayedSkillSnapshot(i, null);
                panel.ClearSlot(i);
                continue;
            }

            SkillTypeSO displaySkill = ResolveDisplaySkill(baseSkill);
            DrawSkillWithChangeHighlight(i, displaySkill, allowModifiedHighlight);
            DrawCooldown(i, baseSkill);
            DrawSequenceTimeout(i, baseSkill);
        }

        TrimDisplayedSkillSnapshot(slotCount);
        hasDisplaySkillSnapshot = true;

        RefreshActiveSkillHighlight();
        RefreshHoveredTooltip();
    }

    private void TryBindSkillControllerFromPlayerHolder()
    {
        if (skillController != null)
            return;

        if (playerHolder == null)
            playerHolder = ServiceLocator.Get<IPlayerHolder>();

        var playerInstance = playerHolder?.GetPlayerInstance;
        if (playerInstance != null)
            BindSkillController(playerInstance);
    }

    private void RefreshCooldowns()
    {
        if (panel == null || skillController == null)
            return;

        int count = Mathf.Min(panel.SlotCount, orderedAvailableSkills.Count);
        for (int i = 0; i < count; i++)
        {
            SkillTypeSO skill = orderedAvailableSkills[i];
            if (skill == null)
            {
                UpdateDisplayedSkillSnapshot(i, null);
                panel.ClearSlot(i);
                continue;
            }

            DrawCooldown(i, skill);
            DrawSequenceTimeout(i, skill);
        }
    }

    private void DrawCooldown(int slotIndex, SkillTypeSO skill)
    {
        if (panel == null || skillController == null || skill == null)
            return;

        float remainingCooldown = skillController.GetRemainingCooldown(skill);
        panel.DrawCooldown(slotIndex, remainingCooldown, skill.Cooldown);
    }

    private void DrawSequenceTimeout(int slotIndex, SkillTypeSO skill)
    {
        if (panel == null || skillController == null || skill == null)
            return;

        if (skillController.TryGetActiveSequenceTimeout(skill, out var remainingTimeout, out var totalTimeout))
        {
            panel.DrawSequenceTimeout(slotIndex, remainingTimeout, totalTimeout);
            return;
        }

        panel.DrawSequenceTimeout(slotIndex, 0f, 0f);
    }


    private void DrawCooldownForSkill(SkillTypeSO skill)
    {
        int slotIndex = FindSkillSlotIndex(skill);
        if (slotIndex < 0)
            return;

        DrawCooldown(slotIndex, skill);
        DrawSequenceTimeout(slotIndex, skill);
    }

    private int FindSkillSlotIndex(SkillTypeSO skill)
    {
        if (skill == null || panel == null || skillController == null)
            return -1;

        int count = Mathf.Min(panel.SlotCount, orderedAvailableSkills.Count);
        for (int i = 0; i < count; i++)
        {
            if (orderedAvailableSkills[i] == skill)
                return i;
        }

        return -1;
    }

    private SkillTypeSO ResolveDisplaySkill(SkillTypeSO baseSkill)
    {
        if (baseSkill == null || skillController == null)
            return baseSkill;

        if (skillController.HasActiveSkill && skillController.ActiveSkill == baseSkill)
        {
            if (TryGetPreviewSkill(out var previewSkill))
                return previewSkill;

            if (skillController.HasResolvedSkill && skillController.ResolvedSkill != null)
                return skillController.ResolvedSkill;
        }

        return baseSkill;
    }

    private bool TryGetPreviewSkill(out SkillTypeSO previewSkill)
    {
        previewSkill = null;

        if (skillController == null || attacker == null)
            return false;

        if (!skillController.TryBuildPreviewAttackSource(attacker, out var previewAttackSource))
            return false;

        previewSkill = previewAttackSource.Skill;
        return previewSkill != null;
    }

    private void DrawSkillWithChangeHighlight(int slotIndex, SkillTypeSO displaySkill, bool allowHighlight)
    {
        if (panel == null)
            return;

        panel.DrawSkill(slotIndex, displaySkill);

        bool isChanged = UpdateDisplayedSkillSnapshot(slotIndex, displaySkill);
        if (!allowHighlight || !isChanged)
            return;

        panel.HighlightSlot(slotIndex, (int)SlotHighlightType.Modified);
        panel.UnHighlightSlotWithFade(slotIndex, (int)SlotHighlightType.Modified, ModifiedHighlightFadeDuration);
    }

    private bool UpdateDisplayedSkillSnapshot(int slotIndex, SkillTypeSO displaySkill)
    {
        if (slotIndex < 0)
            return false;

        EnsureDisplayedSkillSnapshotSize(slotIndex + 1);

        bool isChanged = displayedSkills[slotIndex] != displaySkill;
        displayedSkills[slotIndex] = displaySkill;
        return isChanged;
    }

    private void EnsureDisplayedSkillSnapshotSize(int size)
    {
        while (displayedSkills.Count < size)
        {
            displayedSkills.Add(null);
        }
    }

    private void TrimDisplayedSkillSnapshot(int size)
    {
        if (displayedSkills.Count <= size)
            return;

        displayedSkills.RemoveRange(size, displayedSkills.Count - size);
    }


    private void RefreshResolvedSkillPresentation()
    {
        if (panel == null || skillController == null || !skillController.HasActiveSkill)
            return;

        int activeSlotIndex = FindSkillSlotIndex(skillController.ActiveSkill);
        if (activeSlotIndex < 0)
            return;

        SkillTypeSO displaySkill = ResolveDisplaySkill(skillController.ActiveSkill);
        DrawSkillWithChangeHighlight(activeSlotIndex, displaySkill, allowHighlight: hasDisplaySkillSnapshot);
        DrawCooldown(activeSlotIndex, skillController.ActiveSkill);
        DrawSequenceTimeout(activeSlotIndex, skillController.ActiveSkill);
        RefreshActiveSkillHighlight();
        RefreshHoveredTooltip();
    }

    private void HandleSlotHovered(int slotIndex)
    {
        hoveredSlotIndex = slotIndex;
        ShowSkillTooltip(slotIndex);
    }

    private void HandleSlotHoverExited(int slotIndex)
    {
        if (hoveredSlotIndex != slotIndex)
            return;

        hoveredSlotIndex = -1;
        HideSkillTooltip();
    }

    private void RefreshHoveredTooltip()
    {
        if (hoveredSlotIndex < 0)
            return;

        ShowSkillTooltip(hoveredSlotIndex);
    }

    private void ShowSkillTooltip(int slotIndex)
    {
        if (!TryGetDisplaySkillBySlot(slotIndex, out var displaySkill) || displaySkill == null)
        {
            HideSkillTooltip();
            return;
        }

        Vector2 pointerPos = InputManager.Instance != null
            ? InputManager.Instance.PointerPos
            : (Vector2)Input.mousePosition;

        UIManager.Instance
            .ShowUI<SkillTooltipUI>(SkillTooltipPrefabKey, UICanvas.FeedbackOverlay)
            ?.ShowTooltipAt(pointerPos, displaySkill, statHolder);
    }

    private bool TryGetDisplaySkillBySlot(int slotIndex, out SkillTypeSO displaySkill)
    {
        displaySkill = null;

        if (skillController == null || panel == null)
            return false;

        if (slotIndex < 0 || slotIndex >= orderedAvailableSkills.Count || slotIndex >= panel.SlotCount)
            return false;

        SkillTypeSO baseSkill = orderedAvailableSkills[slotIndex];
        if (baseSkill == null)
            return false;

        displaySkill = ResolveDisplaySkill(baseSkill);
        return displaySkill != null;
    }

    private void RebuildOrderedAvailableSkills()
    {
        orderedAvailableSkills.Clear();
        registeredSkillOrder.Clear();
        uniqueSkillBuffer.Clear();

        if (skillController == null)
            return;

        var registeredSkills = skillController.RegisteredSkills;
        for (int i = 0; i < registeredSkills.Count; i++)
        {
            SkillTypeSO skill = registeredSkills[i];
            if (skill == null || registeredSkillOrder.ContainsKey(skill))
                continue;

            registeredSkillOrder.Add(skill, i);
        }

        var availableSkills = skillController.AvailableSkills;
        for (int i = 0; i < availableSkills.Count; i++)
        {
            SkillTypeSO skill = availableSkills[i];
            if (skill == null || !uniqueSkillBuffer.Add(skill))
                continue;

            orderedAvailableSkills.Add(skill);
        }

        orderedAvailableSkills.Sort(CompareSkillsForSlotOrder);
    }

    private int CompareSkillsForSlotOrder(SkillTypeSO left, SkillTypeSO right)
    {
        int leftCategoryPriority = GetCategoryPriority(left);
        int rightCategoryPriority = GetCategoryPriority(right);
        if (leftCategoryPriority != rightCategoryPriority)
            return rightCategoryPriority.CompareTo(leftCategoryPriority);

        int leftRegisteredOrder = ResolveRegisteredOrder(left);
        int rightRegisteredOrder = ResolveRegisteredOrder(right);
        if (leftRegisteredOrder != rightRegisteredOrder)
            return leftRegisteredOrder.CompareTo(rightRegisteredOrder);

        return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
    }

    private int GetCategoryPriority(SkillTypeSO skill)
    {
        SkillCategory category = skill != null ? skill.SkillCategory : SkillCategory.AdditiveSkill;
        if (categoryPriorityMap.TryGetValue(category, out int priority))
            return priority;

        return ResolveDefaultCategoryPriority(category);
    }

    private int ResolveRegisteredOrder(SkillTypeSO skill)
    {
        if (skill == null)
            return int.MaxValue;

        return registeredSkillOrder.TryGetValue(skill, out int index) ? index : int.MaxValue;
    }

    private void RebuildCategoryPriorityMap()
    {
        categoryPriorityMap.Clear();
        if (categorySortProfile != null && categorySortProfile.Rules != null)
        {
            for (int i = 0; i < categorySortProfile.Rules.Count; i++)
            {
                SkillCategorySortProfileSO.Rule rule = categorySortProfile.Rules[i];
                categoryPriorityMap[rule.category] = rule.priority;
            }
        }

        EnsureCategoryPriority(SkillCategory.WeaponDefaultSkill);
        EnsureCategoryPriority(SkillCategory.AdditiveSkill);
        EnsureCategoryPriority(SkillCategory.UltimateSkill);
    }

    private void EnsureCategoryPriority(SkillCategory category)
    {
        if (!categoryPriorityMap.ContainsKey(category))
        {
            categoryPriorityMap[category] = ResolveDefaultCategoryPriority(category);
        }
    }

    private static int ResolveDefaultCategoryPriority(SkillCategory category)
    {
        return category switch
        {
            SkillCategory.WeaponDefaultSkill => 300,
            SkillCategory.AdditiveSkill => 200,
            SkillCategory.UltimateSkill => 100,
            _ => 0
        };
    }

    private void HideSkillTooltip()
    {
        UIManager.Instance.ReleaseUI(SkillTooltipPrefabKey);
    }

    private void RefreshActiveSkillHighlight()
    {
        if (panel == null)
        {
            highlightedSlotIndex = -1;
            return;
        }

        if (highlightedSlotIndex >= 0)
            panel.UnHighlightSlot(highlightedSlotIndex);

        highlightedSlotIndex = -1;
    }

    private void ClearPanel()
    {
        if (panel == null)
            return;

        panel.ClearAllSlots();
        highlightedSlotIndex = -1;
        displayedSkills.Clear();
        orderedAvailableSkills.Clear();
        registeredSkillOrder.Clear();
        uniqueSkillBuffer.Clear();
        hasDisplaySkillSnapshot = false;
    }
}
