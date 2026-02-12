using System.Collections;
using System.Collections.Generic;
using TH.Combat;
using TH.Core;
using TH.Core.Service;
using TH.Item;
using TH.Resource;
using TH.UI;
using UnityEngine;
using TH.Attribute;

[DisallowMultipleComponent]
public sealed class ActiveSkillSlotController : MonoBehaviour
{
    private const string SkillTooltipPrefabKey = "UI_ItemTooltip.prefab";

    [SerializeField] private ActiveSkillSlotPanel panel;

    private readonly Dictionary<SkillTypeSO, GameItem> tooltipItems = new();
    private readonly List<ItemTypeSO> tooltipItemInfos = new();

    private IPlayerHolder playerHolder;
    private ISkillController skillController;
    private IAttacker attacker;
    private int highlightedSlotIndex = -1;
    private int hoveredSlotIndex = -1;

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

    private void OnDestroy()
    {
        DestroyTooltipItems();
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
            if (c.TryGetComponent(out ISkillController foundSkillController))
                skillController = foundSkillController;
            if (c.TryGetComponent(out IAttacker foundAttacker))
                attacker = foundAttacker;
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

        for (int i = 0; i < slotCount; i++)
        {
            SkillTypeSO baseSkill = i < skills.Count ? skills[i] : null;
            if (baseSkill == null)
            {
                panel.ClearSlot(i);
                continue;
            }

            panel.DrawSkill(i, ResolveDisplaySkill(baseSkill));
            DrawCooldown(i, baseSkill);
        }

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
                panel.ClearSlot(i);
                continue;
            }

            DrawCooldown(i, skill);
        }
    }

    private void DrawCooldown(int slotIndex, SkillTypeSO skill)
    {
        if (panel == null || skillController == null || skill == null)
            return;

        float remainingCooldown = skillController.GetRemainingCooldown(skill);
        panel.DrawCooldown(slotIndex, remainingCooldown, skill.Cooldown);
    }

    private void DrawCooldownForSkill(SkillTypeSO skill)
    {
        int slotIndex = FindSkillSlotIndex(skill);
        if (slotIndex < 0)
            return;

        DrawCooldown(slotIndex, skill);
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

    private void RefreshResolvedSkillPresentation()
    {
        if (panel == null || skillController == null || !skillController.HasActiveSkill)
            return;

        int activeSlotIndex = FindSkillSlotIndex(skillController.ActiveSkill);
        if (activeSlotIndex < 0)
            return;

        panel.DrawSkill(activeSlotIndex, ResolveDisplaySkill(skillController.ActiveSkill));
        DrawCooldown(activeSlotIndex, skillController.ActiveSkill);
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

        var tooltipItem = GetOrCreateTooltipItem(displaySkill);
        if (tooltipItem == null)
        {
            HideSkillTooltip();
            return;
        }

        Vector2 pointerPos = InputManager.Instance != null
            ? InputManager.Instance.PointerPos
            : (Vector2)Input.mousePosition;

        UIManager.Instance
            .ShowUI<ItemTooltipUI>(SkillTooltipPrefabKey, UICanvas.FeedbackOverlay)
            ?.ShowTooltipAt(pointerPos, tooltipItem);
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

    private GameItem GetOrCreateTooltipItem(SkillTypeSO skill)
    {
        if (skill == null)
            return null;

        if (tooltipItems.TryGetValue(skill, out var existingItem) && existingItem != null)
            return existingItem;

        var tooltipItemInfo = ScriptableObject.CreateInstance<ItemTypeSO>();
        tooltipItemInfo.itemType = Enums.ItemType.Default;
        tooltipItemInfo.maxAmount = 1;
        tooltipItemInfo.itemUseEffects = new List<ItemEffectBase>();
        tooltipItemInfo.nameString = skill.SkillId;
        tooltipItemInfo.sprite = skill.SkillSlotImage;
        tooltipItemInfo.desc = BuildSkillTooltipDescription(skill);

        var tooltipItem = new GameItem(tooltipItemInfo);
        tooltipItems[skill] = tooltipItem;
        tooltipItemInfos.Add(tooltipItemInfo);
        return tooltipItem;
    }

    private static string BuildSkillTooltipDescription(SkillTypeSO skill)
    {
        return $"Range: {skill.Range:0.##}\nCooldown: {skill.Cooldown:0.##}s\nHit Count: {skill.HitCount}\nAttack Coef: {skill.AttackCoefficient:0.##}";
    }

    private void HideSkillTooltip()
    {
        UIManager.Instance.ReleaseUI(SkillTooltipPrefabKey);
    }

    private void DestroyTooltipItems()
    {
        tooltipItems.Clear();

        for (int i = 0; i < tooltipItemInfos.Count; i++)
        {
            if (tooltipItemInfos[i] != null)
                Destroy(tooltipItemInfos[i]);
        }

        tooltipItemInfos.Clear();
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
    }
}
