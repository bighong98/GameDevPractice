
using TH.Utils;

namespace TH.Item
{
    public class GameItemTransfer : IGameItemTransfer
    {
        // source 저장소의 slot 에 있는 아이템을 destination 으로 이동.
        // source == destination: true -> 동작하나 독립 Controller에서 처리 권장
        public void Transfer(IGameItemStorage source, IGameItemSlot slot, IGameItemStorage destination)
        {
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: { } item })
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Transfer invoked from invalid slot ({source} - {slot})");
                return;
            }

            int srcIndex = slot.Index;

            // 1) 출발지(source)에서 꺼내기
            if (!source.TryRemoveItem(srcIndex, out var taken) ||
                !item.IsEqual(taken, ItemComparerExtension.ItemCompareMode.CompareInstance))
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Item is missing or changed while Transfer() from ({source}, idx {srcIndex})");
                return;
            }

            // 2) 도착지(destination)에 저장 시도
            if (!destination.TryStore(taken))
            {
                // 실패 시 원복 시도
                if (!source.TryStore(taken, srcIndex))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Transfer rollback failed. Item may be lost. ({source}, idx {srcIndex})");
            }
        }


        // sourceSlot -> otherSlot 로 아이템 이동 (같은 저장소/다른 저장소 모두 허용)

        public void Transfer(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other, IGameItemSlot otherSlot)
        {
            if (sourceSlot is not { IsAccessible: true, HasItem: true, GetItem: { } current })
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Transfer invoked from invalid slot ({source} - {sourceSlot})");
                return;
            }

            int srcIndex = sourceSlot.Index;
            int dstIndex = otherSlot.Index;

            // 같은 저장소인 경우 IRearrangeableStorage.TryTransferItem 으로 아이템 이동 처리
            if (ReferenceEquals(source, other) && source is IRearrangeableStorage rearr)
            {
                if (!rearr.TryTransferItem(sourceSlot, otherSlot))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Internal Transfer via IRearrangeableStorage failed ({srcIndex} -> {dstIndex})");
                return;
            }

            // 서로 다른 저장소인 경우 기존 슬롯에서 아이템 제거
            if (!source.TryRemoveItem(srcIndex, out var taken) ||
                !current.IsEqual(taken, ItemComparerExtension.ItemCompareMode.CompareInstance))
            {
                // 기존 슬롯 아이템 제거 실패 혹은 제거된 아이템이 원래 아이템이 아니라면 에러 메시지 호출
                Logg.LogError($"[{nameof(GameItemTransfer)}] Item is missing while Transfer ({source}, idx {srcIndex})");
                return;
            }

            if (!other.TryStore(taken, dstIndex))
            {
                // 저장 실패 -> 원래 자리로 원복 시도
                if (!source.TryStore(taken, srcIndex))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Transfer rollback failed (cross-storage, indexed). Item may be lost.");
            }
        }

        // sourceSlot 의 아이템을 other 저장소로 보냄
        // 저장 위치는 other의 내부 저장 규칙에 의해 결정됨
        // 동일 저장소 내 슬롯 간 이동/교환도 가능하나 독립 컨트롤러에서 처리를 권장 
        public void TransferOrSwap(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other)
        {
            Logg.Log($"[{nameof(GameItemTransfer)}] TransferOrSwap({source}, {sourceSlot}, {other})", Logg.LoggingMode.InProgress);

            if (sourceSlot is not { IsAccessible: true, HasItem: true, GetItem: { } sourceItem })
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] TransferOrSwap invoked from invalid slot ({source} - {sourceSlot})");
                return;
            }

            // other 가 replace 정책을 가진 저장소인 경우
            if (other is IReplaceableStorage rOther)
            {
                if (!rOther.TryStore(sourceItem, out var otherSlot, out var otherItem))
                {
                    // 수용할 슬롯 자체를 못 찾은 경우
                    Logg.Log($"[{nameof(GameItemTransfer)}] TransferOrSwap failed to store item into target storage ({other})");
                    return;
                }

                int srcIndex  = sourceSlot.Index;
                int destIndex = otherSlot.Index;

                // 출발지에서 제거
                if (!source.TryRemoveItem(srcIndex, out var removed) ||
                    !sourceItem.IsEqual(removed, ItemComparerExtension.ItemCompareMode.CompareInstance))
                {
                    // 제거 실패 → 방금 other 에 저장한 것 롤백
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Failed to remove source item during TransferOrSwap. Rolling back target slot...");

                    other.TryRemoveItem(destIndex);
                    if (otherItem != null && !other.TryStore(otherItem, destIndex))
                        Logg.LogError($"[{nameof(GameItemTransfer)}] Rollback of target slot failed during TransferOrSwap");

                    return;
                }

                // 기존 otherItem 이 없었다면 단순 이동 끝
                if (otherItem == null) return;

                // 기존 otherItem 을 source 로 되돌리며 사실상 Swap
                if (source.TryStore(otherItem, srcIndex))
                    return; // Swap 성공

                // Swap 실패 → 전체 롤백
                Logg.LogError($"[{nameof(GameItemTransfer)}] Swap failed, trying rollback...");

                bool restoredSource = source.TryStore(sourceItem, srcIndex);

                other.TryRemoveItem(destIndex);               // 현재 들어가 있는 sourceItem 제거
                bool restoredOther  = other.TryStore(otherItem, destIndex);

                if (!restoredSource || !restoredOther)
                    Logg.LogError($"[{nameof(GameItemTransfer)}] failed to roll back item from TransferOrSwap({source}, {sourceSlot}, {other}, {otherSlot})");

                return;
            }

            // IReplaceableStorage 가 아니면 그냥 단방향 이동
            Transfer(source, sourceSlot, other);
        }

        // (source, sourceSlot) <-> (other, otherSlot) 간 Swap (또는 Countable 한정 개수 병합)
        // 내부 (source == other : true 인 경우) 아이템 이동도 가능하나 가능하면 독립 컨트롤러에서 처리 권장
        public void TransferOrSwap(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other, IGameItemSlot otherSlot)
        {
            Logg.Log($"[{nameof(GameItemTransfer)}] TransferOrSwap (<{source}, {sourceSlot}> - <{other}, {otherSlot}>) ",
                Logg.LoggingMode.Completed);

            // 기본 유효성 검증
            if (!(sourceSlot?.HasItem ?? false))
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] sourceSlot is empty - TransferOrSwap(<{source}, {sourceSlot}> - <{other}, {otherSlot}>)");
                return;
            }

            if (!(otherSlot?.HasItem ?? false))
            {
                // 도착 슬롯이 비어있으면 단방향 이동
                Transfer(source, sourceSlot, other, otherSlot);
                return;
            }

            int fromIdx = sourceSlot.Index;
            int toIdx   = otherSlot.Index;

            // 같은 저장소 내부 처리 
            if (ReferenceEquals(source, other))
            {
                // 1) 동일 Countable 이면 병합 시도 → ICountableItemStorage에 위임
                if (source is ICountableItemStorage countableStorage &&
                    sourceSlot is
                    {
                        IsAccessible: true,
                        HasItem: true,
                        GetItem: { Type: Enums.ItemType.Countable } sItem
                    } &&
                    otherSlot is
                    {
                        IsAccessible: true,
                        HasItem: true,
                        GetItem: { Type: Enums.ItemType.Countable } oItem
                    } &&
                    sItem.IsEqual(oItem, ItemComparerExtension.ItemCompareMode.CompareData))
                {
                    if (!countableStorage.TryMergeStacks(fromIdx, toIdx))
                        Logg.LogError($"[{nameof(GameItemTransfer)}] TryMergeStacks failed on same storage ({fromIdx} -> {toIdx})");
                    return;
                }

                // 2) 그 외에는 IRearrangeableStorage 가 있으면 내부 재배치/스왑으로 처리
                if (source is IRearrangeableStorage rearr)
                {
                    if (!rearr.TryTransferItem(sourceSlot, otherSlot))
                        Logg.LogError($"[{nameof(GameItemTransfer)}] Internal TransferOrSwap via IRearrangeableStorage failed");
                    return;
                }
            }

            // 일반 Swap (내부/외부 공통)

            // 1) 양쪽 슬롯에서 아이템 꺼내기
            if (!source.TryRemoveItem(fromIdx, out var sourceItem)
                ||!other.TryRemoveItem(toIdx, out var otherItem))
            {
                // 하나라도 실패한 경우, 성공한 쪽만이라도 원복 시도
                if (sourceItem != null)
                    source.TryStore(sourceItem, fromIdx);

                return;
            }

            // 2) otherItem -> sourceSlot 저장 시도
            if (!source.TryStore(otherItem, fromIdx))
            {
                // 실패 시 둘 다 원복 시도
                bool rollback =
                    source.TryStore(sourceItem, fromIdx) &&
                    other.TryStore(otherItem, toIdx);

                if (!rollback)
                    Logg.LogError($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll back failed (step 2)");

                return;
            }

            // 3) sourceItem -> otherSlot 저장 시도
            if (!other.TryStore(sourceItem, toIdx))
            {
                // 실패 시 전체 원복
                bool rollback =
                    source.TryRemoveItem(fromIdx) &&
                    source.TryStore(sourceItem, fromIdx) &&
                    other.TryStore(otherItem, toIdx);

                if (!rollback)
                    Logg.LogError($"[{nameof(GameItemTransfer)}] ItemSwap failed and roll back failed (step 3)");
            }
        }
    }
}

