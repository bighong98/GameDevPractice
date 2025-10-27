using TH.Utils;
using UnityEngine;

namespace TH.Item
{
    public class GameItemTransfer : IGameItemTransfer
    {
        public void Transfer(IGameItemStorage source, IGameItemSlot slot, IGameItemStorage destination)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: { } item })
            {
                Logg.LogError($"[GameItemTransfer] Transfer invoked from invalid slot ({source} - {slot})");
                return;
            }
            // 도착 저장소에 아이템 저장 시도 & 출발 저장소에서 아이템 제거 시도
            if (!destination.TryStore(item, out var destSlot) ||
                !(source.TryRemoveItem(slot.Index, out var existed) && item.IsEqual(existed, ItemComparerExtension.ItemCompareMode.CompareInstance)))
            {
                destSlot?.Clear(); // 저장만 성공, 제거는 실패 시 복사 방지를 위해 도착 슬롯의 아이템 제거
                Logg.LogError($"[{nameof(GameItemTransfer)}] Item is missing while Transfer()");
            }
        }

        public void Transfer(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other, IGameItemSlot otherSlot)
        {
            if (sourceSlot is not { IsAccessible: true, HasItem: true, GetItem: { } item })
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Transfer invoked from invalid slot ({source} - {sourceSlot})");
                return;
            }

            if (!other.TryStore(sourceSlot.GetItem, otherSlot.Index)) // 도착 슬롯에 아이템 저장 시도
            {
                Logg.Log($"[{nameof(GameItemTransfer)}] Transfer Item failed ({source}, {sourceSlot}) - to ({other}, {otherSlot}))");
                return;
            }

            if (!source.TryRemoveItem(sourceSlot.Index)) // 출발 슬롯에서 아이템 제거 시도
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Item is missing while Transfer ({source}, {sourceSlot}) - to ({other}, {otherSlot}))");
                return;
            }
        }
        
        public void TransferOrSwap(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other)
        {
            if (sourceSlot is not { IsAccessible: true, HasItem: true, GetItem: { } sourceItem })
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Transfer invoked from invalid slot ({source} - {sourceSlot})");
                return;
            }

            if (other is not IReplaceableStorage rOther) // other이 Swap을 지원하지 않는 저장소인 경우
            {
                Transfer(source, sourceSlot, other); // 단순 아이템 이동 처리
                return;
            }
            
            // Swap 시작
            // 1) other에 아이템 저장 시도, 기존에 저장되었던 아이템이 있는지 확인
            if (!rOther.TryStore(sourceItem, out var otherSlot, out var otherItem) || otherItem == null)
                return; // other에 아이템 저장을 실패했거나, otherSlot이 원래 빈 슬롯이라 Swap이 불필요한 경우 중단
            // 2) sourceSlot의 기존 아이템 제거 및 otherItem 새로 저장 시도
            if (source.TryRemoveItem(sourceSlot.Index) && source.TryStore(otherItem, sourceSlot.Index))
                return; // Swap 성공 및 매서드 종료
            
            // Swap에 실패 -> 원복 시도
            // sourceSlot: 아이템이 존재한다면 제거 후 기존 sourceItem 다시 저장
            if ((!sourceSlot.HasItem || source.TryRemoveItem(sourceSlot.Index) && source.TryStore(sourceItem, sourceSlot.Index)) 
                && other.TryRemoveItem(otherSlot.Index) // otherSlot: 새로 저장된 sourceItem 제거
                && other.TryStore(otherItem, otherSlot.Index)) // otherSlot: 기존 otherItem 다시 저장
            {
                return; // 원복 성공 시 매서드 종료
            }
            
            Logg.LogError($"[{nameof(GameItemTransfer)}] failed to roll back item from TransferOrSwap({source}, {sourceSlot}, {other}, {otherSlot})");
        }

        public void TransferOrSwap(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other, IGameItemSlot otherSlot)
        {
            Logg.Log($"[ItemUsageHandler] TransferOrSwap (<{source}, {sourceSlot}> - <{other}, {otherSlot}>) ", Logg.LoggingMode.InProgress);

            if (!(otherSlot?.HasItem ?? false)) // 도착 슬롯이 빈 슬롯이면 일방향 이동 시도
            {
                Transfer(source, sourceSlot, other, otherSlot);
                return;
            }
            
            // Swap 시작
            // 1) 양쪽 슬롯 비우기 시도
            if (!source.TryRemoveItem(sourceSlot.Index, out var sourceItem)
                || !other.TryRemoveItem(otherSlot.Index, out var otherItem))
            {
                if (sourceItem != null) // sourceSlot 비우기 실패한 경우
                    source.TryStore(sourceItem, sourceSlot.Index); // 아이템 원복 및 스왑 중단
                return;
            }

            // 2-a) otherSlot에 있던 아이템 sourceSlot에 저장 시도 
            if (!source.TryStore(otherItem, sourceSlot.Index))
            {
                // 2-a 실패 시 원복 및 스왑 중단
                if (source.TryStore(sourceItem, sourceSlot.Index) &&
                    other.TryStore(otherItem, otherSlot.Index))
                {
                    Logg.Log($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll backed", Logg.LoggingMode.Completed);
                    return;
                }
                
                Logg.LogError($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll back failed");
                return;
            }
            // 2-b) sourceSlot에 있던 아이템 otherSlot에 저장 시도 
            if (!other.TryStore(sourceItem, otherSlot.Index))
            {
                // 2-b 실패 시 원복 및 스왑 중단
                if (source.TryRemoveItem(sourceSlot.Index) &&
                    source.TryStore(sourceItem, sourceSlot.Index) &&
                    other.TryStore(otherItem, otherSlot.Index))
                {
                    Logg.Log($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll backed", Logg.LoggingMode.Completed);
                    return;
                }
                Logg.LogError($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll back failed");
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


#region Deprecated
//
// public void Transfer(IGameItemStorage source, IGameItemStorage destination, IGameItemSlot slot)
//         {
//             if (slot is not { IsAccessible: true, HasItem: true, GetItem: {} item, GetItemInfo: { } data }) return;
//
//             if (!destination.TryStore(item, out var destSlot) || 
//                 !(source.TryRemoveItem(slot.Index, out var existed) && item == existed))
//             {
//                 destSlot?.Clear();
//                 Logg.LogError($"[ItemUsageHandler] something went wrong while Transfer({source}, {destination}, {slot})");
//             }
//         }
//
//         public void Transfer(IGameItemStorage oneStorage, IGameItemStorage anotherStorage,
//             IGameItemSlot oneSlot, IGameItemSlot anotherSlot)
//         {
//             if (!anotherStorage.TryStore(oneSlot.GetItem, anotherSlot.Index))
//             {
//                 Logg.LogError($"[ItemUsageHandler] Transfer(from TransferOrSwap) - TryStore({oneSlot.GetItem}, {anotherSlot.Index}) failed");
//                 return;
//             }
//             
//             if (!oneStorage.TryRemoveItem(oneSlot.Index)) // 기존 슬롯에서 아이템 제거 시도
//             {
//                 Logg.LogError($"[ItemUsageHandler] Transfer(from TransferOrSwap) - TryRemoveItem({anotherSlot.Index}) for roll back failed");
//                 anotherStorage.TryRemoveItem(anotherSlot.Index); // 원복 시도
//             }
//         }
//         
//
//         public void TransferOrSwap(IGameItemStorage oneStorage, IGameItemStorage anotherStorage, 
//             IGameItemSlot oneSlot, IGameItemSlot anotherSlot)
//         {
//             Logg.Log($"[ItemUsageHandler] TransferOrSwap({oneStorage}, {anotherStorage}, {oneSlot}, {anotherSlot}) invoked", Logg.LoggingMode.Completed);
//
//             if (!(anotherSlot?.HasItem ?? false)) // anotherSlot(=드랍 슬롯)이 빈 슬롯이라면 단순 아이템 이동
//             {
//                 Transfer(oneStorage, oneSlot,anotherStorage, anotherSlot);
//                 // Transfer(oneStorage, anotherStorage, oneSlot, anotherSlot);
//                 return;
//             }
//             
//             if (!oneStorage.TryRemoveItem(oneSlot.Index, out var oneItem) ||
//                 !anotherStorage.TryRemoveItem(anotherSlot.Index, out var anotherItem))
//             {
//                 if (oneItem != null) // anotherSource로부터 아이템 제거에 실패한 경우
//                     oneStorage.TryStore(oneItem, oneSlot.Index); // 원복
//                 return;
//             }
//
//             if (!oneStorage.TryStore(anotherItem, oneSlot.Index))
//             {
//                 if (oneStorage.TryStore(oneItem, oneSlot.Index) &&
//                     anotherStorage.TryStore(anotherItem, anotherSlot.Index))
//                 {
//                     Logg.Log($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll backed", Logg.LoggingMode.Completed);
//                     return;
//                 }
//                 
//                 Logg.LogError($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll back failed");
//                 return;
//             }
//
//             if (!anotherStorage.TryStore(oneItem, anotherSlot.Index))
//             {
//                 if (oneStorage.TryRemoveItem(oneSlot.Index) &&
//                     oneStorage.TryStore(oneItem, oneSlot.Index) &&
//                     anotherStorage.TryStore(anotherItem, anotherSlot.Index))
//                 {
//                     Logg.Log($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll backed", Logg.LoggingMode.Completed);
//                     return;
//                 }
//                 Logg.LogError($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll back failed");
//                 return;
//             }
//         }

#endregion
