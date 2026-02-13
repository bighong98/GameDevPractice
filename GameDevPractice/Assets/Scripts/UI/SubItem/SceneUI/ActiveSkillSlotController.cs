using System.Collections;
using System.Collections.Generic;
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
    private const float ModifiedHighlightFadeDuration = 0.5f;

    [SerializeField] private ActiveSkillSlotPanel panel;
    private IPlayerHolder playerHolder;
    private ISkillController skillController;
    private IAttacker attacker;
    private IStatHolder statHolder;
    private int highlightedSlotIndex = -1;
    private int hoveredSlotIndex = -1;
    private readonly List<SkillTypeSO> displayedSkills = new();
    private bool hasDisplaySkillSnapshot;

    private void Awake()
    {
        if (panel == null)
            TryGetComponent(out panel);
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
        RedrawAllSlots();
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
            // if (c.TryGetComponent(out ISkillController foundSkillController))
            //     skillController = foundSkillController;
            // if (c.TryGetComponent(out IAttacker foundAttacker))
            //     attacker = foundAttacker;
            // if (c.TryGetComponent(out IStatHolder foundStatHolder))
            //     statHolder = foundStatHolder;

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
            skillController.OnActiveSkillChanged += HandleActiveSkillChanged;
            skillController.OnResolvedSkillChanged += HandleResolvedSkillChanged;
            skillController.OnComboStepChanged += HandleComboStepChanged;
            skillController.OnSkillReady += HandleSkillReady;
        }

        RedrawAllSlots();
    }

    private void UnbindSkillController()
    {
        if (skillController != null)
        {
            skillController.OnSkillBookChanged -= HandleSkillBookChanged;
            skillController.OnActiveSkillChanged -= HandleActiveSkillChanged;
            skillController.OnResolvedSkillChanged -= HandleResolvedSkillChanged;
            skillController.OnComboStepChanged -= HandleComboStepChanged;
            skillController.OnSkillReady -= HandleSkillReady;
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

    private void RedrawAllSlots()
    {
        if (panel == null)
            return;

        if (skillController == null)
        {
            ClearPanel();
            return;
        }

        var skills = skillController.RegisteredSkills;
        int slotCount = panel.SlotCount;
        bool allowModifiedHighlight = hasDisplaySkillSnapshot;

        for (int i = 0; i < slotCount; i++)
        {
            SkillTypeSO baseSkill = i < skills.Count ? skills[i] : null;
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

        var skills = skillController.RegisteredSkills;
        int count = Mathf.Min(panel.SlotCount, skills.Count);

        for (int i = 0; i < count; i++)
        {
            SkillTypeSO skill = skills[i];
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

        var skills = skillController.RegisteredSkills;
        int count = Mathf.Min(panel.SlotCount, skills.Count);

        for (int i = 0; i < count; i++)
        {
            if (skills[i] == skill)
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

        var skills = skillController.RegisteredSkills;
        if (slotIndex < 0 || slotIndex >= skills.Count || slotIndex >= panel.SlotCount)
            return false;

        SkillTypeSO baseSkill = skills[slotIndex];
        if (baseSkill == null)
            return false;

        displaySkill = ResolveDisplaySkill(baseSkill);
        return displaySkill != null;
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
        hasDisplaySkillSnapshot = false;
    }
}
