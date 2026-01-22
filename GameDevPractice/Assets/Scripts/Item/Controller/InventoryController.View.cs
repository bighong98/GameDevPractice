using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Attribute;
using TH.Core;
using TH.Core.Service;
using TH.Item.Storage;
using TH.Resource;
using TH.UI;
using TH.UI.Data;
using TH.Utils;
using UnityEngine;

namespace TH.Item
{
    public sealed partial class InventoryController
    {
        // 인벤토리 SFX 로드
        private static async UniTask<object> LoadInventorySFX(InventorySFXCatalogSO catalog)
        {
            Dictionary<InventorySFX, AudioClip> dict = new();
            foreach (var (key, value) in catalog.Items)
            {
                dict[key] = await ResourceManager.Instance.ExtractAssetRefAsync(value);
            }

            return dict;
        }

        // 슬롯 데이터 변경 시 UI 반영
        private void OnSlotItemChanged(IGameItemStorage storage, IGameItemSlot slot)
        {
            if (GetUIFromStorage(storage) is not { } ui) return;
            if (slot == null) return;

            CancelModifiedSlotProgress(slot);
            ui.DrawSlot(slot.Index, slot.GetItem);
        }

        // 용량 변경 시 UI 반영
        private void OnStorageCapacityChanged(IMutableCapacity storage, int capacity)
        {
            if (GetUIFromStorage(storage) is not IMutableCapacityStorageUI storageUI) return;
            UpdateStorageUICapacity(storageUI, capacity);
        }

        // 사용 시도 결과를 반영
        private void OnSlotItemTryUsed(IUsableItemStorage storage, IGameItemSlot slot)
        {
            this.Log($"OnSlotItemTryUsed - storage{storage}, slot: {slot}", Logg.LoggingMode.Completed);
            if (slot is not { HasItem: true, GetItem: { } item, GetItemInfo: { } itemData }) return;
            if (!itemData.isUsable) return;

            IGameItemStorage dest = storage == pStorage ? pEquipHolder : pStorage;

            switch (item.Type)
            {
                case Enums.ItemType.Countable:
                    if (storage is not IConsumableItemStorage consumableStorage) return;
                    itemConsumer.TryConsume(consumableStorage, slot, playerHealth, 1);
                    break;
                case Enums.ItemType.Equipment:
                    itemTransfer.TransferOrSwap(storage, slot, dest);
                    break;
                default:
                    break;
            }

            if (GetUIFromStorage(storage) is IHighlightableStorageUI hStorageUI)
            {
                hStorageUI.HighlightSlot(slot.Index, (int)SlotHighlightType.Modified);
                hStorageUI.UnHighlightSlotWithFade(slot.Index, (int)SlotHighlightType.Modified);
            }
        }

        // 스토리지 전체 UI 다시 그리기
        private void RefreshStorageUI(IGameItemStorage storage)
        {
            if (storage.IsNull() || storage.ItemSlots is not { } itemSlots)
            {
                this.LogWarning("RefreshStorageUI - invalid storage instance or itemSlot is not initialized", context: this);
                return;
            }
            if (GetUIFromStorage(storage) is not { } storageUI) return;

            foreach (var slot in storage.ItemSlots)
            {
                var index = slot.Index;

                if (slot is { HasItem: true, GetItem: { } item })
                    storageUI.DrawSlot(index, item);
                else storageUI.CleanSlot(index);

                if (slot.IsVisible)
                    storageUI.ShowSlot(index);
                else storageUI.HideSlot(index);
            }
        }

        // 플레이어 인벤토리 UI 갱신
        private void RefreshStorageUI()
        {
            RefreshStorageUI(pStorage);
        }

        // 장비 UI 갱신
        private void RefreshEquipmentUI()
        {
            RefreshStorageUI(pEquipHolder);
        }

        // 용량 표시 업데이트
        private void UpdateStorageUICapacity(IMutableCapacityStorageUI storageUI, int capacity)
        {
            storageUI.SetCapacity(capacity);
        }

        // 슬롯 하이라이트 토글
        private void SetHighlightSlot(object source, int index, bool state)
        {
            if (source is not IHighlightableStorageUI highUI)
                return;

            if (state)
                highUI.HighlightSlot(index);
            else highUI.UnHighlightSlot(index);
        }

        // 아이템 사용 처리(사용/장착/해제)
        private void HandleItemUse(IUsableItemStorage storage, IGameItemSlot slot)
        {
            OnSlotItemTryUsed(storage, slot);
        }

        // 나누기 입력 값 검증 후 처리
        private void HandleItemDivide(IGameItemStorage storage, IGameItemSlot slot, LazyValue<int> expected)
        {
            if (expected is not { Initialized: true }) return;
            HandleItemDivide(storage, slot, expected.Value);
        }

        // 실제 분할 처리
        private void HandleItemDivide(IGameItemStorage storage, IGameItemSlot slot, int expected)
        {
            if (storage is not IDividableStorage dStorage) return;
            dStorage.TryDivide(slot.Index, expected);
        }

        // 필터 적용 후 UI 갱신
        private void FilterStorage(IGameItemStorage storage, InventoryFilterType filter)
        {
            if (storage.ItemSlots.Count == 0) return;
            foreach (var slot in storage.ItemSlots)
            {
                slot.SetVisibility(IsVisibleByFilter(slot, filter));
            }

            RefreshStorageUI(storage);
        }

        // UI 인스턴스로부터 대응 스토리지 조회
        private IGameItemStorage GetStorageFromUI(object targetUI)
        {
            if (targetUI == pInvenUI.StorageUI) return pStorage;
            if (targetUI == pInvenUI.EquipmentUI) return pEquipHolder;
            if (targetUI is QuickSlotPanelUI) return pQuickStorage;

            return null;
        }

        // 스토리지 인스턴스로부터 대응 UI 조회
        private IStorageUI GetUIFromStorage(object storage)
        {
            if (storage == pStorage) return pInvenUI.StorageUI;
            if (storage == pEquipHolder) return pInvenUI.EquipmentUI;

            return null;
        }

        // 사용 버튼 텍스트 결정
        private string GetUseButtonText(IGameItemStorage storage, ItemTypeSO itemInfo)
        {
            if (storage is IEquipmentHolder)
                return DefaultUnEquipText;
            if (itemInfo.itemType == Enums.ItemType.Equipment)
                return DefaultEquipText;
            return DefaultConsumeText;
        }

        // 필터에 따른 슬롯 가시성 판단
        private static bool IsVisibleByFilter(IGameItemSlot slot, InventoryFilterType filter)
        {
            return filter switch
            {
                InventoryFilterType.All => true,
                InventoryFilterType.Equipment => slot is { HasItem: true, GetItemInfo: { itemType: Enums.ItemType.Equipment } },
                InventoryFilterType.Consumable => slot is { HasItem: true, GetItemInfo: { itemType: Enums.ItemType.Countable, isUsable: true } },
                InventoryFilterType.Resource => slot is { HasItem: true, GetItemInfo: { itemType: Enums.ItemType.Countable, isUsable: false } },
                _ => false
            };
        }
    }
}
