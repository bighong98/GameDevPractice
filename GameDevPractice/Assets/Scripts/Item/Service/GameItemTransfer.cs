
using TH.Item.Storage;
using TH.Utils;

namespace TH.Item
{
    public class GameItemTransfer : IGameItemTransfer
    {
        #region Private Helper Methods

        
        // 슬롯 유효성 검사
        private bool ValidateSlotWithItem(IGameItemSlot slot, IGameItemStorage storage)
        {
            if (slot is { IsAccessible: true, HasItem: true, GetItem: not null })
                return true;

            Logg.LogError($"[{nameof(GameItemTransfer)}] invoked from invalid slot ({storage} - {slot})");
            return false;
        }

        
        // 슬롯이 접근 가능한지 검증 (아이템 여부 체크하지 않음)
        private bool ValidateSlotAccessible(IGameItemSlot slot, IGameItemStorage storage)
        {
            if (slot is { IsAccessible: true })
                return true;

            Logg.LogWarning($"[{nameof(GameItemTransfer)}] target slot is invalid ({storage})");
            return false;
        }

        // IReplaceableStorage 타입 체크 및 에러 로깅
        private bool TryGetReplaceableStorage(IGameItemStorage storage, out IReplaceableStorage replaceableStorage)
        {
            if (storage is IReplaceableStorage rs)
            {
                replaceableStorage = rs;
                return true;
            }

            replaceableStorage = null;
            Logg.LogWarning($"[{nameof(GameItemTransfer)}] requires source to implement IReplaceableStorage. ({storage})");
            return false;
        }

        /// 슬롯에서 아이템을 꺼내고 예상 아이템과 일치하는지 검증
        private bool TryTakeOutAndVerify(IReplaceableStorage storage, int slotIndex, IGameItem expectedItem, out IGameItem takenItem)
        {
            if (storage.TryTakeOut(slotIndex, out takenItem) &&
                expectedItem.IsEqual(takenItem, ItemComparerExtension.ItemCompareMode.CompareInstance))
            {
                return true;
            }

            Logg.LogWarning($"[{nameof(GameItemTransfer)}] Item is missing or changed (idx {slotIndex})");
            return false;
        }

        // 아이템을 원래 슬롯으로 롤백 시도
        private void TryRollbackToSlot(IReplaceableStorage storage, IGameItem item, int slotIndex)
        {
            if (!storage.TryReplaceAt(item, slotIndex, out _))
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] rollback failed. Item may be lost. (idx {slotIndex})");
            }
        }

        // IReferenceStorage에 아이템 저장 시도 (인덱스 지정 가능)
        private bool TryStoreToReferenceStorage(IGameItemStorage storage, IGameItem item, int? slotIndex = null)
        {
            if (storage is not IReferenceStorage referenceDest)
                return false;

            if (slotIndex.HasValue)
                referenceDest.TryStoreReference(item, slotIndex.Value);
            else
                referenceDest.TryStoreReference(item);

            return true;
        }

        
        /// Swap 로직에서 destExisting을 원래 위치로 롤백 시도
        private void RollbackDestSlot(IGameItemStorage destination, IGameItem destExisting, IGameItem sourceItem, int destIndex)
        {
            if (destExisting != null)
            {
                if (destination is not IReplaceableStorage replaceDest)
                {
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Cannot rollback - destination does not implement IReplaceableStorage");
                    return;
                }

                if (!replaceDest.TryReplaceAt(destExisting, destIndex, out var rollbackTaken))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Rollback of target slot failed");
                else if (!sourceItem.IsEqual(rollbackTaken, ItemComparerExtension.ItemCompareMode.CompareInstance))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Rollback target slot item mismatch");
            }
            else if (!destination.TryRemoveItem(destIndex))
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Cannot rollback to empty slot without explicit API. Check IReplaceableStorage implementation.");
            }
        }

        // Swap 로직 구현
        // source와 destination 저장소 양쪽이 IReplaceableStorage를 구현한 경우에만 사용 가능
        private bool TrySwap(IReplaceableStorage replaceSource, IReplaceableStorage replaceDest,
            int sourceSlotIndex, int destSlotIndex, out bool rollbackAttempted)
        {
            rollbackAttempted = false;

            // 1) sourceSlot에서 아이템을 꺼내기
            if (!replaceSource.TryTakeOut(sourceSlotIndex, out var sourceItem))
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] Failed to take out source item for Swap (idx {sourceSlotIndex})");
                return false;
            }

            // 2) destSlot에서 아이템을 꺼내기
            if (!replaceDest.TryTakeOut(destSlotIndex, out var destItem))
            {
                // source 롤백
                if (!replaceSource.TryReplaceAt(sourceItem, sourceSlotIndex, out _))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Swap rollback failed on source after destination take-out failure");
                rollbackAttempted = true;
                return false;
            }

            // 3) destItem -> sourceSlot에 저장
            if (!replaceSource.TryReplaceAt(destItem, sourceSlotIndex, out var sExisting) || sExisting != null)
            {
                // 예상치 못한 기존 아이템이 존재하거나 실패한 경우 -> 전체 롤백
                Logg.LogError($"[{nameof(GameItemTransfer)}] Unexpected existing item on source slot during Swap, rolling back...");

                bool rollbackSource = replaceSource.TryReplaceAt(sourceItem, sourceSlotIndex, out _);
                bool rollbackDest = replaceDest.TryReplaceAt(destItem, destSlotIndex, out _);

                if (!rollbackSource || !rollbackDest)
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Swap rollback failed (source-existing)");

                rollbackAttempted = true;
                return false;
            }

            // 4) sourceItem -> destSlot에 저장
            if (!replaceDest.TryReplaceAt(sourceItem, destSlotIndex, out var oExisting) || oExisting != null)
            {
                Logg.LogWarning($"[{nameof(GameItemTransfer)}] Unexpected existing item on destination slot during Swap, rolling back");

                // 4 실패 시 -> 양쪽 다시 꺼내고 롤백 시도
                if (!replaceSource.TryTakeOut(sourceSlotIndex, out _))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Swap rollback: failed to take out destItem from source");

                if (!replaceDest.TryReplaceAt(destItem, destSlotIndex, out _))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Swap rollback: failed to restore destItem to destination");

                if (!replaceSource.TryReplaceAt(sourceItem, sourceSlotIndex, out _))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Swap rollback: failed to restore sourceItem to source");

                rollbackAttempted = true;
                return false;
            }

            // Swap 성공
            return true;
        }

        #endregion

        // source 저장소의 slot 에 있는 아이템을 destination 으로 이동
        // source == destination: true -> 동작하나 독립 Controller에서 처리 권장
        public void Transfer(IGameItemStorage source, IGameItemSlot slot, IGameItemStorage destination)
        {
            if (!ValidateSlotWithItem(slot, source)) 
                return;

            var item = slot.GetItem;

            if (TryStoreToReferenceStorage(destination, item))
                return;

            if (!TryGetReplaceableStorage(source, out var replaceSource))
                return;

            int srcIndex = slot.Index;

            // 1) 출발지(source)에서 꺼내기
            if (!TryTakeOutAndVerify(replaceSource, srcIndex, item, out var taken))
                return;

            // 2) 도착지(destination)에 저장 시도
            if (!destination.TryStore(taken))
            {
                // 실패 시 원복 시도
                TryRollbackToSlot(replaceSource, taken, srcIndex);
            }
        }

        // sourceSlot -> otherSlot 로 아이템 이동
        // 같은 저장소/다른 저장소 모두 허용 (가능하다면 같은 저장소 내부 아이템 이동은 별도의 컨트롤러에서 로직 분리 권장)
        public void Transfer(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage destination, IGameItemSlot destSlot)
        {
            if (!ValidateSlotWithItem(sourceSlot, source))
                return;

            if (!ValidateSlotAccessible(destSlot, destination))
                return;

            var sourceItem = sourceSlot.GetItem;
            int sourceSlotIndex = sourceSlot.Index;
            int destSlotIndex = destSlot.Index;

            // 같은 저장소인 경우 IRearrangeableStorage.TryTransferItem 으로 저장소 내부 아이템 이동 처리
            if (ReferenceEquals(source, destination) && source is IRearrangeableStorage reArrangeStorage)
            {
                if (!reArrangeStorage.TryTransferItem(sourceSlot, destSlot))
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Internal Transfer via IRearrangeableStorage failed ({sourceSlotIndex} -> {destSlotIndex})");
                return;
            }

            if (TryStoreToReferenceStorage(destination, sourceItem, destSlotIndex))
                return;

            // 서로 다른 저장소인 경우
            // destination 저장소의 destSlot에 sourceItem 저장 가능 여부 검사
            if (!destination.CanStore(sourceItem, destSlotIndex))
            {
                // 저장 불가능한 아이템인 경우 or dest가 아이템 저장 불가 상태인 경우 중지
                return;
            }

            if (!TryGetReplaceableStorage(source, out var rSource))
                return;

            // 출발지에서 꺼내기
            if (!TryTakeOutAndVerify(rSource, sourceSlotIndex, sourceItem, out var taken))
                return;

            // 도착지의 지정 슬롯에 저장
            if (!destination.TryStore(taken, destSlotIndex))
            {
                // 저장 실패 -> 원래 자리로 원복 시도
                TryRollbackToSlot(rSource, taken, sourceSlotIndex);
            }
        }

        // sourceSlot 의 아이템을 other 저장소로 보냄
        // 저장 위치는 other의 내부 저장 규칙에 의해 결정됨
        // 동일 저장소 내 슬롯 간 이동/교환도 가능하나 독립 컨트롤러에서 처리를 권장
        public void TransferOrSwap(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage destination)
        {
            this.Log($"TransferOrSwap({source}, {sourceSlot}, {destination})", Logg.LoggingMode.Completed);

            if (!ValidateSlotWithItem(sourceSlot, source))
                return;

            var sourceItem = sourceSlot.GetItem;
            int srcIndex = sourceSlot.Index;

            // 1) Swap 가능 (양쪽 모두 IReplaceableStorage 구현)
            if (source is IReplaceableStorage replaceSource && destination is IReplaceableStorage replaceDest)
            {
                // dest 쪽에서 교체 가능한 슬롯을 하나 찾으면서 기존 아이템을 확보
                if (!replaceDest.TryReplace(sourceItem, out var storedSlot, out var destExisting))
                {
                    // 수용할 슬롯 자체를 못 찾은 경우
                    Logg.Log($"[{nameof(GameItemTransfer)}] TransferOrSwap failed to store item into target storage ({destination})");
                    return;
                }

                int destIndex = storedSlot.Index;

                // 출발지에서 실제 아이템 꺼내기
                if (!TryTakeOutAndVerify(replaceSource, srcIndex, sourceItem, out var removed))
                {
                    // 제거 실패 -> 방금 dest 에 저장한 것 롤백
                    Logg.LogWarning($"[{nameof(GameItemTransfer)}] Failed to remove source item during TransferOrSwap. Rolling back target slot");
                    RollbackDestSlot(destination, destExisting, sourceItem, destIndex);
                    return;
                }

                // 기존 dest에 아이템이 없었다면 단순 이동으로 종료
                if (destExisting == null)
                    return;

                // destExisting 을 sourceSlot에 저장 (Swap)
                // Swap 성공 시 종료
                if (replaceSource.TryReplaceAt(destExisting, srcIndex, out var srcExisting) && srcExisting == null)
                    return;

                // Swap 실패 -> 전체 롤백 시도
                Logg.LogWarning($"[{nameof(GameItemTransfer)}] Swap failed, trying rollback");
                bool restoredSource = replaceSource.TryReplaceAt(removed, srcIndex, out _);

                // dest 롤백: 현재 destIndex 슬롯 에 있는 sourceItem 을 제거하고 destExisting 복원
                if (!replaceDest.TryReplaceAt(destExisting, destIndex, out var rollbackTaken2) ||
                    !sourceItem.IsEqual(rollbackTaken2, ItemComparerExtension.ItemCompareMode.CompareInstance))
                {
                    Logg.LogError($"[{nameof(GameItemTransfer)}] Rollback of target slot failed during TransferOrSwap (Swap branch)");
                }

                if (!restoredSource)
                    Logg.LogError($"[{nameof(GameItemTransfer)}] failed to roll back item from TransferOrSwap({source}, {sourceSlot}, {destination})");

                return;
            }

            if (TryStoreToReferenceStorage(destination, sourceItem))
                return;

            // 2) Swap 불가한 경우
            // -> Swap 하지 않고, CanStore 정책 확인 후 단방향 Transfer만 수행
            if (!destination.CanStore(sourceItem))
                return;

            if (!TryGetReplaceableStorage(source, out var replaceableSource))
                return;

            if (!TryTakeOutAndVerify(replaceableSource, srcIndex, sourceItem, out var taken))
                return;

            if (!destination.TryStore(taken))
            {
                // 실패 시 원복 시도
                TryRollbackToSlot(replaceableSource, taken, srcIndex);
            }
        }

        // (source, sourceSlot) <-> (other, otherSlot) 간 Swap (또는 Countable 한정 개수 병합)
        // 내부 (source == other : true 인 경우) 아이템 이동도 가능하나 가능하면 독립 컨트롤러에서 처리 권장
        public void TransferOrSwap(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage destination, IGameItemSlot destSlot)
        {
            Logg.Log($"[{nameof(GameItemTransfer)}] TransferOrSwap (<{source}, {sourceSlot}> - <{destination}, {destSlot}>) ",
                Logg.LoggingMode.Completed);

            // 기본 유효성 검증
            if (!(sourceSlot?.HasItem ?? false))
            {
                Logg.LogError($"[{nameof(GameItemTransfer)}] sourceSlot is empty - TransferOrSwap(<{source}, {sourceSlot}> - <{destination}, {destSlot}>)");
                return;
            }

            // 도착 슬롯이 비어있으면 단방향 이동
            if (!(destSlot?.HasItem ?? false))
            {
                Transfer(source, sourceSlot, destination, destSlot);
                return;
            }

            int sourceSlotIndex = sourceSlot.Index;
            int destSlotIndex = destSlot.Index;

            // 1) 같은 저장소 내부 처리
            if (ReferenceEquals(source, destination))
            {
                // 1-a) 동일 Countable 이면 병합 시도 → ICountableItemStorage에 위임
                if (source is ICountableItemStorage countableStorage &&
                    sourceSlot is
                    {
                        IsAccessible: true,
                        HasItem: true,
                        GetItem: { Type: Enums.ItemType.Countable } sourceItem
                    } &&
                    destSlot is
                    {
                        IsAccessible: true,
                        HasItem: true,
                        GetItem: { Type: Enums.ItemType.Countable } destItem
                    } &&
                    sourceItem.IsEqual(destItem, ItemComparerExtension.ItemCompareMode.CompareData))
                {
                    if (!countableStorage.TryMergeStacks(sourceSlotIndex, destSlotIndex))
                        Logg.LogError($"[{nameof(GameItemTransfer)}] TryMergeStacks failed on same storage ({sourceSlotIndex} -> {destSlotIndex})");
                    return;
                }

                // 1-b) 그 외에는 IRearrangeableStorage 가 있으면 내부 재배치/스왑으로 처리
                if (source is IRearrangeableStorage rearr)
                {
                    if (!rearr.TryTransferItem(sourceSlot, destSlot))
                        Logg.LogError($"[{nameof(GameItemTransfer)}] Internal TransferOrSwap via IRearrangeableStorage failed");
                    return;
                }
            }

            // 2) 양쪽 모두 IReplaceableStorage 를 구현했다면 -> IReplaceableStorage 기반 Swap
            if (source is IReplaceableStorage replaceSource && destination is IReplaceableStorage replaceDest)
            {
                TrySwap(replaceSource, replaceDest, sourceSlotIndex, destSlotIndex, out _);
                return;
            }

            // 3) 둘 중 하나라도 IReplaceableStorage 가 아닌 경우
            // -> Swap 하지 않고, CanStore 정책 확인 후 단방향 Transfer만 수행
            if (!ValidateSlotWithItem(sourceSlot, source))
                return;

            var srcItem = sourceSlot.GetItem;

            if (TryStoreToReferenceStorage(destination, srcItem, destSlotIndex))
                return;

            // destination이 아이템 추가 저장 가능 여부 확인
            // 저장슬롯이 전부 차있고, 스토리지 내부 규칙상 덮어쓰기 불가한 경우 등이 해당
            if (!destination.CanStore(srcItem, destSlotIndex))
                return; 

            if (!TryGetReplaceableStorage(source, out var repSource))
                return;

            // 단방향 이동 처리
            if (!TryTakeOutAndVerify(repSource, sourceSlotIndex, srcItem, out var moved))
                return;

            if (!destination.TryStore(moved, destSlotIndex))
            {
                // 실패 시 원복
                TryRollbackToSlot(repSource, moved, sourceSlotIndex);
            }
        }

        public bool CanTransfer(IGameItemSlot sourceSlot, IGameItemStorage destination)
        {
            if (sourceSlot is not { IsAccessible: true, IsValid: true, GetItem: { } sourceItem })
                return false;

            return destination.CanStore(sourceItem);
        }

        public bool CanTransfer(IGameItemSlot sourceSlot, IGameItemStorage destination, IGameItemSlot destSlot)
        {
            if (sourceSlot is not { IsAccessible: true, IsValid: true, GetItem: { } sourceItem })
                return false;

            return destination.CanStore(sourceItem, destSlot?.Index ?? -1); // destSlot: null 이면 무조건 실패하도록 -1
        }
    }
}
