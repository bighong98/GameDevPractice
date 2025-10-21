using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RPG.UI;
using TH.Core.Service;
using TH.Core.Pool;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TH.Resource;
using UnityEngine.Pool;
using TH.Utils;
using Cysharp.Threading.Tasks;
using DG.Tweening;

namespace TH.Item
{
    public class PlayerInventoryUI : PopupUI
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
        
            InventoryArea,
            ItemSlots,
            DragDropIconHolder,
            
            PopupArea,
        }

        enum Buttons
        {
            ExitButton,
            SortButton,
            CompressButton,
            
            AllFilterButton,
            EquipmentFilterButton,
            ConsumableFilterButton,
            ResourceFilterButton,
        }
        
        #endregion
        
        private IPlayerInventory inventory;
        
        private PointerEventData pointerEventData;
        private List<RaycastResult> raycastResults;
        private RectTransform inventoryUIRect;

        private UI_ItemTooltip itemTooltip;
        private QuestionPopupUI removeConfirmPopup;
        private ScrollRect scroll;

        [SerializeField] private List<ItemSlotUI> slots;
        [SerializeField] private EquipmentSlotUI[] equipmentSlots;
        
        private GameObject itemSlotUIPrefab;
        private ObjectPool<IPoolObject> slotUIPool;
        
        // hover
        private ItemSlotBaseUI mouseOverSlot;
        
        // drag
        private bool isDragging = false;
        private ItemSlotBaseUI beginDragSlot;
        private Transform beginDragIconTransform;
        private Transform dragDropIconHolder; // 드래그 중인 아이템 아이콘 최상단 표시 목적 컨테이너
        
        //highlight
        private int highlightedEquipmentSlotIdx = - 1; // 장비 아이템 드래그앤드랍 시 타입에 맞는 슬롯 강조. -1 means not initialized or not used
        private ItemSlotBaseUI highlightedEquipmentSlot; // 아이템 드래그 시 강조된 장비 슬롯
        
        private Vector3 currCursorPoint;
        private Vector3 beginDragIconPoint;
        private Vector3 beginDragCursorPoint;
        
        protected override void Awake()
        {
            base.Awake();
            
            raycastResults = new List<RaycastResult>();
            pointerEventData = new PointerEventData(EventSystem.current);
            inventory = ServiceLocator.Require<IPlayerInventory>();
            
            Init();
        }
        
        private void OnEnable()
        {
            SubscribeInputEvents();
            OnInventoryCapacityChanged((inventory as IGameItemStorage).Capacity);
            OnFilterChanged(inventory.CurrentFilter);
            ConnectDataWithSlotUIs();
        }

        private void OnDisable()
        {
            DeSubscribeInputEvents();
            DisConnectDataWithSlotUIs();
            Clear();
        }
        
        private void OnDestroy()
        {
            if (Util.IsQuitting) return;
            
            DeSubscribeInputEvents();
            DisConnectDataWithSlotUIs();
            Clear();
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
            BindButton(typeof(Buttons));

            if (GetObject((int)GameObjects.Contents) is { } contentsArea)
            {
                inventoryUIRect = contentsArea.GetComponent<RectTransform>();
            }
            
            scroll = GetObject((int)GameObjects.InventoryArea).GetComponent<ScrollRect>();
            dragDropIconHolder = GetObject((int)GameObjects.DragDropIconHolder).transform;

            
            // 아이템 툴팁 UI 로드
            if (ResourceManager.Instance.Instantiate("UI_ItemTooltip.prefab", transform) is { } tooltipObj)
            {
                itemTooltip = tooltipObj.GetComponent<UI_ItemTooltip>();
                itemTooltip.HideTooltip();
            }
            
            ConnectButtons();
            InitializeSlotUIs();
            FillButtonDict();
            CacheOriginalFilterButtonScales();
            
            // 인벤토리 슬롯UI 전체 초기화
            for (int i = 0; i < slots.Count; i++)
            {
                UpdateSlotUI(i);
            }
            
            return true;
        }

        private void InitializeSlotUIs()
        {
            int slotNum = slots.Count;
            int slotCap = (inventory as IGameItemStorage).Capacity;

            // 아이템 슬롯 UI 오브젝트 풀 생성
            if (ResourceManager.Instance.Load<GameObject>("ItemSlotUI.prefab") is { } loadedSlotUI)
            {
                itemSlotUIPrefab = loadedSlotUI;
                slotUIPool = PoolManager.Instance.GetPool(
                    itemSlotUIPrefab,
                    GetObject((int)GameObjects.ItemSlots).transform,
                    capacity: slotCap,
                    maxSize: inventory.MaxCapacity,
                    registerPool: false);
            }
            
            // 인벤토리 슬롯 UI 초기화
            for (int i = 0; i < slotNum; i++)
            {
                slots[i].Init();
                slots[i].SetSlotIndex(i);

                bool isActive = i < slotCap;
                if (slots[i] is { } slotUI)
                {
                    slotUI.SetSlotAccessibleState(isActive);
                    slotUI.SetItemAccessibleState(isActive);
                    if (!isActive)
                        DisableSlotUI(i);
                }
            }
            
            // 장비 슬롯 UI 초기화
            for (int i = 0; i < (int)Enums.EquippedItemSlotType.Max; i++)
            {
                int idx = i + (int)GameObjects.WeaponSlot;
                if (GetObject(idx).GetOrAddComponent<EquipmentSlotUI>() is { } equipmentSlotUI)
                {
                    equipmentSlotUI.Init();
                    equipmentSlotUI.SetSlotIndex(i);
                    equipmentSlotUI.SetSlotAccessibleState(true);
                    equipmentSlots[i] = equipmentSlotUI;
                }
            }
        }
        
        private void ConnectButtons()
        {
            GetButton((int)Buttons.ExitButton).onClick.AddListener(OnExitButtonPressed);
            GetButton((int)Buttons.SortButton).onClick.AddListener(OnSortButtonPressed);
            GetButton((int)Buttons.CompressButton).onClick.AddListener(OnCompressButtonPressed);
            
            GetButton((int)Buttons.AllFilterButton).onClick.AddListener(() =>
            {
                OnFilterButtonPressed(InventoryFilterType.All);
            });
            GetButton((int)Buttons.EquipmentFilterButton).onClick.AddListener(() =>
            {
                OnFilterButtonPressed(InventoryFilterType.Equipment);
            });
            GetButton((int)Buttons.ConsumableFilterButton).onClick.AddListener(() =>
            {
                OnFilterButtonPressed(InventoryFilterType.Consumable);
            });
            GetButton((int)Buttons.ResourceFilterButton).onClick.AddListener(() =>
            {
                OnFilterButtonPressed(InventoryFilterType.Resource);
            });
        }
        
        private void SubscribeInputEvents()
        {
            DeSubscribeInputEvents(); // 중복 델리게이트 등록 방지
            
            InputManager.Instance.OnUIPointerMoved += OnPointerMove;

            InputManager.Instance.OnDragStarted += OnDrag;
            InputManager.Instance.OnDragEnded += OffDrag;

            InputManager.Instance.OnSingleClicked += TryShowDetailedItemTooltip;
            InputManager.Instance.OnAltClicked += TryUseItem;
            InputManager.Instance.OnAdditived += TryDivideItem;
        }

        private void DeSubscribeInputEvents()
        {
            if (Util.IsQuitting) return; // 어플리케이션 종료 중이라면 취소
            
            InputManager.Instance.OnUIPointerMoved -= OnPointerMove;
            
            InputManager.Instance.OnDragStarted -= OnDrag;
            InputManager.Instance.OnDragEnded -= OffDrag;

            InputManager.Instance.OnSingleClicked -= TryShowDetailedItemTooltip;
            InputManager.Instance.OnAltClicked -= TryUseItem;
            InputManager.Instance.OnAdditived -= TryDivideItem;
        }

        private void ConnectDataWithSlotUIs()
        {
            inventory.OnSlotChanged += OnInventorySlotUpdated;
            // inventory.OnEquippedSlotChanged += OnEquipmentSlotUpdated;

            inventory.OnStorageChanged += this.OnInventoryUpdated;
            inventory.OnCapacityChanged += this.OnInventoryCapacityChanged;
            inventory.OnFilterChanged += this.OnFilterChanged;
        }

        private void DisConnectDataWithSlotUIs()
        {
            if (Util.IsQuitting) return;
            
            inventory.OnSlotChanged -= OnInventorySlotUpdated;
            // inventory.OnEquippedSlotChanged -= OnEquipmentSlotUpdated;
            
            inventory.OnStorageChanged -= this.OnInventoryUpdated;
            inventory.OnCapacityChanged -= this.OnInventoryCapacityChanged;
            inventory.OnFilterChanged -= this.OnFilterChanged;
        }
        #endregion

        
        #region Update Slot UI

        private void UpdateSlotUI(int index) // 인벤토리 슬롯 UI 갱신 (장비슬롯x)
        {
            // if (inventory.GetInventorySlot(index) is not { } itemSlot) return; // 인벤토리 시스템으로부터 슬롯 정보 받아오기
            
            if (!IsValidInventoryIndex(index)) return;
            if (!inventory.TryGetItemSlot(index, out var itemSlot)) return;
            
            if (itemSlot.IsVisible) 
                EnableSlotUI(index);
            else // 슬롯이 비가시처리된 경우 (인벤토리 필터 등)
                DisableSlotUI(index);
            
            if (itemSlot is not { HasItem: true } ) // 슬롯에 아이템이 없는 경우 (빈 슬롯)
            {
                CleanSlot(index);
                return;
            }
            
            SetInventorySlotIcon(index, itemSlot);

            if (itemSlot.GetItem is not ICountableItem cItem) // 1-1. 셀 수 없는 아이템
            {
                HideSlotAmountText(index); // 수량 텍스트 비활성화
                return;
            } 
            
            // 1-2. 셀 수 있는 아이템
            if (cItem.IsEmpty) // 개수가 0인지 확인
            {
                CleanSlot(index);
            }
            else
            {
                SetSlotAmountText(index, cItem.GetAmount);
                ShowSlotAmountText(index);
            }
        }

        private void UpdateAllItemSlotUIs()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                UpdateSlotUI(i);
            }
        }

        private void UpdateEquippedSlotUI(int index) // 장비 슬롯 UI 갱신
        {
            // var equippedItem = inventory.GetEquippedSlot(index);
            // if (equippedItem is null or { HasItem: false } )
            // {
            //     equipmentSlots[index].RemoveIcon(); // todo: 함수로 래핑
            //     return;
            // }
            //
            // SetEquipmentSlotIcon(index, equippedItem);
        }
        
        private void UpdateAllEquippedSlotUI()
        {
            for (int i = 0; i < equipmentSlots.Length; i++)
            {
                UpdateEquippedSlotUI(i);
            }
        }

        private void DisableSlotUI(int index)
        {
            if (!IsValidInventoryIndex(index)) return;
            if (slots[index] is not { gameObject: { activeSelf: true } } slotUI) return;
            
            slotUI.SetSlotAccessibleState(false);
            slotUI.gameObject.SetActive(false);
        }

        private void EnableSlotUI(int index)
        {
            if (!IsValidInventoryIndex(index)) return;
            if (slots[index].gameObject.activeSelf) return; // 이미 활성화되어 있다면 실행x

            // 리스트에 새 슬롯을 추가하거나 기존 슬롯 재활성화
            ItemSlotUI slotUI = slots.Count <= index ? AddItemSlotUI() : slots[index];
            slotUI.SetSlotAccessibleState(true);
            slotUI.gameObject.SetActive(true);
        }

        #endregion


        
        #region Slot Icon
        
        private void SetInventorySlotIcon(int index, IGameItemSlot item) => SetSlotIcon(slots[index], item);
        private void SetEquipmentSlotIcon(int index, IGameItemSlot item) => SetSlotIcon(equipmentSlots[index], item);

        private void SetSlotIcon(ItemSlotBaseUI slotUI, IGameItemSlot item)
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
            
            slots[index].RemoveIcon();
            slots[index].HideText();
            slots[index].HideHighlight();
        }

        #endregion

        
        #region Handle Event
        
        private void OnFilterChanged(InventoryFilterType filter)
        {
            HighlightSelectedFilterButton(filter);
            UpdateAllItemSlotUIs();
            GetButton((int)Buttons.SortButton).interactable = (filter == InventoryFilterType.All);
        }
        
        private void OnInventoryCapacityChanged(int capa)
        {
            int currCount = slots.Count; 
            if (currCount == capa) return;
            if (currCount > capa) // case: 인벤토리 칸 감소
            {
                for (int i = capa; i < currCount; i++)
                {
                    DisableSlotUI(i);
                } 
            }
            else // case: itemSlotUIs.Count < capa : 인벤토리 칸 증가
            {
                for (int i = currCount; i < capa; i++)
                {
                    EnableSlotUI(i);
                } 
            }
        }

        private void OnInventorySlotUpdated(int index)
        {
            UpdateSlotUI(index);
            if (IsValidInventoryIndex(index))
            {
                CancelItemModifyingProgressForChangedSlot(slots[index]);
            }
        }

        private void OnEquipmentSlotUpdated(int index)
        {
            UpdateEquippedSlotUI(index);
            if (IsValidEquipIndex(index))
            {
                CancelItemModifyingProgressForChangedSlot(equipmentSlots[index]);
            }
        }

        private void OnInventoryUpdated()
        {
            UpdateAllItemSlotUIs();
            CancelAllItemModifyingProgress();
        }

        #endregion

        
        #region Validate Slot UI

        private bool IsValidInventoryIndex(int index) // 유효한 인벤토리 슬롯인지 검사
        {
            return index >= 0 && index < slots.Count;
        }

        private bool IsValidEquipIndex(int index) // 유효한 장비 슬롯인지 검사
        {
            return !(index < 0 || index >= equipmentSlots.Length);
        }

        #endregion
        
        #region Slot Text (Amount)

        private void SetSlotAmountText(int index, int amount)
        {
            slots[index].SetItemAmount(amount);
        }
        
        public void ShowSlotAmountText(int index) => slots[index].ShowText();
        public void HideSlotAmountText(int index) => slots[index].HideText();

        #endregion

        #region Add/Remove Slot UI (using Object Pool)

        private ItemSlotUI AddItemSlotUI() // 아이템 슬롯 UI 생성
        {
            if (slotUIPool.Get() is not ItemSlotUI newSlotUI) return null;

            newSlotUI.SetSlotIndex(slots.Count);
            newSlotUI.SetItemAccessibleState(true);
            newSlotUI.SetSlotAccessibleState(true);
            slots.Add(newSlotUI);
            return newSlotUI;
        }

        private void RemoveItemSlotUI(int index) // 아이템 슬롯 UI 제거 (오브젝트 풀에 반환)
        {
            // 해당 인덱스 위치의 슬롯UI가 존재하지 않거나 이미 비활성화된 상태라면 실행x
            if (!IsValidInventoryIndex(index) || slots[index] is not { isActiveAndEnabled: true } slotUI) return;
            
            slots.RemoveAt(index);
            slotUIPool.Release(slotUI); // 풀에 슬롯UI 반환
        }

        #endregion
        
        
        #region UI Interaction (User Input)
        
        private void OnPointerMove(Vector2 pos) // 마우스/터치패드 포인터가 움직인 경우
        {
            pointerEventData.position = pos;
            currCursorPoint = pos;

            var prevSlot = mouseOverSlot;
            mouseOverSlot = RaycastAndGetFirstComponent<ItemSlotBaseUI>();

            if (prevSlot == mouseOverSlot) return; // 포인터가 위치한 슬롯이 이전과 동일하다면 중지
        
            ShowSlotInfo(prevSlot);
        }
        
        private void OnDrag(Vector2 pos) // 드래그 시작 시
        {
            beginDragSlot = RaycastAndGetFirstComponent<ItemSlotBaseUI>();
        
            if (beginDragSlot is { HasItem: true })
            {
                scroll.vertical = false; // 스크롤링 불가능
                beginDragSlot.IconImage.maskable = false;
                beginDragIconTransform = beginDragSlot.IconRect;
                beginDragIconPoint = beginDragIconTransform.position;
                beginDragCursorPoint = currCursorPoint; 
                
                beginDragIconTransform.SetParent(dragDropIconHolder, worldPositionStays: true); // 다른 슬롯 UI에 가려지지 않도록 
                isDragging = true;
                HighlightSuitableEquipmentSlot();
            }
            else
                beginDragSlot = null;
        }

        private void OffDrag(Vector2 pos) // 드래그 종료 시 (=드랍)
        {
            if (isDragging && beginDragSlot is { HasItem: true } ) // 드래그 종료 시점에서 드래그 시작 지점 슬롯 재검사
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
            ItemSlotBaseUI endDragSlot = RaycastAndGetFirstComponent<ItemSlotBaseUI>(); // pointerEventData 기준으로 드래그 종료 시점 포인터 위치의 슬롯UI 반환

            if (endDragSlot is { IsAccessibleSlot: true } && endDragSlot != beginDragSlot)
            {
                TrySwapItems(beginDragSlot, endDragSlot); // 아이템 위치 교환/합치기 시도
            }
            // 아이템 드래그&드랍 위치가 인벤토리 영역 밖인 경우
            else if (!RectTransformUtility.RectangleContainsScreenPoint(inventoryUIRect, pointerEventData.position)) 
            {
                TryDiscardItem(beginDragSlot, true); // 아이템 버리기(삭제) 시도
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
        
        private void TryUseItem(Vector2 pos)
        {
            if (RaycastAndGetFirstComponent<ItemSlotBaseUI>() is { } slotUI)
            {
                TryUseItem(slotUI);
            }
        }

        private void TryUseItem(ItemSlotBaseUI slotUI)
        {
            if (inventory == null) return;
            // inventory.TryUseItem(slotUI);
            inventory.TryUseItem(slotUI.Index);
        }

        private void TryShowDetailedItemTooltip(Vector2 pos)
        {
            if (inventory == null) return;
            if (RaycastAndGetFirstComponent<ItemSlotBaseUI>() is { } slotUI)
            {
                ShowDetailedTooltip(slotUI);
            }
        }
        
        private void TrySwapItems(ItemSlotBaseUI fromSlotUI, ItemSlotBaseUI toSlotUI)
        {
            if (inventory == null) return;
            
            Logg.Log($"trying to TrySwapItems({fromSlotUI}.{fromSlotUI.Index}, {toSlotUI}.{toSlotUI.Index})", Logg.LoggingMode.Completed);
            // inventory.TrySwapItems(fromSlotUI, toSlotUI);
            inventory.TryTransferItem(fromSlotUI.Index, toSlotUI.Index);
        }

        private void TryDiscardItem(ItemSlotBaseUI slotUI, bool confirm)
        {
            if (inventory == null) return;

            if (confirm)
            {
                ShowRemoveConfirmPopup(slotUI);
            }
            else
            {
                // inventory.RemoveItem(slotUI);
                inventory.TryRemoveItem(slotUI.Index);
            }
        }

        private void TryDivideItem(Vector2 pos)
        {
            if (inventory == null) return;
            if (RaycastAndGetFirstComponent<ItemSlotUI>() is { } slotUI)
            {
                Logg.Log($"[PlayerInventoryUI] trying to divideItem from '{slotUI}'");
                // inventory.DivideItem(slotUI);
            }
        }
        
        private void OnExitButtonPressed()
        {
            UIManager.Instance.ClosePopupUI(this);
        }
        
        private void OnCompressButtonPressed()
        {
            if (inventory == null) return;
            // inventory.CompressInven(false);
        }
        
        private void OnSortButtonPressed()
        {
            if (inventory == null) return;
            // inventory.CompressInven(true);
        }
        
        private void OnFilterButtonPressed(InventoryFilterType filter)
        {
            if (inventory == null) return;
            // inventory.TryFilterInven(filter);
        }
        
        #endregion

             
        #region Helper Function
        
        private ItemSlotBaseUI GetLastSlotTransform => slots.FindLast(slot => slot != null);
        private int GetSelectedItemIdx() => RaycastAndGetFirstComponent<ItemSlotUI>().Index;
        
        private T RaycastAndGetFirstComponent<T>() where T : Component
        {
            return raycastHandler.RaycastAndGetFirstUIComponent<T>(pointerEventData, raycastResults);
            // return Util.RaycastAndGetFirstUIComponent<T>(pointerEventData, raycastResults);
            // raycastResults.Clear();
            // // graphicRaycaster.Raycast(pointerEventData, raycastResults);
            // EventSystem.current.RaycastAll(pointerEventData, raycastResults);
            //
            // if (raycastResults.Count == 0)
            //     return null;
            //
            // Util.Log(raycastResults[0], Util.LoggingMode.Completed);
            // return raycastResults[0].gameObject.GetComponent<T>();
        }

        #endregion

        #region Highlight Slot UI

        void HighlightSuitableEquipmentSlot()
        {
            UnHighlightEquipmentSlot(); // 이전에 강조된 슬롯이 존재하면 강조 해제
            
            // if (inventory.FindUITargetSlot(beginDragSlot) is not { HasItem: true } targetSlot) return;
            if (!inventory.TryGetItemSlot(beginDragSlot.Index, out var slot)
                || slot is not { HasItem: true }) return;
            if (slot.GetItemInfo is not EquipmentTypeSO equipmentData) return;
            if (!IsValidEquipIndex((int)equipmentData.slotType)) return;
            
            equipmentSlots[(int)equipmentData.slotType].ShowHighlight();
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
            equipmentSlots[highlightedEquipmentSlotIdx].HideHighlight();
            highlightedEquipmentSlotIdx = -1; // -1 means highlighting nothing
        }

        #endregion
        
        #region Tooltip
        
        private void ShowSlotInfo(ItemSlotBaseUI prevSlot) // 간략한 아이템 툴팁 (PC 전용)
        {
            switch (mouseOverSlot)
            {
                // case EquipmentSlotUI equipmentSlot when isDragging && !inventory.CanStore(beginDragSlot, mouseOverSlot) :
                //     UnHighlightPrevSlot();
                //     WarningCurrSlot();
                //     if (equipmentSlot.HasItem)
                //         ShowCurrTooltip();
                //     else
                //         HidePrevTooltip();
                //     break;
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
            // void WarningCurrSlot() => (mouseOverSlot as EquipmentSlotUI)?.ShowWarningHighlight();
            void UnHighlightPrevSlot()
            {
                if (prevSlot == null) return;
                prevSlot.HideHighlight();
            }

            void HidePrevTooltip() => itemTooltip.HideTooltip();
            void ShowCurrTooltip()
            {
                itemTooltip.MoveTooltip(currCursorPoint);
                if (inventory.TryGetItemSlot(mouseOverSlot.Index, out var slot))
                    itemTooltip.ShowTooltip(slot);
                // itemTooltip.ShowTooltip(
                //     mouseOverSlot switch
                //     {
                //         ItemSlotUI inventorySlot => inventory.GetInventorySlot(inventorySlot.Index),
                //         EquipmentSlotUI equipmentSlot => inventory.GetEquippedSlot(equipmentSlot.Index),
                //         _ => null
                //     }
                // );
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

        #region Popup CTS (CancellationTokenSource) handle 
        
        // 현재 유저가 상호작용 중인 (상세 팝업 호출, 아이템 버리기 팝업 호출 등) 작업 목록 (슬롯UI, CTS) 
        private readonly Dictionary<ItemSlotBaseUI, CancellationTokenSource> progressingSlotAndCTSDictionary = new ();
        
        private void CancelItemModifyingProgressForChangedSlot(ItemSlotBaseUI slotUI)
        {
            if (slotUI == null) return;
            if (progressingSlotAndCTSDictionary.TryGetValue(slotUI, out var cts))
            {
                Util.ClearCTS(cts); // .Cancel and .Dispose
                progressingSlotAndCTSDictionary.Remove(slotUI);
            }
        }

        private void CancelAllItemModifyingProgress()
        {
            if (progressingSlotAndCTSDictionary.Count == 0) return;
            var ctsList = progressingSlotAndCTSDictionary.Values.ToArray();
            foreach (var cts in ctsList)
            {
                Util.ClearCTS(cts); // .Cancel and .Dispose
            }
        }

        private CancellationTokenSource AddNewItemModifyingProgressCTS(ItemSlotBaseUI slotUI)
        {
            Logg.Log($"{nameof(AddNewItemModifyingProgressCTS)}: {slotUI}", Logg.LoggingMode.InProgress);
            if (progressingSlotAndCTSDictionary.TryGetValue(slotUI, out var cts)
                && !(cts?.IsCancellationRequested ?? true))
            {
                return cts;
            } 
            var newCTS = new CancellationTokenSource(); // CTS 인스턴스 생성
            progressingSlotAndCTSDictionary[slotUI] = newCTS; // 신규 (슬롯UI, CTS) 작업 중인 아이템 슬롯 딕셔너리 목록에 추가
            
            return newCTS;
        }
        
        #endregion
        
        #region Sub Popups
        
        private const string RemoveConfirmText = "아이템을 정말 파괴하시겠습니까?";
        
        private void ShowRemoveConfirmPopup(ItemSlotBaseUI slotUI)
        {
            var targetSlot = slotUI;
            var newCTS = AddNewItemModifyingProgressCTS(targetSlot);
            
            if (UIManager.Instance.ShowPopupUI<QuestionPopupUI>() is { } popup)
            {
                popup.ChainPopupCTS(newCTS.Token);
                popup.SetQuestion(
                    RemoveConfirmText,
                    YesAction: () =>
                    {
                        if (inventory == null) return; // 중간에 인벤토리 인스턴스의 참조를 잃어버린 경우 (씬 이동 등) 오류 방지
                        if (targetSlot == null || !targetSlot.gameObject.activeSelf) return; // 중간에 슬롯이 비활성화된 경우 오류 방지
                        // inventory.RemoveItem(targetSlot);
                        inventory.TryRemoveItem(targetSlot.Index);
                        if (popup is {} validPopup) validPopup.ClosePopupUI();
                    },
                    NoAction: () =>
                    {
                        if (popup is {} validPopup) validPopup.ClosePopupUI();
                    });
            }
        }

        private void ShowDetailedTooltip(ItemSlotBaseUI slotUI)
        {
            if (inventory == null) return;
            // if (inventory.FindUITargetSlot(slotUI) is not { GetAmount: > 0, GetItem: {} item, GetItemInfo: {} itemInfo } slot) return;
            if (!inventory.TryGetItem(slotUI.Index, out var item) || item.GetItemInfo is not { } itemInfo) return;
            
            Logg.Log($"{nameof(ShowDetailedTooltip)}: {slotUI}");
            var currSlotUI = slotUI;
            var newCTS = AddNewItemModifyingProgressCTS(currSlotUI);
            if (UIManager.Instance.ShowPopupUI<DetailedItemTooltipUI>() is { } popup)
            {
                popup.ChainPopupCTS(newCTS.Token);
                popup.SetTooltip(item, slotUI,
                    removeAction: itemInfo.itemType == Enums.ItemType.Special ? 
                        null : () =>
                        {
                            TryDiscardItem(currSlotUI, true);
                        }, 
                    useAction: itemInfo.isUsable ? () =>
                    {
                        TryUseItem(currSlotUI);
                        if (popup is {} validPopup) validPopup.ClosePopupUI();
                    } : null);
            }
        }

        #endregion
        
                
        #region Inventory Filter

        private readonly Dictionary<int, Transform> filterButtonDict = new ();
        private readonly Dictionary<Transform, Vector3> buttonOriginScale = new ();
        private const float HighlightScale = 1.2f;
        private const float TweenDuration = 0.15f;
        private void FillButtonDict()
        {
            filterButtonDict[(int)InventoryFilterType.All] 
                = GetButton((int)Buttons.AllFilterButton).transform;
            filterButtonDict[(int)InventoryFilterType.Equipment] 
                = GetButton((int)Buttons.EquipmentFilterButton).transform;
            filterButtonDict[(int)InventoryFilterType.Consumable] 
                = GetButton((int)Buttons.ConsumableFilterButton).transform;
            filterButtonDict[(int)InventoryFilterType.Resource] 
                = GetButton((int)Buttons.ResourceFilterButton).transform;
        }

        private void CacheOriginalFilterButtonScales()
        {
            foreach (var button in filterButtonDict.Values)
            {
                CacheOriginalScale(button);
            }
        }

        private Transform GetFilterButtonByType(InventoryFilterType filterType)
        {
            if (filterButtonDict.TryGetValue((int)filterType, out var button))
            {
                return button;
            }

            return null;
        }

        private void HighlightSelectedFilterButton(InventoryFilterType filterType)
        {
            if (GetFilterButtonByType(filterType) is not { } button) return;
            
            foreach (var kv in buttonOriginScale)
            {
                if (kv.Key is not { } other || other == button) continue;

                UnHighlightFilterButton(other, kv.Value);
            }
            
            HighlightFilterButton(button);
        }

        private static void UnHighlightFilterButton(Transform other, Vector3 scale)
        {
            other.DOKill();
            other.DOScale(scale, TweenDuration)
                .SetUpdate(UpdateType.Late, true)
                .SetEase(Ease.OutQuad)
                .SetLink(other.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void HighlightFilterButton(Transform trs)
        {
            trs.DOKill();
            if (!buttonOriginScale.TryGetValue(trs, out var originalScale))
            {
                buttonOriginScale[trs] = originalScale = trs.localScale;
            }
            trs.DOScale(originalScale * HighlightScale, TweenDuration)
                .SetUpdate(UpdateType.Late, true)
                .SetEase(Ease.OutBack)
                .SetLink(trs.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void CacheOriginalScale(Transform t)
        {
            if (t != null && !buttonOriginScale.ContainsKey(t))
            {
                buttonOriginScale[t] = t.localScale;
            }
        }

        #endregion
      
        
        private void Refresh()
        {
            UpdateAllItemSlotUIs();
            UpdateAllEquippedSlotUI();
        }
        
        protected override void Clear()
        {
            base.Clear();
            
            CancelItemDrag();
            HideTooltip();
            HideHighlight();
            
            ItemSlotBaseUI slot = mouseOverSlot;
            mouseOverSlot = null;
            ShowSlotInfo(slot);
        }
    }
}

