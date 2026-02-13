using System;
using System.Collections.Generic;
using TH.Resource;
using TH.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ActiveSkillSlotPanel : BaseUI, IHoverableStorageUI, IPointerMoveHandler, IPointerExitHandler
{
    private enum GameObjects
    {
        slots,
    }

    [SerializeField] private readonly List<ActiveSkillSlotUI> slotUIs = new();
    private IReadOnlyList<ActiveSkillSlotUI> readonlySlotUIs;

    private ISlotUI lastHoveredSlot;

    public event Action<int> OnSlotHovered;
    public event Action<int> OffSlotHovered;

    public IReadOnlyList<ActiveSkillSlotUI> SlotUIs
    {
        get
        {
            readonlySlotUIs ??= slotUIs.AsReadOnly();
            return readonlySlotUIs;
        }
    }

    public int SlotCount => slotUIs.Count;

    protected override void Awake()
    {
        base.Awake();
        BindObject(typeof(GameObjects));
        CollectSlotUIs();
    }

    public ActiveSkillSlotUI GetSlotUI(int index)
    {
        if (!IsValidSlotIndex(index))
            return null;

        return slotUIs[index];
    }

    public void DrawSkill(int index, SkillTypeSO skill)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].SetSkill(skill);
    }

    public void DrawCooldown(int index, float remainingCooldown, float totalCooldown)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].SetCooldown(remainingCooldown, totalCooldown);
    }

    public void DrawSequenceTimeout(int index, float remainingTimeout, float totalTimeout)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].SetSequenceTimeout(remainingTimeout, totalTimeout);
    }


    public void SetSlotKeyText(int index, string keyText)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].SetSlotKeyText(keyText);
    }

    public void HighlightSlot(int index)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].Highlight();
    }

    public void HighlightSlot(int index, int highlightType)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].Highlight(highlightType);
    }


    public void UnHighlightSlot(int index)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].UnHighlight();
    }

    public void UnHighlightSlotWithFade(int index, int highlightType, float duration = 0.5f)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].UnHighlightWithFade(highlightType, duration);
    }


    public void ClearSlot(int index)
    {
        if (!IsValidSlotIndex(index))
            return;

        slotUIs[index].Clear();
    }

    public void ClearAllSlots()
    {
        for (int i = 0; i < slotUIs.Count; i++)
        {
            slotUIs[i].Clear();
        }
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        switch (eventData.pointerEnter)
        {
            case { } target when target.TryGetComponent(out ISlotUI slotUI) && slotUI != lastHoveredSlot:
                if (lastHoveredSlot is { Index: { } lastHoveredIndex })
                    OffSlotHovered?.Invoke(lastHoveredIndex);

                lastHoveredSlot = slotUI;
                OnSlotHovered?.Invoke(slotUI.Index);
                break;

            case null when lastHoveredSlot != null:
                OffSlotHovered?.Invoke(lastHoveredSlot.Index);
                lastHoveredSlot = null;
                break;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (lastHoveredSlot == null)
            return;

        OffSlotHovered?.Invoke(lastHoveredSlot.Index);
        lastHoveredSlot = null;
    }

    private void CollectSlotUIs()
    {
        if (slotUIs.Count > 0)
        {
            for (int i = 0; i < slotUIs.Count; i++)
            {
                if (slotUIs[i] != null)
                    slotUIs[i].SetIndex(i);
            }

            return;
        }

        if (GetObject((int)GameObjects.slots) is not { } slotsRoot)
            return;

        int slotIndex = 0;
        foreach (var slotUI in slotsRoot.GetComponentsInChildren<ActiveSkillSlotUI>(true))
        {
            slotUI.SetIndex(slotIndex++);
            slotUIs.Add(slotUI);
        }
    }

    private bool IsValidSlotIndex(int index)
    {
        return index >= 0 && index < slotUIs.Count;
    }
}

