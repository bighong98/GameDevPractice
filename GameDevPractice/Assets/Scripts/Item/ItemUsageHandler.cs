using TH.Utils;
using UnityEngine;

namespace TH.Item
{
    public class ItemUsageHandler : IItemUsageHandler
    {
        public void Transfer(IGameItemStorage source, IGameItemStorage destination, IGameItemSlot slot)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: {} item, GetItemInfo: { } data }) return;

            if (!destination.TryStore(item) || 
                !(source.TryRemoveItem(slot.Index, out var existed) && item == existed))
            {
                //todo: 아이템 제자리로 롤백
                Logg.Log($"[ItemUsageHandler] something went wrong while Transfer({source}, {destination}, {slot})", Logg.LoggingMode.InProgress);
            }
            
        }

        public void Consume(IGameItemStorage source, object destination, IGameItemSlot slot, int amount = 1)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: {} item, GetItemInfo: { } data }) return;
            //todo: data 기반으로 사용 효과 적용
            //todo: 사용 실패 시 아무것도x
            //case 사용 성공
            if (item is ICountableItem cItem)
            {
                cItem.SetAmount(cItem.GetAmount - amount);
            }
            //todo: countable은 아니지만 사용시 없어지는 아이템 제거
        }
    }
}

