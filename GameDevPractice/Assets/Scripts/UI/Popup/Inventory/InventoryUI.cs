using System;
using System.Collections.Generic;
using System.Resources;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TH.Core;
using TH.Item;
using TH.UI.Data;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    public sealed class InventoryUI : PopupUI, IPlayerInventoryUI
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
        
        private Dictionary<InventorySFX, AudioClip> sfxs;
        
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

        IDraggableStorageUI quickSlotPanelUI;
        void OnEnable()
        {
            if (UIManager.Instance.TryGetSceneUI(out var sceneUI) && sceneUI.IsAlive()
                && sceneUI.GetQuickSlotPanelUI(out var quickSlotPanelUI))
            {
                this.quickSlotPanelUI = quickSlotPanelUI;
                SubscribeDragEndEvent(quickSlotPanelUI);
            }
        }

        void OnDisable()
        {
            if (quickSlotPanelUI.IsAlive())
            {
                UnSubscribeDragEndEvent(quickSlotPanelUI);
            }
        }

        private void LateUpdate()
        {
            if (!isDragging) return;

            // dragDropGhost.position = MonoInputManager.Instance.PointerPos;
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

        private readonly Dictionary<IDraggableStorageUI, Action<int>> _dragBeginHandlers = new();
        private readonly Dictionary<IDraggableStorageUI, Action<int>> _dragEndHandlers = new();

        private void SubscribeDragEvents()
        {
            SubscribeDragBeginEvent(storageUI);
            SubscribeDragBeginEvent(equipmentUI);
            SubscribeDragEndEvent(storageUI);
            SubscribeDragEndEvent(equipmentUI);
        }

        // private void SubscribeDragBeginEvent(IDraggableStorageUI sourceUI)
        // {
        //     sourceUI.OnSlotDragged += (index) => { OnDragBegin(sourceUI, index); };
        // }
        
        // private void SubscribeDragEndEvent(IDraggableStorageUI sourceUI)
        // {
        //     sourceUI.OffSlotDragged += (index) => { OnDragEnd(sourceUI, index); };
        // }

        private void SubscribeDragBeginEvent(IDraggableStorageUI sourceUI)
        {
            UnSubscribeDragBeginEvent(sourceUI);
            Action<int> e = (index) => OnDragBegin(sourceUI, index);
            _dragBeginHandlers[sourceUI] = e;
            sourceUI.OnSlotDragged += e;
        }

        private void UnSubscribeDragBeginEvent(IDraggableStorageUI sourceUI)
        {
            if (_dragBeginHandlers.Remove(sourceUI, out var e))
                sourceUI.OnSlotDragged -= e;
        }

        private void SubscribeDragEndEvent(IDraggableStorageUI sourceUI)
        {
            UnSubscribeDragEndEvent(sourceUI);
            Action<int> e = (index) => OnDragEnd(sourceUI, index);
            _dragEndHandlers[sourceUI] = e;
            sourceUI.OffSlotDragged += e;
        }

        private void UnSubscribeDragEndEvent(IDraggableStorageUI sourceUI)
        {
            if (_dragEndHandlers.Remove(sourceUI, out var e))
                sourceUI.OffSlotDragged -= e;
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

            Logg.Log($"[InventoryUI] OnDragEnd - ({beginDragSourceUI}, {beginDragIdx}, {sourceUI}, {index})", Logg.LoggingMode.Completed);
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
            
            GetButton((int)Buttons.SortButton).onClick.AddListener(() => { OnSortButtonPressed?.Invoke(); PlaySortSound(); });
            GetButton((int)Buttons.TrimButton).onClick.AddListener(() => { OnTrimButtonPressed?.Invoke(); PlaySortSound(); });
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

        private void PlaySortSound()
        {
            if (sfxs.TryGetValue(InventorySFX.SortSFX, out var clip))
            {
                SoundManager.Instance.Play(Enums.AudioType.Effect, clip);
            }
                
        }

        public async void SetSfx(UniTask<object> sfxData)
        {
            if (await sfxData is Dictionary<InventorySFX, AudioClip> casted)
                sfxs = casted;
            else Logg.LogWarning($"[{GetType().Name}] failed to get sfx data from Controller");
        }
    }
}

