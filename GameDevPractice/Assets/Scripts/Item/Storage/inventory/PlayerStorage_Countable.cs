using System;
using TH.Resource;

namespace TH.Item
{
    // 수량형 아이템 누적/합산 규칙 위임 partial
    public sealed partial class PlayerStorage
    {
        #region ICountableItemStorage

        // 수량형 타입별 총량 변경 알림 이벤트
        public event Action<ItemTypeSO, int> OnCountableAmountModified;

        // 수량형 아이템 저장 요청 위임
        public bool TryStoreCountable(ICountableItem countableItem, int amount, out int excess)
            => countableService.TryStoreCountable(countableItem, amount, out excess);

        // 지정 슬롯 기준 수량형 저장 요청 위임
        public bool TryStoreCountable(ICountableItem countableItem, int amount, int index, out int excess)
            => countableService.TryStoreCountable(countableItem, amount, index, out excess);

        // 지정 슬롯 기준 수량형 증분 저장 요청 위임
        public bool TryAddCountable(ICountableItem countableItem, int amount, int index, out int excess)
            => countableService.TryAddCountable(countableItem, amount, index, out excess);

        // 수량형 인스턴스 기준 총량 조회 위임
        public bool TryGetCountableAmount(ICountableItem countableItem, out int amount)
            => countableService.TryGetCountableAmount(countableItem, out amount);

        // 타입 기준 총량 조회 위임
        public bool TryGetCountableAmount(ItemTypeSO itemInfo, out int amount)
            => countableService.TryGetCountableAmount(itemInfo, out amount);

        // 스택 병합 요청 위임
        public bool TryMergeStacks(int fromIndex, int toIndex)
            => countableService.TryMergeStacks(fromIndex, toIndex);

        // 수량형 캐시 재구축 위임
        private void RebuildCountableCache()
        {
            countableService.RebuildCountableCache();
        }

        // 수량 캐시 증감 반영 래퍼
        private void UpdateCountableDict(ItemTypeSO data, int delta)
        {
            countableCache.ApplyDelta(data, delta);
        }

        // 총량 변경 이벤트 발행 래퍼
        private void RaiseCountableAmountModified(ItemTypeSO data, int amount)
        {
            OnCountableAmountModified?.Invoke(data, amount);
        }

        #endregion
    }
}
