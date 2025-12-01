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
        
        // 독립된 저장소 간 아이템 이동 및 교환
        // source, sourceSlot: 기준 저장소와 저장소 내부 슬롯
        // other: 기준 저장소로부터 아이템을 보내려는 별도의 저장소, 어느 슬롯에 저장할지는 other 인스턴스의 내부 아이템 저장 방식을 따름
        public void TransferOrSwap(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other)
        {
            Logg.Log($"[{nameof(GameItemTransfer)}] TransferOrSwap({source}, {sourceSlot}, {other})", Logg.LoggingMode.InProgress);
            
            // sourceSlot이 접근 불가하거나 비어있을 경우 중단
            if (sourceSlot is not { IsAccessible: true, HasItem: true, GetItem: { } sourceItem })
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Transfer invoked from invalid slot ({source} - {sourceSlot})");
                return;
            }
            
            // other이 Swap을 지원하지 않는 저장소인 경우 -> 단순 아이템 이동 처리
            if (other is not IReplaceableStorage rOther) 
            {
                Transfer(source, sourceSlot, other);
                return;
            }
            
            // other에 아이템 저장 시도, 저장된 슬롯과 기존 아이템 확인
            if (!rOther.TryStore(sourceItem, out var otherSlot, out var otherItem)) return; // other에 아이템 저장 실패 -> 중단
            // 기존 아이템 제거
            if (!source.TryRemoveItem(sourceSlot.Index) && !other.TryRemoveItem(otherSlot.Index))
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] failed to roll back item from TransferOrSwap({source}, {sourceSlot}, {other}, {otherSlot})");
                return;
            }
            // 저장 전 슬롯에 아이템이 있었는지 확인 -> 없다면 swap 불필요, 메서드 종료
            if (otherItem == null) return; 
            
            // sourceSlot에 otherItem 저장 (Swap)
            if (source.TryStore(otherItem, sourceSlot.Index))
                return; // Swap 성공 -> 메서드 종료
            
            // Swap 실패 -> 원복 시도
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
            Logg.Log($"[ItemUsageHandler] TransferOrSwap (<{source}, {sourceSlot}> - <{other}, {otherSlot}>) ", Logg.LoggingMode.Completed);

            if (!(sourceSlot?.HasItem ?? false)) // sourceSlot이 빈 슬롯이면 중단
            {
                Logg.LogError($"[GameItemTransfer] sourceSlot is empty - TransferOrSwap(<{source}, {sourceSlot}> - <{other}, {otherSlot}>)");
                return;
            }
            
            if (!(otherSlot?.HasItem ?? false)) // 도착 슬롯이 빈 슬롯이면 일방향 이동 시도
            {
                Transfer(source, sourceSlot, other, otherSlot);
                return;
            }
            
            // Swap 시작
            // 1-b) 양쪽 슬롯 비우기 시도
            if (!source.TryRemoveItem(sourceSlot.Index, out var sourceItem)
                || !other.TryRemoveItem(otherSlot.Index, out var otherItem))
            {
                if (sourceItem != null) // sourceSlot 비우기 실패한 경우
                    source.TryStore(sourceItem, sourceSlot.Index); // 아이템 원복 및 스왑 중단
                return;
            }

            // // 1-a) sourceItem과 otherItem이 동일한 CountableItem인 경우 -> 아이템 개수 합치기
            if (sourceItem.Type == Enums.ItemType.Countable
                && sourceItem.IsEqual(otherItem, ItemComparerExtension.ItemCompareMode.CompareData))
            {
                int sourceAmount = sourceItem.GetAmount; 
                int otherAmount = otherItem.GetAmount;
                int total = sourceAmount + otherAmount;
                int max = Mathf.Min(sourceItem.GetItemInfo.maxAmount, total);
                int remain = total - max;
                Logg.Log($"[GameItemTransfer] TransferOrSwap in progressing: (sourceAmount: {sourceAmount}, otherAmount: {otherAmount}, total: {total}, remain: {remain})", Logg.LoggingMode.Completed);
                
                ((ICountableItem)otherItem).SetAmount(max);
                if (!other.TryStore(otherItem, otherSlot.Index))
                {
                    if (!source.TryStore((ICountableItem)sourceItem, sourceSlot.Index))
                    {
                        Logg.LogError($"[{nameof(GameItemTransfer)}]ItemSwap failed and roll back failed - \n TransferOrSwap(<{source}, {sourceSlot}> - <{other}, {otherSlot}>)");
                        return;
                    }
                }

                if (remain <= 0) return; 
                ((ICountableItem)sourceItem).SetAmount(remain);
                if (source.TryStore(sourceItem, sourceSlot.Index)) return;
                
                ((ICountableItem)sourceItem).SetAmount(sourceAmount);
                ((ICountableItem)otherItem).SetAmount(otherAmount);
                if (source.TryRemoveItem(sourceSlot.Index)
                    && other.TryRemoveItem(otherSlot.Index)
                    && source.TryStore(sourceItem, sourceSlot.Index)
                    && other.TryStore(otherItem, otherSlot.Index)) return;
                
                Logg.LogError($"[{nameof(GameItemTransfer)}]ItemSwap failed and roll back failed - \n TransferOrSwap(<{source}, {sourceSlot}> - <{other}, {otherSlot}>)");
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
    }
}
