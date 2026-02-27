using System;
using TH.Attribute;
using TH.Control;
using TH.UI;
using TH.Utils;
using UnityEngine.SceneManagement;

namespace TH.Item
{
    // 인벤토리 컨트롤러 이벤트 구독/해제 partial
    public sealed partial class InventoryController
    {
        // EventHandlerRegistry<> 인스턴스 초기화
        // 이벤트 레지스트리 생성
        private void InitializeEventRegistries()
        {
            _hoverEnterRegistry = new EventHandlerRegistry<IHoverableStorageUI, int>(
                adder: (ui, handler) => ui.OnSlotHovered += handler,
                remover: (ui, handler) => ui.OnSlotHovered -= handler
            );
            _hoverExitRegistry = new EventHandlerRegistry<IHoverableStorageUI, int>(
                adder: (ui, handler) => ui.OffSlotHovered += handler,
                remover: (ui, handler) => ui.OffSlotHovered -= handler
            );
            _clickRegistry = new EventHandlerRegistry<IClickableStorageUI, int>(
                adder: (ui, handler) => ui.OnSlotClicked += handler,
                remover: (ui, handler) => ui.OnSlotClicked -= handler
            );
            _subClickRegistry = new EventHandlerRegistry<ISubClickableStorageUI, int>(
                adder: (ui, handler) => ui.OnSlotSubClicked += handler,
                remover: (ui, handler) => ui.OnSlotSubClicked -= handler
            );
            _slotChangedRegistry = new EventHandlerRegistry<IGameItemStorage, IGameItemSlot>(
                adder: (storage, handler) => storage.OnSlotChanged += handler,
                remover: (storage, handler) => storage.OnSlotChanged -= handler
            );
            _tryUsedRegistry = new EventHandlerRegistry<IUsableItemStorage, IGameItemSlot>(
                adder: (storage, handler) => storage.OnItemTryUsed += handler,
                remover: (storage, handler) => storage.OnItemTryUsed -= handler
            );
            _capacityRegistry = new EventHandlerRegistry<IMutableCapacity, int>(
                adder: (storage, handler) => storage.OnCapacityChanged += handler,
                remover: (storage, handler) => storage.OnCapacityChanged -= handler
            );
        }

        // View(UI) Event Bind
        // UI 이벤트 구독 연결
        private void BindStorageUIEvents(IGameItemStorage storage)
        {
            if (GetUIFromStorage(storage) is not { } storageUI) return;

            if (storageUI is IHoverableStorageUI hStorageUI)
            {
                SubscribeHoverEnterEvent(hStorageUI);
                SubscribeHoverExitEvent(hStorageUI);
            }
            if (storageUI is IClickableStorageUI cStorageUI)
                SubscribeClickEvent(cStorageUI);
            if (storageUI is ISubClickableStorageUI scStorageUI)
                SubscribeSubClickEvent(scStorageUI);
        }

        // 상단 버튼 입력 이벤트 구독 연결
        private void BindButtonEvents()
        {
            pInvenUI.OnExitUICalled += OnExitCalled;
            pInvenUI.OnFilterButtonPressed += OnFilterRequested;
            pInvenUI.OnSortButtonPressed += OnInvenSortRequested;
            pInvenUI.OnTrimButtonPressed += OnInvenTrimRequested;
        }

        // 드래그 시작/드롭 이벤트 구독 연결
        private void BindDragDropUIEvents()
        {
            pInvenUI.OnDragStarted += this.OnDragStarted;
            pInvenUI.OnDragDrop += this.OnDragDrop;
        }

        // Model(Storage) Event Bind
        // 스토리지 이벤트 구독 연결
        private void BindStorageEvents(IGameItemStorage storage)
        {
            if (storage == null) return;

            // 개별 Subscribe 계열 매서드들이 반드시 UnSubscribe 이후 구독하도록 할 것 (이벤트 핸들러 누적 방지)
            // EventHandlerRegistry<> 로 관리되는 경우 Register() 호출 시 내부에서 UnRegister() 자동 호출됨
            SubscribeStorageModifiedEvent(storage);
            SubscribeSlotModifiedEvent(storage);
            if (storage is IMutableCapacity cStorage)
                SubscribeStorageCapacityEvent(cStorage);
            if (storage is IUsableItemStorage uStorage)
                SubscribeStorageUsageEvent(uStorage);
        }

        // 스토리지 이벤트 해제
        private void UnBindStorageEvents(IGameItemStorage storage)
        {
            if (storage == null) return;

            UnSubscribeStorageModifiedEvent(storage);
            _slotChangedRegistry.UnRegister(storage);
            if (storage is IMutableCapacity cStorage)
                _capacityRegistry.UnRegister(cStorage);
            if (storage is IUsableItemStorage uStorage)
                _tryUsedRegistry.UnRegister(uStorage);
        }

        // 씬 변경 시 플레이어 참조 갱신 및 재바인딩
        private void RenewPlayerReference(Scene s, LoadSceneMode m) { RenewPlayerReference(); }
        private void RenewPlayerReference()
        {
            var old = pEquipHolder;
            IEquipmentHolder newer = null;

            if (FindFirstObjectByType<PlayerController>() is { } player
                && player.TryGetComponent(out IEquipmentHolder newEquipHolder))
            {
                player.TryGetComponent<Health>(out playerHealth);
                newer = newEquipHolder;
            }

            if (old != null && old != newer)
                UnBindStorageEvents(old);

            pEquipHolder = newer;
            BindStorageEvents(pEquipHolder);
        }

        // 슬롯 호버 이벤트 연결
        private void SubscribeHoverEnterEvent(IHoverableStorageUI sourceUI)
        {
            _hoverEnterRegistry.Register(sourceUI, (index) => OnSlotHovered(sourceUI, index));
        }

        // 슬롯 호버 해제 이벤트 연결
        private void SubscribeHoverExitEvent(IHoverableStorageUI sourceUI)
        {
            _hoverExitRegistry.Register(sourceUI, (index) => OffSlotHovered(sourceUI, index));
        }

        // 슬롯 클릭 이벤트 연결
        private void SubscribeClickEvent(IClickableStorageUI sourceUI)
        {
            _clickRegistry.Register(sourceUI, (index) => OnSlotClicked(sourceUI, index));
        }

        // 슬롯 서브 클릭 이벤트 연결
        private void SubscribeSubClickEvent(ISubClickableStorageUI sourceUI)
        {
            _subClickRegistry.Register(sourceUI, (index) => OnSlotSubClicked(sourceUI, index));
        }

        // 슬롯 변경 이벤트 연결
        private void SubscribeSlotModifiedEvent(IGameItemStorage storage)
        {
            _slotChangedRegistry.Register(storage, (slot) => OnSlotItemChanged(storage, slot));
        }

        // 용량 변경 이벤트 연결
        private void SubscribeStorageCapacityEvent(IMutableCapacity storage)
        {
            _capacityRegistry.Register(storage, (capacity) => OnStorageCapacityChanged(storage, capacity));
        }

        // 아이템 사용 시도 이벤트 연결
        private void SubscribeStorageUsageEvent(IUsableItemStorage storage)
        {
            _tryUsedRegistry.Register(storage, (slot) => OnSlotItemTryUsed(storage, slot));
        }

        // 스토리지 변경 이벤트 연결(중복 방지)
        private void SubscribeStorageModifiedEvent(IGameItemStorage storage)
        {
            UnSubscribeStorageModifiedEvent(storage);
            Action handler = () => RefreshStorageUI(storage);
            _storageChangedHandlers[storage] = handler;
            storage.OnStorageChanged += handler;
        }

        // 스토리지 변경 이벤트 해제
        private void UnSubscribeStorageModifiedEvent(IGameItemStorage storage)
        {
            if (_storageChangedHandlers.Remove(storage, out var handler))
                storage.OnStorageChanged -= handler;
        }

        // 스토리지 변경 이벤트 전체 해제
        private void ClearStorageModifiedEvent()
        {
            foreach (var (storage, handler) in _storageChangedHandlers)
            {
                if (storage != null)
                    storage.OnStorageChanged -= handler;
            }
        }
    }
}
