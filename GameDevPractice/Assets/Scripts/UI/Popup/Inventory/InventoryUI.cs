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
using TH.Core.Service;

namespace TH.UI
{
    // 인벤토리 팝업 루트 UI 및 드래그/필터 입력 중계 컴포넌트
    public sealed class InventoryUI : PopupUI, IPlayerInventoryUI
    {
        [Header("InventoryUI")]
        // 인벤토리 슬롯 패널 참조
        [SerializeField] private PlayerStorageUI storageUI;
        // 장비 슬롯 패널 참조
        [SerializeField] private PlayerEquipmentUI equipmentUI;
        // 플레이어 상태 패널 참조
        [SerializeField] private PlayerStatusPanelUI statusPanelUI;

        // 인벤토리 슬롯 UI 인터페이스 노출
        public IPlayerStorageUI StorageUI => storageUI;
        // 장비 슬롯 UI 인터페이스 노출
        public IEquipmentHolderUI EquipmentUI => equipmentUI;

        
        // UI events
        // UI 입력 이벤트 외부 전달 채널
        public event Action<IDraggableStorageUI, int> OnDragStarted;
        public event Action<DragSlotInfo> OnDragDrop;
        public event Action OnExitUICalled;
        public event Action<InventoryFilterType> OnFilterButtonPressed;
        public event Action OnSortButtonPressed;
        public event Action OnTrimButtonPressed;
        
        // scroll
        // 스크롤 영역 참조 및 드래그 상호작용 제어 핸들러
        private ScrollRect scroll;
        private ICustomScrollRectHandler scrollDragHandler;

        // drag
        // 현재 드래그 세션 상태값
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
        
        // UI 레퍼런스 보장 및 버튼 이벤트 바인딩 초기화
        protected override void Awake()
        {
            base.Awake();

            EnsureStorageUI();
            EnsureEquipmentUI();
            EnsureStatusPanelUI();

            BindObject(typeof(GameObjects));
            BindButton(typeof(Buttons));
            BindButtonEvents();

            if (GetObject((int)GameObjects.InventoryArea).TryGetComponent<ScrollRect>(out scroll))
            {
                scrollDragHandler = (ICustomScrollRectHandler)scroll;
            }
            ghostImage = dragDropGhost.GetComponent<Image>();
        }
        
        // 드래그 이벤트 구독 시작 시점
        private void Start()
        {
            SubscribeDragEvents();
        }

        IDraggableStorageUI quickSlotPanelUI;

        // 씬 UI의 퀵슬롯 패널과 드래그 종료 이벤트 연동
        void OnEnable()
        {
            if (UIManager.Instance.TryGetSceneUI(out var sceneUI) && sceneUI.IsNotNull()
                && sceneUI.GetQuickSlotPanelUI(out var quickSlotPanelUI))
            {
                this.quickSlotPanelUI = quickSlotPanelUI;
                SubscribeDragEndEvent(quickSlotPanelUI);
            }
        }

        // 비활성 전환 시 퀵슬롯 드래그 이벤트 해제
        void OnDisable()
        {
            if (quickSlotPanelUI.IsNotNull())
            {
                UnSubscribeDragEndEvent(quickSlotPanelUI);
            }
        }

        // 드래그 고스트 아이콘 포인터 위치 동기화
        private void LateUpdate()
        {
            if (!isDragging) return;

            // dragDropGhost.position = MonoInputManager.Instance.PointerPos;
            dragDropGhost.position = InputManager.Instance.PointerPos;
        }

        #region Initialization

        // 스토리지 UI 참조 보장
        private void EnsureStorageUI()
        {
            if (storageUI != null) return;
            if (Util.FindChild(gameObject, "InventoryArea", true) is { } found
                && found.TryGetComponent(out IPlayerStorageUI pStorageUI))
            {
                storageUI = (PlayerStorageUI)pStorageUI;
            }
        }
        
        // 장비 UI 참조 보장
        private void EnsureEquipmentUI()
        {
            if (equipmentUI != null) return;
            if (Util.FindChild(gameObject, "EquipmentArea", true) is { } found
                && found.TryGetComponent(out IEquipmentHolderUI pEquipmentUI))
            {
                equipmentUI = (PlayerEquipmentUI)pEquipmentUI;
            }
        }

        // 상태 패널 UI 참조 보장
        private void EnsureStatusPanelUI()
        {
            if (statusPanelUI != null) return;
            statusPanelUI = GetComponentInChildren<PlayerStatusPanelUI>(true);
        }

        // 상태 패널 대상 플레이어 인스턴스 갱신
        public void SetPlayerStatusPanel(GameObject playerInstance)
        {
            if (playerInstance == null) return;
            EnsureStatusPanelUI();
            if (statusPanelUI == null) return;
            statusPanelUI.SetPlayer(playerInstance);
        }

        #endregion

        #region Handle Drag

        private readonly Dictionary<IDraggableStorageUI, Action<int>> _dragBeginHandlers = new();
        private readonly Dictionary<IDraggableStorageUI, Action<int>> _dragEndHandlers = new();

        // 인벤토리/장비 슬롯 드래그 이벤트 일괄 구독
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

        // 드래그 시작 이벤트 구독 등록
        private void SubscribeDragBeginEvent(IDraggableStorageUI sourceUI)
        {
            UnSubscribeDragBeginEvent(sourceUI);
            Action<int> e = (index) => OnDragBegin(sourceUI, index);
            _dragBeginHandlers[sourceUI] = e;
            sourceUI.OnSlotDragged += e;
        }

        // 드래그 시작 이벤트 구독 해제
        private void UnSubscribeDragBeginEvent(IDraggableStorageUI sourceUI)
        {
            if (_dragBeginHandlers.Remove(sourceUI, out var e))
                sourceUI.OnSlotDragged -= e;
        }

        // 드래그 종료 이벤트 구독 등록
        private void SubscribeDragEndEvent(IDraggableStorageUI sourceUI)
        {
            UnSubscribeDragEndEvent(sourceUI);
            Action<int> e = (index) => OnDragEnd(sourceUI, index);
            _dragEndHandlers[sourceUI] = e;
            sourceUI.OffSlotDragged += e;
        }

        // 드래그 종료 이벤트 구독 해제
        private void UnSubscribeDragEndEvent(IDraggableStorageUI sourceUI)
        {
            if (_dragEndHandlers.Remove(sourceUI, out var e))
                sourceUI.OffSlotDragged -= e;
        }
        
        // 드래그 시작 입력 분기
        private void OnDragBegin(IDraggableStorageUI sourceUI, int index)
        {
            if (isDragging) return; // 이미 드래그 중인 경우 무시

            SetDragState(sourceUI, index);
            OnDragStarted?.Invoke(sourceUI, index); // 컨트롤러에게 드래그 가능 여부 확인을 위해 이벤트 호출
        }

        // 드래그 종료 입력 분기
        private void OnDragEnd(IDraggableStorageUI sourceUI, int index)
        {
            if (!isDragging) return; // 드래그 중이 아닐 경우 무시
            if (beginDragSourceUI == null) return; // 드래그 시작 슬롯 데이터가 유효하지 않으면 중지

            Logg.Log($"[InventoryUI] OnDragEnd - ({beginDragSourceUI}, {beginDragIdx}, {sourceUI}, {index})", Logg.LoggingMode.Completed);
            OnDragDrop?.Invoke(new DragSlotInfo(beginDragSourceUI, beginDragIdx, sourceUI, index)); // 드래그 발생 이벤트 호출
            ResetDragState(); // 드래그 플래그 갱신
        }

        // 드래그 상태값 설정 및 스크롤 잠금
        private void SetDragState(IDraggableStorageUI sourceUI, int index)
        {
            isDragging = true;
            beginDragSourceUI = sourceUI;
            beginDragIdx = index;
            scrollDragHandler?.SetDragInteractable(false);
        }
        
        // 드래그 상태값 초기화 및 스크롤 잠금 해제
        private void ResetDragState()
        {
            isDragging = false;
            beginDragSourceUI = null;
            beginDragIdx = default;
            scrollDragHandler?.SetDragInteractable(true);
        }
        
        // 드래그 고스트 표시 허용
        public void AllowDrag(Sprite sprite)
        {
            Logg.Log($"[InventoryUI] AllowDrag invoked", Logg.LoggingMode.Completed);
            
            ghostImage.sprite = sprite;
            ghostImage.enabled = true;
        }

        // 드래그 고스트 숨김 및 상태 초기화
        public void CancelDrag()
        {
            ResetDragState();
            ghostImage.enabled = false;
        }

        #endregion

        #region Bind Button Event

        // 버튼 클릭 이벤트 바인딩 루틴
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

        // 필터 버튼별 이벤트 연결 및 초기 스케일 캐시
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
        
        // 현재 선택 필터 버튼 강조 갱신
        public void UpdateFilter(InventoryFilterType filter)
        {
            if (!filterButtons.TryGetValue(filter, out var button)) return;
            
            HighlightFilterButton(button); // 새 필터 버튼 강조
            UnHighlightFilterButton(currentFilterButton); // 기존 필터 버튼 강조 해제
            currentFilterButton = button; // 현재 필터 갱신
        }
        
        // 비활성 필터 버튼 강조 해제 트윈
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

        // 활성 필터 버튼 강조 트윈
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
        
        // 버튼 원본 스케일 캐시 보장
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

        // 팝업 종료 시 내부 상태 정리 진입점
        public override void OnPopupClosed()
        {
            base.OnPopupClosed();
            Clear();
        }

        // 정렬/정리 버튼 효과음 재생
        private void PlaySortSound()
        {
            if (sfxs.TryGetValue(InventorySFX.SortSFX, out var clip))
            {
                SoundManager.Instance.Play(Enums.AudioType.Effect, clip);
            }
                
        }

        // 컨트롤러 전달 SFX 데이터 비동기 반영
        public async void SetSfx(UniTask<object> sfxData)
        {
            if (await sfxData is Dictionary<InventorySFX, AudioClip> casted)
                sfxs = casted;
            else Logg.LogWarning($"[{GetType().Name}] failed to get sfx data from Controller");
        }
    }
}

