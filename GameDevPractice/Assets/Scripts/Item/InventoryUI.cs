using System;
using System.Collections.Generic;
using GameDevTV.Utils;
using RPG.Item;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace RPG.UI
{
    public class InventoryUI : PopupUI
    {
        #region Enums

        enum GameObjects
        {
            Contents,
            
            WeaponSlot, // 반드시 Enums.EquippedSlotType과 순서, 개수가 동일해야함
            HeadSlot,
            BodySlot,
            HandSlot,
            FootSlot,
        
            ItemArea,
            ItemSlots,
            
            PopupPanel,
            UI_RemoveConfirmPopup,
            UI_ItemTooltip,
        }

        #endregion
        
        private RPG.Item.InventorySystem inventorySystem; 
        private GraphicRaycaster graphicRaycaster;
        private PointerEventData pointerEventData;
        private List<RaycastResult> raycastResults;

        private GameObject itemSlotUIPrefab;
        private ObjectPool<ItemSlotUI> slotUIPool;
        
        private UI_ItemTooltip itemTooltip;
        private ScrollRect scroll;

        [SerializeField] private List<ItemSlotUI> itemSlotUIs;
        [SerializeField] private UI_EquipmentSlot[] equipmentSlotUIs;
        
        // hover
        private ItemSlotBaseUI mouseOverSlot;
        
        // drag
        private bool isDragging = false;
        private ItemSlotBaseUI beginDragSlot;
        private Transform beginDragIconTransform;
        
        //highlight
        private int highlightedEquipmentSlotIdx = - 1; // 장비 아이템 드래그앤드랍 시 타입에 맞는 슬롯 강조. -1 means not initialized or not used
        private ItemSlotBaseUI highlightedEquipmentSlot; // 아이템 드래그 시 강조된 장비 슬롯
        
        private Vector3 currCursorPoint;
        private Vector3 beginDragIconPoint;
        private Vector3 beginDragCursorPoint;

        public void TestMethod()
        {
            inventorySystem.CompressInven();
        }
        
        private void Awake()
        {
            raycastResults = new List<RaycastResult>();
            pointerEventData = new PointerEventData(EventSystem.current);
            
            inventorySystem = FindFirstObjectByType<RPG.Item.InventorySystem>();
            if (inventorySystem == null)
            {
                Util.Log($"[{typeof(InventoryUI)}]Failed to Get InventorySystem");
                return;
            }
            
            Init();
        }

        private void OnEnable()
        {
            SubscribeInputEvents();
            OnInventoryCapacityChanged(inventorySystem.Capacity);
            inventorySystem.OnInventoryChanged += this.UpdateAllSlotUI;
            inventorySystem.OnCapacityChanged += this.OnInventoryCapacityChanged;
        }

        private void OnDisable()
        {
            DeSubscribeInputEvents();
            inventorySystem.OnCapacityChanged -= this.OnInventoryCapacityChanged;
            inventorySystem.OnInventoryChanged -= this.UpdateAllSlotUI;
            Refresh();
        }

        private void OnDestroy()
        {
            DisConnectDataWithSlotUIs();
        }

        private void Update()
        {
            MoveIconImageForDragDrop();
        }

        #region Initialization

        public override bool Init()
        {
            if (base.Init() == false)
                return false;
            
            BindObject(typeof(GameObjects));
            
            graphicRaycaster = GetObject((int)GameObjects.Contents).GetOrAddComponent<GraphicRaycaster>();
            scroll = GetObject((int)GameObjects.ItemArea).GetComponent<ScrollRect>();
            
            InitializeSlotUIs();
            ConnectDataWithSlotUIs();
            
            return true;
        }

        private void InitializeSlotUIs()
        {
            int slotNum = itemSlotUIs.Count;
            int slotCap = inventorySystem.Capacity;

            // 아이템 슬롯 UI 오브젝트 풀 생성
            if (ResourceManager.Instance.Load<GameObject>("ItemSlotUI.prefab") is { } loadedSlotUI)
            {
                itemSlotUIPrefab = loadedSlotUI;
                slotUIPool = PoolingManager.Instance.GetPool<ItemSlotUI>(
                    itemSlotUIPrefab,
                    GetObject((int)GameObjects.ItemSlots).transform,
                    capacity: inventorySystem.Capacity,
                    maxSize: inventorySystem.MaxCapacity,
                    registerPool: false);
            }
            
            // 인벤토리 슬롯 UI 초기화
            for (int i = 0; i < slotNum; i++)
            {
                itemSlotUIs[i].Init();
                itemSlotUIs[i].SetSlotIndex(i);
                itemSlotUIs[i].SetSlotAccessibleState(i < slotCap);
                itemSlotUIs[i].SetItemAccessibleState(i < slotCap);
            }

            // 아이템 툴팁 UI 로드
            if (ResourceManager.Instance.Instantiate("UI_ItemTooltip.prefab", transform) is { } tooltipObj)
            {
                itemTooltip = tooltipObj.GetComponent<UI_ItemTooltip>();
                itemTooltip.HideTooltip();
            }
            
            // 장비 슬롯 UI 초기화
            for (int i = 0; i < (int)Enums.EquippedItemSlotType.Max; i++)
            {
                int idx = i + (int)GameObjects.WeaponSlot;
                if (GetObject(idx).GetOrAddComponent<UI_EquipmentSlot>() is { } equipmentSlotUI)
                {
                    equipmentSlotUI.Init();
                    equipmentSlotUI.SetSlotIndex(i);
                    equipmentSlotUI.SetSlotAccessibleState(true);
                    equipmentSlotUIs[i] = equipmentSlotUI;
                }
            }
        }

        private void ConnectDataWithSlotUIs()
        {
            inventorySystem.OnInventorySlotChanged += UpdateSlotUI;
            inventorySystem.OnEquippedSlotChanged += UpdateEquippedSlotUI;

            for (int i = 0; i < itemSlotUIs.Count; i++)
            {
                UpdateSlotUI(i);
            }
        }

        private void DisConnectDataWithSlotUIs()
        {
            // if (Util.IsQuitting) return;
            
            inventorySystem.OnInventorySlotChanged -= UpdateSlotUI;
            inventorySystem.OnEquippedSlotChanged -= UpdateEquippedSlotUI;
        }

        public InventoryUI InitImmediately()
        {
            Init();
            return this;
        }

        #endregion

        #region Validation

        private bool IsValidInventoryIndex(int index) // 유효한 인벤토리 슬롯인지 검사
        {
            return !(index < 0 || index >= itemSlotUIs.Count);
        }

        private bool IsValidEquipIndex(int index) // 유효한 장비 슬롯인지 검사
        {
            return !(index < 0 || index >= equipmentSlotUIs.Length);
        }

        private bool IsActiveSlotUI(int index) // 활성화된 인벤토리 슬롯인지 검사
        {
            return (IsValidInventoryIndex(index) && itemSlotUIs[index] is { isActiveAndEnabled: true });
        }

        #endregion
        
        #region Update Slot UI

        private void UpdateSlotUI(int index)
        {
            var itemSlot = inventorySystem.GetInventorySlot(index);
            if (itemSlot is not { HasItem: true })
            {
                CleanSlot(index);
                return;
            }
            
            SetInventorySlotIcon(index, itemSlot);

            if (itemSlot.GetItem is RPG.Item.CountableItem cItem) // 1-1. 셀 수 있는 아이템 
            {
                Util.Log($"[UpdateSlotUI(index: {index})] item is countableItem", Util.LoggingMode.Completed);
                if (cItem.IsEmpty)
                {
                    Util.Log($"[UpdateSlotUI(index: {index})] cItem is empty", Util.LoggingMode.Completed);
                    CleanSlot(index);
                }
                else
                {
                    Util.Log($"[UpdateSlotUI(index: {index})] Trying to SetSlotAmountText amount: {cItem.GetAmount}", Util.LoggingMode.Completed);
                    SetSlotAmountText(index, cItem.GetAmount);
                    ShowSlotAmountText(index);
                }
            }
            else // 1-2. 셀 수 없는 아이템: 수량 텍스트 제거 
            {
                HideSlotAmountText(index);
            }
        }

        private void UpdateAllSlotUI()
        {
            for (int i = 0; i < itemSlotUIs.Count; i++)
            {
                UpdateSlotUI(i);
            }
        }

        private void UpdateEquippedSlotUI(int index)
        {
            var equippedItem = inventorySystem.GetEquippedSlot(index);
            if (equippedItem is null or { HasItem: false } )
            {
                equipmentSlotUIs[index].RemoveIcon(); // todo: 함수로 래핑
                return;
            }
            
            SetEquipmentSlotIcon(index, equippedItem);
        }

        private void OnInventoryCapacityChanged(int capa)
        {
            int currCount = itemSlotUIs.Count; // case: 인벤토리 칸 감소
            if (currCount == capa) return;
            if (currCount > capa)
            {
                for (int i = capa - 1; i < currCount; i++)
                {
                    DisableSlotUI(i);
                } 
            }
            else // case: itemSlotUIs.Count < capa = 인벤토리 칸 증가
            {
                for (int i = currCount - 1; i < capa; i++)
                {
                    EnableSlotUI(i);
                } 
            }
        }

        private void DisableSlotUI(int index)
        {
            if (!IsValidInventoryIndex(index)) return;
            if (itemSlotUIs[index] is not { isActiveAndEnabled: true } slotUI) return;
            
            slotUI.SetSlotAccessibleState(false);
            slotUI.gameObject.SetActive(false);
        }

        private void EnableSlotUI(int index)
        {
            if (index < 0) return;

            // 리스트에 새 슬롯을 추가하거나 기존 슬롯 재활성화
            ItemSlotUI slotUI = itemSlotUIs.Count <= index ? AddItemSlotUI() : itemSlotUIs[index];
            slotUI.SetSlotAccessibleState(true);
            slotUI.gameObject.SetActive(true);
        }

        #endregion

        #region Subscribe/DeSubscribe Input Event

        private void SubscribeInputEvents()
        {
            DeSubscribeInputEvents(); // 중복 델리게이트 등록 방지
            
            InputManager.Instance.OnUIPointerMoved += OnPointerMove;

            InputManager.Instance.OnDragStarted += OnDrag;
            InputManager.Instance.OnDragEnded += OffDrag;

            InputManager.Instance.OnDoubleClicked += TryUseItem;
            InputManager.Instance.OnAltClicked += TryUseItem;
        }

        private void DeSubscribeInputEvents()
        {
            if (Util.IsQuitting) return; // 어플리케이션 종료 중이라면 취소
            
            InputManager.Instance.OnUIPointerMoved -= OnPointerMove;
            
            InputManager.Instance.OnDragStarted -= OnDrag;
            InputManager.Instance.OnDragEnded -= OffDrag;
            
            InputManager.Instance.OnDoubleClicked -= TryUseItem;
            InputManager.Instance.OnAltClicked -= TryUseItem;
        }

        #endregion
        
        #region Handle User Input
        
        private void OnPointerMove(Vector2 pos)
        {
            pointerEventData.position = pos;
            currCursorPoint = pos;

            var prevSlot = mouseOverSlot;
            mouseOverSlot = RaycastAndGetFirstComponent<ItemSlotBaseUI>();

            if (prevSlot == mouseOverSlot) return; // 포인터가 위치한 슬롯이 이전과 동일하다면 중지
        
            ShowSlotInfo(prevSlot);
        }
        
        private void OnDrag(Vector2 pos)
        {
            beginDragSlot = RaycastAndGetFirstComponent<ItemSlotBaseUI>();
        
            if (beginDragSlot is { HasItem: true })
            {
                scroll.vertical = false; // 스크롤링 불가능
                beginDragSlot.IconImage.maskable = false;
                beginDragIconTransform = beginDragSlot.IconRect;
                beginDragIconPoint = beginDragIconTransform.position;
                beginDragCursorPoint = currCursorPoint; 
                
                beginDragIconTransform.SetParent(GetLastSlotTransform.transform, worldPositionStays: true); // 다른 슬롯 UI에 가려지지 않도록 
                isDragging = true;
                HighlightSuitableEquipmentSlot();
            }
            else
                beginDragSlot = null;
        }

        private void OffDrag(Vector2 pos)
        {
            if (beginDragSlot is { HasItem: true } ) // 드래그 종료 시점에서 드래그 시작 지점 슬롯 재검사
            {
                DropItem();
            }
            CancelItemDrag();
        }
        
        private void MoveIconImageForDragDrop() // 드래그 중
        {
            if (!isDragging || beginDragSlot == null) return;

            beginDragIconTransform.position = 
                beginDragIconPoint + (currCursorPoint - beginDragCursorPoint); // _currCursorPoint = Input.mousePosition;
        }
        
        private void DropItem()
        {
            ItemSlotBaseUI endDragSlot = RaycastAndGetFirstComponent<ItemSlotBaseUI>();

            if (endDragSlot is { IsAccessibleSlot: true } && endDragSlot != beginDragSlot)
            {
                TrySwapItems(beginDragSlot, endDragSlot);
            }
            else if (true)
            {
                //todo: 인벤토리 영역 밖이면 아이템 버리기
            }
        }

        private void CancelItemDrag()
        {
            if (!isDragging || beginDragSlot == null) return;

            scroll.vertical = true; // 스크롤링 가능
            beginDragSlot.IconImage.maskable = true;
            beginDragIconTransform.position = beginDragIconPoint;
            beginDragIconTransform.SetParent(beginDragSlot.transform, worldPositionStays: true); // 아이템 아이콘 원래 부모 슬롯에게로 원복
            
            beginDragIconTransform = null;
            beginDragSlot = null;
            isDragging = false;
            UnHighlightEquipmentSlot();
        }
        
        #endregion

        #region Slot UI Interaction (Use, Swap, etc)

        private void TryUseItem(Vector2 pos)
        {
            if (RaycastAndGetFirstComponent<ItemSlotBaseUI>() is { } slotUI)
            {
                inventorySystem.TryUseItem(slotUI);
            }
        }
        
        private void TrySwapItems(ItemSlotBaseUI fromSlotUI, ItemSlotBaseUI toSlotUI)
        {
            Util.Log($"trying to TrySwapItems({fromSlotUI}.{fromSlotUI.Index}, {toSlotUI}.{toSlotUI.Index})", Util.LoggingMode.Completed);
            
            inventorySystem.TrySwapItems(fromSlotUI, toSlotUI);
        }

        #endregion

        #region Add/Remove Slot UI (using Object Pool)

        private ItemSlotUI AddItemSlotUI() // 아이템 슬롯 UI 생성
        {
            if (slotUIPool.Get() is not { } newSlotUI) return null;

            newSlotUI.SetSlotIndex(itemSlotUIs.Count);
            newSlotUI.SetItemAccessibleState(true);
            newSlotUI.SetSlotAccessibleState(true);
            itemSlotUIs.Add(newSlotUI);
            return newSlotUI;
        }

        private void RemoveItemSlotUI(int index) // 아이템 슬롯 UI 제거 (오브젝트 풀에 반환)
        {
            // 해당 인덱스 위치의 슬롯UI가 존재하지 않거나 이미 비활성화된 상태라면 실행x
            if (!IsValidInventoryIndex(index) || itemSlotUIs[index] is not { isActiveAndEnabled: true } slotUI) return;
            
            itemSlotUIs.RemoveAt(index);
            slotUIPool.Release(slotUI); // 풀에 슬롯UI 반환
        }

        #endregion
        
        #region Slot Icon
        
        private void SetInventorySlotIcon(int index, ItemSlot item) => SetSlotIcon(itemSlotUIs[index], item);
        private void SetEquipmentSlotIcon(int index, ItemSlot item) => SetSlotIcon(equipmentSlotUIs[index], item);

        private void SetSlotIcon(ItemSlotBaseUI slotUI, ItemSlot item)
        {
            if (item.GetItemInfo.sprite is { } sprite)
            {
                slotUI.SetIcon(sprite);
            }
        }

        public void CleanSlot(int index)
        {
            // Util.Log($"CleanSlot(index: {index})");
            if (!IsValidInventoryIndex(index)) return;
            
            itemSlotUIs[index].RemoveIcon();
            itemSlotUIs[index].HideText();
            itemSlotUIs[index].HideHighlight();
        }

        #endregion

        #region Slot Text (Amount)

        private void SetSlotAmountText(int index, int amount)
        {
            itemSlotUIs[index].SetItemAmount(amount);
        }
        
        public void ShowSlotAmountText(int index) => itemSlotUIs[index].ShowText();
        public void HideSlotAmountText(int index) => itemSlotUIs[index].HideText();

        #endregion
        
        #region Helper Function
        
        private ItemSlotBaseUI GetLastSlotTransform => itemSlotUIs.FindLast(slot => slot != null);
        private int GetSelectedItemIdx() => RaycastAndGetFirstComponent<ItemSlotUI>().Index;
        
        private T RaycastAndGetFirstComponent<T>() where T : Component
        {
            raycastResults.Clear();
            graphicRaycaster.Raycast(pointerEventData, raycastResults);
        
            if (raycastResults.Count == 0)
                return null;
            // Util.Log(_raycastResults[0]);
            return raycastResults[0].gameObject.GetComponent<T>();
        }

        #endregion

        #region Highlight

        void HighlightSuitableEquipmentSlot()
        {
            UnHighlightEquipmentSlot(); // 이전에 강조된 슬롯이 존재하면 강조 해제
            
            if (inventorySystem.FindUITargetSlot(beginDragSlot) is not { HasItem: true } targetSlot) return;
            if (targetSlot.GetItemInfo is not EquipmentTypeSO equipmentData) return;
            if (!IsValidEquipIndex((int)equipmentData.slotType)) return;
            
            equipmentSlotUIs[(int)equipmentData.slotType].ShowHighlight();
            highlightedEquipmentSlotIdx = (int)equipmentData.slotType;
            
            // BaseItem item = inventorySystem.GetItemInSlot(beginDragSlot);
            // int idx = item switch
            // {
            //     WeaponItem weapon => (int)Enums.EquippedItemSlotType.Weapon,
            //     ArmorItem armor => (int)Enums.EquippedItemSlotType.Head + armor.ArmorType,
            //     _ => -1
            // };
            // if (idx == -1) return;
            //
            // equipmentSlotUIs[idx].ShowHighlight();
            // highlightedEquipmentSlotIdx = idx;
        }
    
        void UnHighlightEquipmentSlot()
        {
            if (highlightedEquipmentSlotIdx == -1)
                return;
            equipmentSlotUIs[highlightedEquipmentSlotIdx].HideHighlight();
            highlightedEquipmentSlotIdx = -1; // -1 means highlighting nothing
        }

        #endregion
        
        #region Tooltip
        
        private void ShowSlotInfo(ItemSlotBaseUI prevSlot)
        {
            switch (mouseOverSlot)
            {
                case UI_EquipmentSlot equipmentSlot when isDragging && !inventorySystem.CanStore(beginDragSlot, mouseOverSlot) :
                    UnHighlightPrevSlot();
                    WarningCurrSlot();
                    if (equipmentSlot.HasItem)
                        ShowCurrTooltip();
                    else
                        HidePrevTooltip();
                    break;
                case { HasItem: false } when isDragging: // 드래그 중 빈 슬롯
                case { HasItem: true } slot when isDragging && slot == beginDragSlot: // 드래그 중 본인 슬롯
                    UnHighlightPrevSlot();
                    HighlightCurrSlot();
                    HidePrevTooltip();
                    break;
                case { HasItem: true } : // 아이템이 있는 슬롯 (+드래그 중인 아이템 자신의 슬롯이 아닌 경우)
                    UnHighlightPrevSlot();
                    HighlightCurrSlot();
                    ShowCurrTooltip();
                    break;
                default: // 빈슬롯, 슬롯x
                    UnHighlightPrevSlot();
                    HidePrevTooltip();
                    break;
            }
            
            void HighlightCurrSlot() => mouseOverSlot.ShowHighlight();
            void WarningCurrSlot() => (mouseOverSlot as UI_EquipmentSlot)?.ShowWarningHighlight();
            void UnHighlightPrevSlot()
            {
                if (prevSlot == null) return;
                prevSlot.HideHighlight();
            }

            void HidePrevTooltip() => itemTooltip.HideTooltip();
            void ShowCurrTooltip()
            {
                itemTooltip.MoveTooltip(currCursorPoint);
                itemTooltip.ShowTooltip(
                    mouseOverSlot switch
                    {
                        ItemSlotUI inventorySlot => inventorySystem.GetInventorySlot(inventorySlot.Index),
                        UI_EquipmentSlot equipmentSlot => inventorySystem.GetEquippedSlot(equipmentSlot.Index),
                        _ => null
                    }
                );
            }
        }

        private void HideTooltip() => itemTooltip.HideTooltip();
        private void HideHighlight()
        {
            if (mouseOverSlot != null)
                mouseOverSlot.HideHighlight();
            if (beginDragSlot != null)
                beginDragSlot.HideHighlight();
        }

        #endregion
        
        private void Refresh()
        {
            Clear();
        
            ItemSlotBaseUI slot = mouseOverSlot;
            mouseOverSlot = null;
            ShowSlotInfo(slot);
        }

        private void Clear()
        {
            CancelItemDrag();
            HideTooltip();
            HideHighlight();
        }
        
        #region Deprecated
        
        // private void ConnectDataWithSlotUIs()
        // {
        //     // int invIdx = 0;
        //     // foreach (var itemSlot in inventorySystem.ReadOnlyInventorySlots)
        //     // {
        //     //     slotDataUIPairs[itemSlot] = itemSlotUIs[invIdx];
        //     //     invIdx++;
        //     // }
        //     //
        //     // int equIdx = 0;
        //     // foreach (var equipSlot in inventorySystem.ReadOnlyEquippedSlots)
        //     // {
        //     //     slotDataUIPairs[equipSlot] = equipmentSlotUIs[equIdx];
        //     //     equIdx++;
        //     // }
        //
        //     inventorySystem.OnInventorySlotChanged += UpdateSlotUI;
        //     inventorySystem.OnEquippedSlotChanged += UpdateEquippedSlotUI;
        //     // inventorySystem.OnItemSlotChanged += UpdateSlotUI;
        //
        //     for (int i = 0; i < itemSlotUIs.Count; i++)
        //     {
        //         UpdateSlotUI(i);
        //     }
        // }

        // private Dictionary<ItemSlot, UI_ItemSlotBase> slotDataUIPairs = new();
        // private void UpdateSlotUI(ItemSlot slot)
        // {
        //     if (!slotDataUIPairs.TryGetValue(slot, out var resultSlotUI)) return; // 연결된 슬롯UI가 없으면 중지
        //     
        //     if (resultSlotUI is UI_EquipmentSlot equipSlotUI && slot is EquipmentSlot equipSlot)
        //     {
        //         UpdateEquippedSlotUI(equipSlotUI.Index, equipSlot);
        //     }
        //     else
        //     {
        //         UpdateSlotUI(resultSlotUI.Index, slot);
        //     }    
        // }
        
        // private void UpdateSlotUI(int index, ItemSlot item)
        // {
        //     if (item is not { GetAmount: > 0 })
        //     {
        //         CleanSlot(index);
        //         return;
        //     }
        //     
        //     SetInventorySlotIcon(index, item);
        //
        //     if (item is CountableItemSlot cItem) // 1-1. 셀 수 있는 아이템 
        //     {
        //         if (cItem.IsEmpty)
        //         {
        //             CleanSlot(index);
        //         }
        //         else
        //         {
        //             SetSlotAmountText(index, cItem.GetAmount);
        //             ShowSlotAmountText(index);
        //         }
        //     }
        //     else // 1-2. 셀 수 없는 아이템: 수량 텍스트 제거 
        //     {
        //         HideSlotAmountText(index);
        //     }
        // }
        //
        // private void UpdateEquippedSlotUI(int index, EquipmentSlot equipSlot)
        // {
        //     if (equipSlot == null)
        //     {
        //         equipmentSlotUIs[index].RemoveIcon(); // todo: 함수로 래핑
        //         return;
        //     }
        //     
        //     SetEquipmentSlotIcon(index, equipSlot);
        // }
        
        // private void SetSlotIcon(int index, ItemSlot itemSlot)
        // {
        //     if (!itemSlotUIs[index].IsAccessibleSlot)
        //     {
        //         Util.Log("InAccessible slot");
        //         return;
        //     }
        //     itemSlotUIs[index].SetIcon(itemSlot.GetItemInfo.sprite);
        // }

        #endregion 
        
    }
}

