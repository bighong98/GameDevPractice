using System;
using System.Collections;
using System.Collections.Generic;
using RPG.Item;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UI_Inventory : PopupUI // 이름에 popup이라고 명시되어있지 않지만, 팝업임
{
    #region Enums

    enum GameObjects
    {
        ContentArea,
        
        WeaponSlot, // 반드시 Enums.EquippedSlotType과 순서, 개수가 동일해야함
        HeadSlot,
        BodySlot,
        HandSlot,
        FootSlot,
        
        SlotArea,
        PopupPanel,
        UI_RemoveConfirmPopup,
        UI_ItemTooltip,
    }

    #endregion

    public InventorySystem _inventorySystem;
    
    [SerializeField]private GraphicRaycaster _graphicRaycaster;
    private PointerEventData _pointerEventData;
    private List<RaycastResult> _raycastResults;
    
    // Hover
    private UI_ItemTooltip _itemTooltip;
    private UI_ItemSlotBase _mouseOverSlot;
    
    // Drag
    private bool _isDragging = false;
    private int _highlightedEquipmentSlotIdx = - 1; // 장비 아이템 드래그앤드랍 시 타입에 맞는 슬롯 강조. -1 means not initialized or not used
    private UI_ItemSlotBase _beginDragSlot;
    private Transform _beginDragIconTransform;
    private UI_ItemSlotBase _highlightedEquipmentSlot; // 아이템 드래그 시 강조된 장비 슬롯

    private Vector3 _currCursorPoint;
    private Vector3 _beginDragIconPoint;
    private Vector3 _beginDragCursorPoint;
    // private InputAction.CallbackContext emptyContext = default; // 리프레쉬용 빈 콜백

    [SerializeField] private List<UI_ItemSlot> _uiItemSlots; // 반드시 인스펙터로 레퍼런스 연결 해주어야함
    [SerializeField] private UI_EquipmentSlot[] _uiEquipmentSlots = new UI_EquipmentSlot[(int)Enums.EquippedItemSlotType.Max]; 
    private UI_ItemSlotBase GetLastSlotTransform => _uiItemSlots.FindLast(slot => slot != null);

    #region Input Action
    
    private InputActionMap _inventoryActionMap;
    private InputAction _useItemAction;
    private InputAction _divideItemAction;

    private InputAction _pointerMoveAction;
    private InputAction _pointerDragAction;

    #endregion
    
    private void Awake()
    {
        InitializeInputAction();
        
        _pointerEventData = new PointerEventData(EventSystem.current);
        _raycastResults = new List<RaycastResult>();
    }
    
    private void OnEnable()
    {
        if (_inventoryActionMap == null)
            return;
        
        // _pointerMoveAction.Enable();
        // _pointerMoveAction.performed -= OnPointerMove;
        // _pointerMoveAction.performed += OnPointerMove;
        //
        // _pointerDragAction.Enable();
        // _pointerDragAction.performed -= OnDrag;
        // _pointerDragAction.performed += OnDrag;
        // _pointerDragAction.canceled -= OffDrag;
        // _pointerDragAction.canceled += OffDrag;
        //
        // _useItemAction.Enable();
        // _useItemAction.performed -= OnItemUsed;
        // _useItemAction.performed += OnItemUsed;
        // _divideItemAction.Enable();
        // _divideItemAction.performed -= OnItemDivided;
        // _divideItemAction.performed += OnItemDivided;

        InputManager.Instance.OnPointerMoved -= OnPointerMove;
        InputManager.Instance.OnPointerMoved += OnPointerMove;
        
        InputManager.Instance.OnDragStarted -= OnDrag;
        InputManager.Instance.OnDragStarted += OnDrag;

        InputManager.Instance.OnDragEnded -= OffDrag;
        InputManager.Instance.OnDragEnded += OffDrag;
    }

    private void Start()
    {
        Init();
    }

    public override bool Init()
    {
        if (base.Init() == false)
            return false;
        
        BindObject(typeof(GameObjects));

        _inventorySystem = FindFirstObjectByType<InventorySystem>();
        _graphicRaycaster = GetObject((int)GameObjects.ContentArea).GetOrAddComponent<GraphicRaycaster>();
        
        int slotNum = _uiItemSlots.Count; 
        int slotCap = _inventorySystem.Capacity;
        for (int i = 0; i < slotNum; i++)
        {
            _uiItemSlots[i].SetSlotIndex(i);
            _uiItemSlots[i].SetSlotAccessibleState(i < slotCap);
            _uiItemSlots[i].SetItemAccessibleState(i < slotCap);
        }
        
        _itemTooltip = GetObject((int)GameObjects.UI_ItemTooltip).GetOrAddComponent<UI_ItemTooltip>();
        _itemTooltip.HideTooltip();

        for (int i = 0; i < (int)Enums.EquippedItemSlotType.Max; i++)
        {
            int idx = i + (int)GameObjects.WeaponSlot;
            UI_EquipmentSlot slot = GetObject(idx).GetOrAddComponent<UI_EquipmentSlot>();
            if (slot != null)
            {
                slot.SetSlotIndex(i);
                slot.SetSlotAccessibleState(true);
                _uiEquipmentSlots[i] = slot;
            }
        }
        
        return true;
    }

    public UI_Inventory InitImmediately()
    {
        Init();
        return this;
    }

    private void Update()
    {
        OnPointerDrag();
    }

    #region Input Event Handle

    private void InitializeInputAction()
    {
        // _inventoryActionMap =
        //     ResourceManager.Instance.Load<InputActionAsset>("UI.inputactions").FindActionMap("Inventory");
        // _useItemAction = _inventoryActionMap?.FindAction("UseItem");
        // _divideItemAction = _inventoryActionMap?.FindAction("DivideItem");
        //
        // _pointerMoveAction = _inventoryActionMap?.FindAction("PointerMove");
        // _pointerDragAction = _inventoryActionMap?.FindAction("Drag");
        
    }

    private void OnPointerMove(Vector2 pos)
    {
        _pointerEventData.position = pos;
        _currCursorPoint = pos;

        var prevSlot = _mouseOverSlot;
        _mouseOverSlot = RaycastAndGetFirstComponent<UI_ItemSlotBase>();
        
        if (prevSlot == _mouseOverSlot) return; // 포인터가 위치한 슬롯이 이전과 동일하다면 중지
        
        ShowSlotInfo(prevSlot);
    }

    private void OnPointerMove(InputAction.CallbackContext context)
    {
        _currCursorPoint = context.ReadValue<Vector2>();
        _pointerEventData.position =_currCursorPoint;

        UI_ItemSlotBase prevSlot = _mouseOverSlot;
        _mouseOverSlot = RaycastAndGetFirstComponent<UI_ItemSlotBase>();

        if (prevSlot == _mouseOverSlot)
            return;
        
        ShowSlotInfo(prevSlot); // Refresh를 위해 분리
    }

    private void ShowSlotInfo(UI_ItemSlotBase prevSlot)
    {
        switch (_mouseOverSlot)
        {
            case UI_EquipmentSlot equipmentSlot when _isDragging && !_inventorySystem.IsValidSlotForEquipment(equipmentSlot, _inventorySystem.GetItemInSlot(_beginDragSlot)) :
                UnHighlightPrevSlot();
                WarningCurrSlot();
                if (equipmentSlot.HasItem)
                    ShowCurrTooltip();
                else
                    HidePrevTooltip();
                break;
            case { HasItem: false } when _isDragging: // 드래그 중 빈 슬롯
            case { HasItem: true } slot when _isDragging && slot == _beginDragSlot: // 드래그 중 본인 슬롯
                UnHighlightPrevSlot();
                HighlightCurrSlot();
                HidePrevTooltip();
                break;
            case { HasItem: true } : // 아이템이 있는 슬롯 (+드래그 중인 아이템 자신의 슬롯이 아닌 경우)
                UnHighlightPrevSlot();
                HighlightCurrSlot();
                ShowCurrTooltip();
                break;
            case null: // 슬롯x
                UnHighlightPrevSlot();
                HidePrevTooltip();
                break;
        }
        
        void HighlightCurrSlot() => _mouseOverSlot.ShowHighlight();
        void WarningCurrSlot() => (_mouseOverSlot as UI_EquipmentSlot)?.ShowWarningHighlight();
        void UnHighlightPrevSlot()
        {
            if (prevSlot == null) return;
            prevSlot.HideHighlight();
        }

        void HidePrevTooltip() => _itemTooltip.HideTooltip();
        void ShowCurrTooltip()
        {
            _itemTooltip.MoveTooltip(_currCursorPoint);
            _itemTooltip.ShowTooltip(
                _mouseOverSlot switch
                {
                    UI_ItemSlot inventorySlot => _inventorySystem.GetItem(inventorySlot.Index),
                    UI_EquipmentSlot equipmentSlot => _inventorySystem.GetEquipment(equipmentSlot.Index),
                    _ => null
                }
            );
        }
    }

    private void OnDrag(Vector2 pos)
    {
        _beginDragSlot = RaycastAndGetFirstComponent<UI_ItemSlotBase>();
        
        if (_beginDragSlot != null && _beginDragSlot.HasItem)
        {
            _beginDragIconTransform = _beginDragSlot.IconRect;
            _beginDragIconPoint = _beginDragIconTransform.position;
            _beginDragCursorPoint = _currCursorPoint; 
                
            _beginDragIconTransform.SetParent(GetLastSlotTransform.transform, worldPositionStays: true); // 다른 슬롯 UI에 가려지지 않도록 
            _isDragging = true;
            HighlightSuitableEquipmentSlot();
        }
        else
            _beginDragSlot = null;
    }

    private void OffDrag(Vector2 pos)
    {
        if (_beginDragSlot != null)
        {
            _beginDragIconTransform.position = _beginDragIconPoint;
            _beginDragIconTransform.SetParent(_beginDragSlot.transform, worldPositionStays: true); // 원래 부모 슬롯에게로 원복
            EndDrag();
            _beginDragSlot = null;
            _beginDragIconTransform = null;
        }

        _isDragging = false;
        UnHighlightEquipmentSlot();
    }

    private void OnDrag(InputAction.CallbackContext context)
    {
        _beginDragSlot = RaycastAndGetFirstComponent<UI_ItemSlotBase>();
        
        if (_beginDragSlot != null && _beginDragSlot.HasItem)
        {
            _beginDragIconTransform = _beginDragSlot.IconRect;
            _beginDragIconPoint = _beginDragIconTransform.position;
            _beginDragCursorPoint = _currCursorPoint; 
                
            _beginDragIconTransform.SetParent(GetLastSlotTransform.transform, worldPositionStays: true); // 다른 슬롯 UI에 가려지지 않도록 
            _isDragging = true;
            HighlightSuitableEquipmentSlot();
        }
        else
            _beginDragSlot = null;
    }
    
    private void OffDrag(InputAction.CallbackContext context)
    {
        if (_beginDragSlot != null)
        {
            _beginDragIconTransform.position = _beginDragIconPoint;
            _beginDragIconTransform.SetParent(_beginDragSlot.transform, worldPositionStays: true); // 원래 부모 슬롯에게로 원복
            EndDrag();
            _beginDragSlot = null;
            _beginDragIconTransform = null;
        }

        _isDragging = false;
        UnHighlightEquipmentSlot();
    }

    private void OnItemUsed(InputAction.CallbackContext context) // 마우스 우클릭 상호작용
    {
        UI_ItemSlotBase selectedSlot = RaycastAndGetFirstComponent<UI_ItemSlotBase>();
        if (selectedSlot == null || !selectedSlot.IsAccessible || !selectedSlot.HasItem)
            return;

        switch (selectedSlot)
        {
            case UI_ItemSlot inventorySlot:
                _inventorySystem.TryUseOrEquipItem(inventorySlot.Index);
                break;
            case UI_EquipmentSlot equipmentSlot:
                _inventorySystem.UnEquipItem(equipmentSlot.Index);
                break;
            default:
                break;
        }
        Refresh();
    }

    private void OnItemDivided(InputAction.CallbackContext context)
    {
        Util.Log("OnItemDivided called");
        UI_ItemSlot selectedSlot = RaycastAndGetFirstComponent<UI_ItemSlot>();
        if (!selectedSlot.IsAccessible || selectedSlot == null)
            return;
        
        _inventorySystem.DivideItem(selectedSlot.Index);
        Refresh();
    }
    
    #endregion

    #region Drag Helper Function
    
    private void OnPointerDrag() // 드래그 중
    {
        if (!_isDragging) return;

        _beginDragIconTransform.position = 
            _beginDragIconPoint + (_currCursorPoint - _beginDragCursorPoint); // _currCursorPoint = Input.mousePosition;
    }

    private void EndDrag()
    {
        UI_ItemSlotBase endDragSlot = RaycastAndGetFirstComponent<UI_ItemSlotBase>();
        
        if (endDragSlot != null && endDragSlot.IsAccessibleSlot)
        {
            TrySwapItems(_beginDragSlot, endDragSlot);
        }
        else if (true)
        {
            //todo: 인벤토리 영역 밖이면 아이템 버리기
        }
    }

    #endregion
    
    private T RaycastAndGetFirstComponent<T>() where T : Component
    {
        _raycastResults.Clear();
        _graphicRaycaster.Raycast(_pointerEventData, _raycastResults);
        
        if (_raycastResults.Count == 0)
            return null;
        // Util.Log(_raycastResults[0]);
        return _raycastResults[0].gameObject.GetComponent<T>();
    }

    private void TrySwapItems(UI_ItemSlotBase from, UI_ItemSlotBase to)
    {
        if (from == to) return; // case from == to (동일한 슬롯에서 멈춘 경우)
        
        if (from.GetType() == to.GetType()) // case from 타입 == to 타입
        {
            switch (from)
            {
                case UI_ItemSlot fromInventory:
                    _inventorySystem.SwapItem(fromInventory.Index, to.Index);
                    break;
                default:
                    break;
            }
        }
        else // case from 타입 != to 타입 (서로 다른 종류의 슬롯인 경우)
        {
            BaseItem fromItem = _inventorySystem.GetItemInSlot(from);
            BaseItem toItem = _inventorySystem.GetItemInSlot(to);

            if (toItem == null) // case 빈 칸에 옮기려는 경우
            {
                if (_inventorySystem.ReplaceItemInSlot(to, fromItem))
                    if (!_inventorySystem.RemoveItem(from))
                        _inventorySystem.ReplaceItemInSlot(from, fromItem);
                return;
            }
            // case 양쪽 칸에 아이템이 있는 경우
            if (!_inventorySystem.ReplaceItemInSlot(to, fromItem))
                return;
            
            if (!_inventorySystem.ReplaceItemInSlot(from, toItem))
                _inventorySystem.ReplaceItemInSlot(to, toItem);
        }
    }
    
    public void CleanSlot(int index)
    {
        _uiItemSlots[index].RemoveIcon();
    }

    public void CleanEquipmentSlot(int index) => _uiEquipmentSlots[index].RemoveIcon();

    public void SetSlotIcon(int index, RPG.Item.ItemSlot itemSlot)
    {
        if (!_uiItemSlots[index].IsAccessibleSlot)
        {
            Util.Log("InAccessible slot");
            return;
        }
        _uiItemSlots[index].SetIcon(itemSlot.GetItemInfo.sprite);
    }
    public void SetSlotIcon(int index, BaseItem item)
    {
        if (_uiItemSlots[index].IsAccessibleSlot == false)
        {
            Util.Log("InAccessible slot");
            return;
        }
        _uiItemSlots[index].SetIcon(ResourceManager.Instance.Load<Sprite>(item.ItemSpriteName));
    }

    public void SetEquipmentSlotIcon(int index, EquipmentItem equipment)
    {
        if (_uiEquipmentSlots[index].IsAccessibleSlot == false)
        {
            Util.Log("UI_Inventory: InAccessible Equipment Slot");
            return;
        }
        _uiEquipmentSlots[index].SetIcon(ResourceManager.Instance.Load<Sprite>(equipment.ItemSpriteName));
    }
    
    public void SetSlotItemAmount(int index, int amount)
    {
        if (_uiItemSlots[index].IsAccessible == false)
            return;
        
        _uiItemSlots[index].SetItemAmount(amount);
    }

    private int GetSelectedItemIdx() => RaycastAndGetFirstComponent<UI_ItemSlot>().Index;
    public void ShowSlotItemAmountText(int index) => _uiItemSlots[index].ShowText();
    public void HideSlotItemAmountText(int index) => _uiItemSlots[index].HideText();

    void HighlightSuitableEquipmentSlot()
    {
        UnHighlightEquipmentSlot(); // 이전에 강조된 슬롯이 존재하면 강조 해제
        
        BaseItem item = _inventorySystem.GetItemInSlot(_beginDragSlot);
        int idx = item switch
        {
            WeaponItem weapon => (int)Enums.EquippedItemSlotType.Weapon,
            ArmorItem armor => (int)Enums.EquippedItemSlotType.Head + armor.ArmorType,
            _ => -1
        };
        if (idx == -1) return;

        _uiEquipmentSlots[idx].ShowHighlight();
        _highlightedEquipmentSlotIdx = idx;
    }
    
    void UnHighlightEquipmentSlot()
    {
        if (_highlightedEquipmentSlotIdx == -1)
            return;
        _uiEquipmentSlots[_highlightedEquipmentSlotIdx].HideHighlight();
        _highlightedEquipmentSlotIdx = -1; // -1 means highlighting nothing
    }
    
    private void Refresh()
    {
        Clear();
        
        UI_ItemSlotBase slot = _mouseOverSlot;
        _mouseOverSlot = null;
        ShowSlotInfo(slot);
    }

    private void Clear()
    {
        HideTooltip();
        HideHighlight();
    }
    
    private void HideTooltip() => _itemTooltip.HideTooltip();
    private void HideHighlight()
    {
        if (_mouseOverSlot != null)
            _mouseOverSlot.HideHighlight();
        if (_beginDragSlot != null)
            _beginDragSlot.HideHighlight();
    }
    
    private void OnDisable()
    {
        Refresh();
        
        // _useItemAction.Disable();
        // _useItemAction.performed -= OnItemUsed;
        // _divideItemAction.Disable();
        // _divideItemAction.performed -= OnItemDivided;
        //
        // _pointerMoveAction.Disable();
        // _pointerMoveAction.performed -= OnPointerMove;
        //
        // _pointerDragAction.Disable();
        // _pointerDragAction.performed -= OnDrag;
        // _pointerDragAction.canceled -= OffDrag;
    }
}
