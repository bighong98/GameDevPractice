using TH.Utils;
using UnityEngine;

namespace TH.Item
{
    public class ItemUsageHandler : IItemUsageHandler
    {
        public void Transfer(IGameItemStorage source, IGameItemStorage destination, IGameItemSlot slot)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: {} item, GetItemInfo: { } data }) return;

            if (!destination.TryStore(item, out var destSlot) || 
                !(source.TryRemoveItem(slot.Index, out var existed) && item == existed))
            {
                destSlot?.Clear();
                Logg.LogError($"[ItemUsageHandler] something went wrong while Transfer({source}, {destination}, {slot})");
            }
            
        }

        private void Transfer(IGameItemStorage oneSource, IGameItemStorage anotherSource,
            IGameItemSlot oneSlot, IGameItemSlot anotherSlot)
        {
            if (!anotherSource.TryStore(oneSlot.GetItem, anotherSlot.Index))
            {
                Logg.LogError($"[ItemUsageHandler] Transfer(from TransferOrSwap) - TryStore({oneSlot.GetItem}, {anotherSlot.Index}) failed");
                return;
            }
            
            if (!oneSource.TryRemoveItem(oneSlot.Index)) // 기존 슬롯에서 아이템 제거 시도
            {
                Logg.LogError($"[ItemUsageHandler] Transfer(from TransferOrSwap) - TryRemoveItem({anotherSlot.Index}) for roll back failed");
                anotherSource.TryRemoveItem(anotherSlot.Index); // 원복 시도
            }
        }

        public void TransferOrSwap(IGameItemStorage oneSource, IGameItemStorage anotherSource, 
            IGameItemSlot oneSlot, IGameItemSlot anotherSlot)
        {
            Logg.Log($"[ItemUsageHandler] TransferOrSwap({oneSource}, {anotherSource}, {oneSlot}, {anotherSlot}) invoked", Logg.LoggingMode.Completed);

            if (!(anotherSlot?.HasItem ?? false)) // anotherSlot(=드랍 슬롯)이 빈 슬롯이라면 단순 아이템 이동
            {
                Transfer(oneSource, anotherSource, oneSlot, anotherSlot);
                return;
            }
            
            if (!oneSource.TryRemoveItem(oneSlot.Index, out var oneItem) ||
                !anotherSource.TryRemoveItem(anotherSlot.Index, out var anotherItem))
            {
                if (oneItem != null) // anotherSource로부터 아이템 제거에 실패한 경우
                    oneSource.TryStore(oneItem, oneSlot.Index); // 원복
                return;
            }

            if (!oneSource.TryStore(anotherItem, oneSlot.Index))
            {
                if (oneSource.TryStore(oneItem, oneSlot.Index) &&
                    anotherSource.TryStore(anotherItem, anotherSlot.Index))
                {
                    Logg.Log($"[{nameof(ItemUsageHandler)}] ItemSwap failed and roll backed", Logg.LoggingMode.Completed);
                    return;
                }
                
                Logg.LogError($"[{nameof(ItemUsageHandler)}] ItemSwap failed and roll back failed");
                return;
            }

            if (!anotherSource.TryStore(oneItem, anotherSlot.Index))
            {
                if (oneSource.TryRemoveItem(oneSlot.Index) &&
                    oneSource.TryStore(oneItem, oneSlot.Index) &&
                    anotherSource.TryStore(anotherItem, anotherSlot.Index))
                {
                    Logg.Log($"[{nameof(ItemUsageHandler)}] ItemSwap failed and roll backed", Logg.LoggingMode.Completed);
                    return;
                }
                Logg.LogError($"[{nameof(ItemUsageHandler)}] ItemSwap failed and roll back failed");
                return;
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

