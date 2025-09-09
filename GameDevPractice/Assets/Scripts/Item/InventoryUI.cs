using System;
using System.Collections.Generic;
using DG.Tweening;
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
        
            InventoryArea,
            ItemSlots,
            DragDropIconHolder,
            
            PopupPanel,
            UI_RemoveConfirmPopup,
            UI_ItemTooltip,
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
        
        private RPG.Item.InventorySystem inventorySystem; 
        
        private GraphicRaycaster graphicRaycaster;
        private PointerEventData pointerEventData;
        private List<RaycastResult> raycastResults;
        private RectTransform inventoryUIRect; // 인벤토리UI 경계 기준 (현재 Contents.RectTransform)
        
        private UI_ItemTooltip itemTooltip; 
        private ScrollRect scroll; // 인벤토리 스크롤
        
        [SerializeField] private List<ItemSlotUI> itemSlotUIs;
        [SerializeField] private UI_EquipmentSlot[] equipmentSlotUIs;
        
        private GameObject itemSlotUIPrefab;
        private ObjectPool<ItemSlotUI> slotUIPool;
        
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
        
        private void Awake()
        {
            raycastResults = new List<RaycastResult>();
            pointerEventData = new PointerEventData(EventSystem.current);
            
            inventorySystem = FindFirstObjectByType<RPG.Item.InventorySystem>();
            if (inventorySystem == null)
            {
                Util.LogError($"[{typeof(InventoryUI)}] Failed to Find InventorySystem instance");
                return;
            }
            
            Init();
        }

        private void OnEnable()
        {
            SubscribeInputEvents();
            OnInventoryCapacityChanged(inventorySystem.Capacity);
            OnFilterChanged(inventorySystem.CurrentFilter);
            
            inventorySystem.OnInventoryChanged += this.UpdateAllItemSlotUIs;
            inventorySystem.OnCapacityChanged += this.OnInventoryCapacityChanged;
            inventorySystem.OnInventoryFilterChanged += this.OnFilterChanged;
        }

        private void OnDisable()
        {
            DeSubscribeInputEvents();
            
            inventorySystem.OnCapacityChanged -= this.OnInventoryCapacityChanged;
            inventorySystem.OnInventoryChanged -= this.UpdateAllItemSlotUIs; 
            inventorySystem.OnInventoryFilterChanged -= this.OnFilterChanged;
            
            Clear();
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
            BindButton(typeof(Buttons));

            if (GetObject((int)GameObjects.Contents) is { } contentsArea)
            {
                graphicRaycaster = contentsArea.GetOrAddComponent<GraphicRaycaster>();
                inventoryUIRect = contentsArea.GetComponent<RectTransform>();
            }
            
            scroll = GetObject((int)GameObjects.InventoryArea).GetComponent<ScrollRect>();
            dragDropIconHolder = GetObject((int)GameObjects.DragDropIconHolder).transform;

            ConnectButtons();
            InitializeSlotUIs();
            ConnectDataWithSlotUIs();
            FillButtonDict();
            CacheOriginalFilterButtonScales();
            
            
            return true;
        }

        private void ConnectButtons()
        {
            GetButton((int)Buttons.ExitButton).onClick.AddListener(OnExitButtonPressed);
            GetButton((int)Buttons.SortButton).onClick.AddListener(OnSortButtonPressed);
            GetButton((int)Buttons.CompressButton).onClick.AddListener(OnCompressButtonPressed);
            
            GetButton((int)Buttons.AllFilterButton).onClick.AddListener(() =>
            {
                OnFilterButtonPressed(InventorySystem.InventoryFilterType.All);
            });
            GetButton((int)Buttons.EquipmentFilterButton).onClick.AddListener(() =>
            {
                OnFilterButtonPressed(InventorySystem.InventoryFilterType.Equipment);
            });
            GetButton((int)Buttons.ConsumableFilterButton).onClick.AddListener(() =>
            {
                OnFilterButtonPressed(InventorySystem.InventoryFilterType.Consumable);
            });
            GetButton((int)Buttons.ResourceFilterButton).onClick.AddListener(() =>
            {
                OnFilterButtonPressed(InventorySystem.InventoryFilterType.Resource);
            });
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

                bool isActive = i < slotCap;
                if (itemSlotUIs[i] is { } slotUI)
                {
                    slotUI.SetSlotAccessibleState(isActive);
                    slotUI.SetItemAccessibleState(isActive);
                    if (!isActive)
                        DisableSlotUI(i);
                }
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
        
        #endregion

        #region Validate Slot UI

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

        private void UpdateSlotUI(int index) // 인벤토리 슬롯 UI 갱신 (장비슬롯x)
        {
            if (inventorySystem.GetInventorySlot(index) is not { } itemSlot) return; // 인벤토리 시스템으로부터 슬롯 정보 받아오기

            if (itemSlot.IsVisible) 
            {
                EnableSlotUI(index);
            }
            else // 슬롯이 비가시처리된 경우 (인벤토리 필터 등)
            {
                DisableSlotUI(index);
            }
            
            if (itemSlot is not { HasItem: true } ) // 슬롯에 아이템이 없는 경우 (빈 슬롯)
            {
                CleanSlot(index);
                return;
            }
            
            SetInventorySlotIcon(index, itemSlot);

            if (itemSlot.GetItem is not RPG.Item.CountableItem cItem) // 1-1. 셀 수 없는 아이템
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
            for (int i = 0; i < itemSlotUIs.Count; i++)
            {
                UpdateSlotUI(i);
            }
        }

        private void UpdateEquippedSlotUI(int index) // 장비 슬롯 UI 갱신
        {
            var equippedItem = inventorySystem.GetEquippedSlot(index);
            if (equippedItem is null or { HasItem: false } )
            {
                equipmentSlotUIs[index].RemoveIcon(); // todo: 함수로 래핑
                return;
            }
            
            SetEquipmentSlotIcon(index, equippedItem);
        }
        
        private void UpdateAllEquippedSlotUI()
        {
            for (int i = 0; i < equipmentSlotUIs.Length; i++)
            {
                UpdateEquippedSlotUI(i);
            }
        }

        private void DisableSlotUI(int index)
        {
            if (!IsValidInventoryIndex(index)) return;
            if (itemSlotUIs[index] is not { gameObject: { activeSelf: true } } slotUI) return;
            
            slotUI.SetSlotAccessibleState(false);
            slotUI.gameObject.SetActive(false);
        }

        private void EnableSlotUI(int index)
        {
            if (!IsValidInventoryIndex(index)) return;
            if (itemSlotUIs[index].gameObject.activeSelf) return; // 이미 활성화되어 있다면 실행x

            // 리스트에 새 슬롯을 추가하거나 기존 슬롯 재활성화
            ItemSlotUI slotUI = itemSlotUIs.Count <= index ? AddItemSlotUI() : itemSlotUIs[index];
            slotUI.SetSlotAccessibleState(true);
            slotUI.gameObject.SetActive(true);
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
                TryDiscardItem(beginDragSlot); // 아이템 버리기(삭제) 시도
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
            if (RaycastAndGetFirstComponent<ItemSlotBaseUI>() is { } slotUI && inventorySystem != null)
            {
                inventorySystem.TryUseItem(slotUI);
            }
        }
        
        private void TrySwapItems(ItemSlotBaseUI fromSlotUI, ItemSlotBaseUI toSlotUI)
        {
            if (inventorySystem == null) return;
            
            Util.Log($"trying to TrySwapItems({fromSlotUI}.{fromSlotUI.Index}, {toSlotUI}.{toSlotUI.Index})", Util.LoggingMode.Completed);
            inventorySystem.TrySwapItems(fromSlotUI, toSlotUI);
        }

        private void TryDiscardItem(ItemSlotBaseUI slotUI)
        {
            if (inventorySystem == null) return;

            inventorySystem.RemoveItem(slotUI);
        }
        
        private void OnExitButtonPressed()
        {
            UIManager.Instance.ClosePopupUI(this);
        }

        private void OnCompressButtonPressed()
        {
            if (inventorySystem == null) return;
            inventorySystem.CompressInven(false);
        }

        private void OnSortButtonPressed()
        {
            if (inventorySystem == null) return;
            inventorySystem.CompressInven(true);
        }

        private void OnFilterButtonPressed(InventorySystem.InventoryFilterType filter)
        {
            if (inventorySystem == null) return;
            inventorySystem.TryFilterInven(filter);
        }
        
        #endregion

        #region Handle Event (Subscribe, Listen)
        
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

        private void OnFilterChanged(InventorySystem.InventoryFilterType filter)
        {
            HighlightSelectedFilterButton(filter);
            UpdateAllItemSlotUIs();
        }
        
        private void OnInventoryCapacityChanged(int capa)
        {
            int currCount = itemSlotUIs.Count; 
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
            
            Util.Log(raycastResults[0], Util.LoggingMode.Completed);
            return raycastResults[0].gameObject.GetComponent<T>();
        }

        #endregion

        #region Highlight Slot UI

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

        #region Inventory Filter

        private readonly Dictionary<int, Transform> filterButtonDict = new ();
        private readonly Dictionary<Transform, Vector3> buttonOriginScale = new ();
        private const float HighlightScale = 1.2f;
        private const float TweenDuration = 0.15f;
        private void FillButtonDict()
        {
            filterButtonDict[(int)InventorySystem.InventoryFilterType.All] 
                = GetButton((int)Buttons.AllFilterButton).transform;
            filterButtonDict[(int)InventorySystem.InventoryFilterType.Equipment] 
                = GetButton((int)Buttons.EquipmentFilterButton).transform;
            filterButtonDict[(int)InventorySystem.InventoryFilterType.Consumable] 
                = GetButton((int)Buttons.ConsumableFilterButton).transform;
            filterButtonDict[(int)InventorySystem.InventoryFilterType.Resource] 
                = GetButton((int)Buttons.ResourceFilterButton).transform;
        }

        private void CacheOriginalFilterButtonScales()
        {
            foreach (var button in filterButtonDict.Values)
            {
                CacheOriginalScale(button);
            }
        }

        private Transform GetFilterButtonByType(InventorySystem.InventoryFilterType filterType)
        {
            if (filterButtonDict.TryGetValue((int)filterType, out var button))
            {
                return button;
            }

            return null;
        }

        private void HighlightSelectedFilterButton(InventorySystem.InventoryFilterType filterType)
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
        
        private void Clear()
        {
            CancelItemDrag();
            HideTooltip();
            HideHighlight();
            
            ItemSlotBaseUI slot = mouseOverSlot;
            mouseOverSlot = null;
            ShowSlotInfo(slot);
        }
    }
}

