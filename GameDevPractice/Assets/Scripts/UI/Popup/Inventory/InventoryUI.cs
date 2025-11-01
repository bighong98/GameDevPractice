using System;
using System.Collections.Generic;
using DG.Tweening;
using RPG.UI;
using TH.Item;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public class InventoryUI : PopupUI, IPlayerInventoryUI
    {
        [Header("InventoryUI")]
        [SerializeField] private PlayerStorageUI storageUI;
        [SerializeField] private PlayerEquipmentUI equipmentUI;

        public IPlayerStorageUI StorageUI => storageUI;
        public IEquipmentHolderUI EquipmentUI => equipmentUI;
        
        // UI events
        public event Action<IDraggableStorageUI, int> OnDragStarted;
        public event Action<DragSlotInfo> OnDragDrop;
        public event Action OnExitUICalled;
        public event Action<InventoryFilterType> OnFilterButtonPressed;
        public event Action OnSortButtonPressed;
        public event Action OnTrimButtonPressed;
        
        // scroll
        private ScrollRect scroll;
        private ICustomScrollRectHandler scrollDragHandler;

        // drag
        private bool isDragging = false;
        private IDraggableStorageUI beginDragSourceUI; // 드래그가 시작된 슬롯의 소속
        private int beginDragIdx; // 드래그가 시작된 슬롯의 인덱스
        
        [SerializeField] private Transform dragDropGhost;
        private Image ghostImage;
        
        #region Enum

        enum GameObjects
        {
            Contents,
            
            InventoryArea,
            ItemSlots,
            DragDropIconHolder,
        }

        enum Buttons
        {
            ExitButton,
            SortButton,
            TrimButton,
            
            AllFilterButton,
            EquipmentFilterButton,
            ConsumableFilterButton,
            ResourceFilterButton,
        }

        #endregion
        
        protected override void Awake()
        {
            base.Awake();

            EnsureStorageUI();
            EnsureEquipmentUI();
            
            BindObject(typeof(GameObjects));
            BindButton(typeof(Buttons));
            BindButtonEvents();

            if (GetObject((int)GameObjects.InventoryArea).TryGetComponent<ScrollRect>(out scroll))
            {
                scrollDragHandler = (ICustomScrollRectHandler)scroll;
            }
            ghostImage = dragDropGhost.GetComponent<Image>();
        }
        
        private void Start()
        {
            SubscribeDragEvents();
        }

        private void LateUpdate()
        {
            if (!isDragging) return;

            dragDropGhost.position = InputManager.Instance.PointerPos;
        }

        #region Initialization

        private void EnsureStorageUI()
        {
            if (storageUI != null) return;
            if (Util.FindChild(gameObject, "InventoryArea", true) is { } found
                && found.TryGetComponent(out IPlayerStorageUI pStorageUI))
            {
                storageUI = (PlayerStorageUI)pStorageUI;
            }
        }
        
        private void EnsureEquipmentUI()
        {
            if (equipmentUI != null) return;
            if (Util.FindChild(gameObject, "EquipmentArea", true) is { } found
                && found.TryGetComponent(out IEquipmentHolderUI pEquipmentUI))
            {
                equipmentUI = (PlayerEquipmentUI)pEquipmentUI;
            }
        }

        #endregion
        
        #region Handle Drag

        private void SubscribeDragEvents()
        {
            SubscribeDragBeginEvent(storageUI);
            SubscribeDragBeginEvent(equipmentUI);
            SubscribeDragEndEvent(storageUI);
            SubscribeDragEndEvent(equipmentUI);
        }

        private void SubscribeDragBeginEvent(IDraggableStorageUI sourceUI)
        {
            sourceUI.OnSlotDragged += (index) => { OnDragBegin(sourceUI, index); };
        }
        
        private void SubscribeDragEndEvent(IDraggableStorageUI sourceUI)
        {
            sourceUI.OffSlotDragged += (index) => { OnDragEnd(sourceUI, index); };
        }
        
        private void OnDragBegin(IDraggableStorageUI sourceUI, int index)
        {
            if (isDragging) return; // 이미 드래그 중인 경우 무시

            SetDragState(sourceUI, index);
            OnDragStarted?.Invoke(sourceUI, index); // 컨트롤러에게 드래그 가능 여부 확인을 위해 이벤트 호출
        }

        private void OnDragEnd(IDraggableStorageUI sourceUI, int index)
        {
            if (!isDragging) return; // 드래그 중이 아닐 경우 무시
            if (beginDragSourceUI == null) return; // 드래그 시작 슬롯 데이터가 유효하지 않으면 중지
            
            OnDragDrop?.Invoke(new DragSlotInfo(beginDragSourceUI, beginDragIdx, sourceUI, index)); // 드래그 발생 이벤트 호출
            ResetDragState(); // 드래그 플래그 갱신
        }

        private void SetDragState(IDraggableStorageUI sourceUI, int index)
        {
            isDragging = true;
            beginDragSourceUI = sourceUI;
            beginDragIdx = index;
            scrollDragHandler?.SetDragInteractable(false);
        }
        
        private void ResetDragState()
        {
            isDragging = false;
            beginDragSourceUI = null;
            beginDragIdx = default;
            scrollDragHandler?.SetDragInteractable(true);
        }
        
        public void AllowDrag(Sprite sprite)
        {
            Logg.Log($"[InventoryUI] AllowDrag invoked", Logg.LoggingMode.Completed);
            
            ghostImage.sprite = sprite;
            ghostImage.enabled = true;
        }

        public void CancelDrag()
        {
            ResetDragState();
            ghostImage.enabled = false;
        }

        #endregion

        #region Bind Button Event

        private void BindButtonEvents()
        {
            GetButton((int)Buttons.ExitButton).onClick.AddListener(() => {OnExitUICalled?.Invoke();});
            BindFilterButtonEvent(GetButton((int)Buttons.AllFilterButton), InventoryFilterType.All);
            BindFilterButtonEvent(GetButton((int)Buttons.EquipmentFilterButton), InventoryFilterType.Equipment);
            BindFilterButtonEvent(GetButton((int)Buttons.ConsumableFilterButton), InventoryFilterType.Consumable);
            BindFilterButtonEvent(GetButton((int)Buttons.ResourceFilterButton), InventoryFilterType.Resource);
            
            GetButton((int)Buttons.SortButton).onClick.AddListener(() => { OnSortButtonPressed?.Invoke(); });
            GetButton((int)Buttons.TrimButton).onClick.AddListener(() => { OnTrimButtonPressed?.Invoke(); });
        }

        private void BindFilterButtonEvent(Button button, InventoryFilterType filter)
        {
            filterButtons[filter] = button;
            CacheOriginalScale(button);
            button.onClick.AddListener(() => {
                OnFilterButtonPressed?.Invoke(filter);
            });
        }

        #endregion

        #region Filter

        private readonly Dictionary<InventoryFilterType, Button> filterButtons = new();
        private readonly Dictionary<Button, Vector3> buttonOriginalScales = new();
        private Button currentFilterButton;
        
        private const float HighlightScale = 1.2f;
        private const float TweenDuration = 0.15f;
        
        public void UpdateFilter(InventoryFilterType filter)
        {
            if (!filterButtons.TryGetValue(filter, out var button)) return;
            
            HighlightFilterButton(button); // 새 필터 버튼 강조
            UnHighlightFilterButton(currentFilterButton); // 기존 필터 버튼 강조 해제
            currentFilterButton = button; // 현재 필터 갱신
        }
        
        private void UnHighlightFilterButton(Button btn)
        {
            if (btn == null || btn.transform == null) return;
            var other = btn.transform;
            if (!buttonOriginalScales.TryGetValue(btn, out var scale)) return;
            
            other.DOKill();
            other.DOScale(scale, TweenDuration)
                .SetUpdate(UpdateType.Late, true)
                .SetEase(Ease.OutQuad)
                .SetLink(other.gameObject, LinkBehaviour.KillOnDestroy);
        }

        private void HighlightFilterButton(Button btn)
        {
            var trs = btn.transform;
            trs.DOKill();
            if (!buttonOriginalScales.TryGetValue(btn, out var originalScale))
            {
                buttonOriginalScales[btn] = originalScale = trs.localScale;
            }
            trs.DOScale(originalScale * HighlightScale, TweenDuration)
                .SetUpdate(UpdateType.Late, true)
                .SetEase(Ease.OutBack)
                .SetLink(trs.gameObject, LinkBehaviour.KillOnDestroy);
        }
        
        private void CacheOriginalScale(Button btn)
        {
            if (btn != null && !buttonOriginalScales.ContainsKey(btn))
            {
                buttonOriginalScales[btn] = btn.transform.localScale;
            }
        }

        #endregion
        
        protected override void Clear()
        {
            base.Clear();
            ResetDragState();
        }

        public override void OnPopupClosed()
        {
            base.OnPopupClosed();
            Clear();
        }


    }
}

