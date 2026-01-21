using System;
using System.Collections.Generic;
using TH.Item;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.EventSystems;

public class QuickSlotPanelUI : BaseUI, IStorageUI<QuickSlotUI>, IDraggableStorageUI, IClickableStorageUI, IHoverableStorageUI, IHighlightableStorageUI,
    IPointerMoveHandler, IPointerExitHandler, IBeginDragHandler, IEndDragHandler, IDragHandler, IDropHandler, IPointerDownHandler, IPointerUpHandler
{
    #region Enums

    enum GameObjects
    {
        slots,
    }

    #endregion

    [SerializeField] private readonly List<QuickSlotUI> slotUIs = new(); // serialize for debug
    private IReadOnlyCollection<QuickSlotUI> readonlySlots;

    // IDraggableStorageUI, etc ... User Input events
    public event Action<int> OnSlotDragged;
    public event Action<int> OffSlotDragged;
    public event Action<int> OnSlotClicked;
    public event Action<int> OnSlotHovered;
    public event Action<int> OffSlotHovered;

    // IStorageUI


    System.Collections.IEnumerable IStorageUI.Slots => Slots;
    public IReadOnlyCollection<QuickSlotUI> Slots 
    { 
        get { readonlySlots ??= slotUIs.AsReadOnly(); return readonlySlots; } 
    }
    public IReadOnlyCollection<QuickSlotUI> SlotUIs => Slots;


    protected override void Awake() 
    {
        base.Awake();
        BindObject(typeof(GameObjects));

        if (slotUIs.Count > 0) return;
        if (GetObject((int)GameObjects.slots) is {} slots && slots.IsNotNull())
        {
            int i = 0;
            foreach (var slotUI in slots.GetComponentsInChildren<QuickSlotUI>())
            {
                slotUI.SetIndex(i);
                slotUIs.Add(slotUI);
                i++;
            }
        }
    }

    public QuickSlotUI GetSlotUI(int index)
    {
        if (!IsValidSlotIdx(index)) return null;
        return slotUIs[index];
    }

    public void DrawSlot(int index, IGameItem instance)
    {
        if (!IsValidSlotIdx(index)) return;
        if (GetSlotUI(index) is not {} slotUI) return;
        
        if (instance is not { IsValid: true, GetItemInfo: {} itemInfo })
        {
            slotUI.HideIcon();
            return;
        }
        
        slotUI.SetIcon(itemInfo.sprite);
    }

    public void SetSlotKeyText(int index, string text)
    {
        if (!IsValidSlotIdx(index)) return;
        if (GetSlotUI(index) is {} slotUI)
            slotUI.SetKeyText(text);
    }

    public void CleanSlot(int index)
    {
        if (!IsValidSlotIdx(index)) return;
        if (GetSlotUI(index) is {} slotUI)
            slotUI.HideIcon();
    }

    public void ShowSlot(int index)
    {
        if (!IsValidSlotIdx(index)) return;
        if (GetSlotUI(index) is {} slotUI)
            slotUI.gameObject.SetActive(true);
    }

    public void HideSlot(int index)
    {
        if (!IsValidSlotIdx(index)) return;
        if (GetSlotUI(index) is {} slotUI)
            slotUI.gameObject.SetActive(false);
    }

    private bool IsValidSlotIdx(int index)
    {
        return index >= 0 && index < slotUIs.Count;
    }

    private ISlotUI lastHoveredSlot;
    public void OnPointerMove(PointerEventData eventData)
    {
        Logg.Log($"[{GetType().Name}] OnPointerMove invoked", Logg.LoggingMode.Completed);

        switch (eventData.pointerEnter)
        {
            // 새로운 슬롯에 포인터가 이동한 경우
            case {} target when target.TryGetComponent(out ISlotUI slotUI) && slotUI != lastHoveredSlot:
                if (lastHoveredSlot is {Index: {} lastHoveredIndex}) OffSlotHovered?.Invoke(lastHoveredIndex);
                lastHoveredSlot = slotUI;
                Logg.Log($"[{GetType().Name}] OnPointerMove '{eventData.pointerEnter}'", Logg.LoggingMode.Completed);

                OnSlotHovered?.Invoke(slotUI.Index);
                break;
            
            // 슬롯이 없는 빈 공간으로 이동했는데 기존에 머무르던 슬롯이 있는 경우
            case null when lastHoveredSlot != null:
                OffSlotHovered?.Invoke(lastHoveredSlot.Index);
                lastHoveredSlot = null;
                break;
            
            default: // 기존 슬롯에서 머무르고 있거나 기존에 머무르던 슬롯이 없고 빈공간에 포인터가 위치해있는 경우
                break;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (lastHoveredSlot == null) return;
            
        OffSlotHovered?.Invoke(lastHoveredSlot.Index);
        lastHoveredSlot = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Logg.Log($"[{GetType().Name}] OnPointerClick '{eventData.pointerEnter}'", Logg.LoggingMode.Completed);
        if (eventData.dragging)
        {
            Logg.Log($"[{GetType().Name}] OnPointerClick '{eventData.pointerEnter}' " +
                        $"canceled because dragging is true", Logg.LoggingMode.Completed);
            return;
        }
        if (eventData.pointerEnter is { } target &&
            target.TryGetComponent(out ISlotUI slotUI))
        {
            // switch (eventData.button)
            // {
            //     case PointerEventData.InputButton.Left when eventData.clickCount > 2:
            //     case PointerEventData.InputButton.Right:
            //         OnSlotSubClicked?.Invoke(slotUI.Index);
            //         break;
            //     default:
            //         OnSlotClicked?.Invoke(slotUI.Index);
            //         break;
            // }
            Logg.Log($"[{GetType().Name}] OnPointerClick({slotUI.Index})", Logg.LoggingMode.Completed);
            OnSlotClicked?.Invoke(slotUI.Index);
        } 
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Logg.Log($"[{GetType().Name}] OnBeginDrag invoked '{eventData.pointerDrag}'", Logg.LoggingMode.Completed);

        if (eventData.pointerEnter is { } target
            && target.TryGetComponent(out ISlotUI slotUI))
        {
            Logg.Log($"[{GetType().Name}] OnBeginDrag trying to call OnSlotDragged.Invoke({slotUI})", 
                Logg.LoggingMode.Completed);
            OnSlotDragged?.Invoke(slotUI.Index);
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        OnDrop(eventData);
    }

    public void OnDrag(PointerEventData eventData) {}

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerEnter is { } target && target.TryGetComponent(out ISlotUI slotUI))
            OffSlotDragged?.Invoke(slotUI.Index);
        else OffSlotDragged?.Invoke(-1); // -1 means drop failed
    }

    private GameObject lastPointerDown;
    public void OnPointerDown(PointerEventData eventData)
    {
        lastPointerDown = eventData.pointerEnter;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (lastPointerDown != eventData.pointerEnter) return;
        
        OnPointerClick(eventData);
    }

    #region IHighlightableStorageUI
    public void HighlightSlot(int index)
    {
        if (!IsValidSlotIdx(index)) return;
        slotUIs[index].Highlight();
    }

    public void HighlightSlot(int index, int highlightType)
    {
        if (!IsValidSlotIdx(index)) return;
        slotUIs[index].Highlight(highlightType);
    }

    public void HighlightEquippingSlot(int index)
    {
        if (!IsValidSlotIdx(index)) return;
        slotUIs[index].HighlightEquipping();
    }

    public void UnHighlightSlot(int index)
    {
        if (!IsValidSlotIdx(index)) return;
        slotUIs[index].UnHighlight();
    }

    public void UnHighlightSlot(int index, int highlightType)
    {
        if (!IsValidSlotIdx(index)) return;
        slotUIs[index].UnHighlight(highlightType);
    }

    public void UnHighlightEquippingSlot(int index)
    {
        if (!IsValidSlotIdx(index)) return;
        slotUIs[index].UnHighlightEquipping();
    }

    public void UnHighlightSlotWithFade(int index, int highlightType, float duration = 0.5f)
    {
        if (!IsValidSlotIdx(index)) return;
        slotUIs[index].UnHighlightWithFade(highlightType, duration);
    }

    #endregion

}
