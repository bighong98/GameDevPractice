using TH.Item.Storage;
using TH.Resource;
using TH.Utils;

namespace TH.Item
{
    // 슬롯/아이템 인스턴스 생성 보조 partial
    public sealed partial class PlayerStorage
    {
        #region Item/Slot instance building

        // 초기 용량 기준 빈 슬롯 리스트 구성
        private void FillInventoryWithEmptySlots()
        {
            if (slots.Count > 0)
            {
                Logg.Log($"[{nameof(PlayerStorage)}] item slot list has something before initialization", Logg.LoggingMode.Completed);
                slots.Clear();
            }
            for (int i = 0; i < InitialCapacity; i++)
            {
                slots.Add(MakeEmptySlot(i));
            }
        }

        // 지정 인덱스 빈 슬롯 생성 유틸리티
        private IGameItemSlot MakeEmptySlot(int index)
        {
            return new ItemSlot(index: index, item: null, validTypes: inventoryValidItemTypes);
        }

        // 아이템 타입 데이터 기반 런타임 아이템 빌더
        private readonly IItemBuilder itemBuilder = new ItemBuilder();
        private IPlayerStorage _playerStorageImplementation;

        // 기존 아이템을 타입 기반 신규 인스턴스로 정규화
        private IGameItem EnsureItemInstanceByType(IGameItem item)
        {
            if (item is not { GetAmount: int amount and > 0, GetItemInfo: { } itemInfo }) return null;
            return itemBuilder.GetItemFromData(itemInfo, amount);
        }

        // 아이템 타입 데이터 기반 인스턴스 생성
        private IGameItem EnsureItemInstanceByType(ItemTypeSO data, int amount = 1)
        {
            return itemBuilder.GetItemFromData(data, amount);
        }

        #endregion
    }
}

