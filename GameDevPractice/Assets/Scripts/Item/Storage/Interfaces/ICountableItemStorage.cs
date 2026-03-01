using System;
using TH.Resource;

namespace TH.Item
{
    // 아이템 타입: Countable(ItemTypeSO.Type) 아이템 저장을 지원하는 저장소 클래스에 구현
    // 개수(amount)를 특정하여 저장, 부분 저장(excess로 초과분 확인) 등 지원
    public interface ICountableItemStorage
    {
        // Countable 아이템 저장 메서드
        // (아이템 인스턴스 자체 개수) * amount 값으로 전체 개수를 계산하는 방식 사용할 것
        // excess로 저장하고 남은 개수를 반환 (0이면 전부 저장 성공)
        // 1개라도 저장에 성공했다면 true 반환
        // IGameItemStorage와 ICountableItemStorage를 동시에 구현할 경우 
        // - IGameItemStorage.TryStore 사용 시 내부적으로 아이템 타입 체크
        // - TryStoreCountable 경유하는 방식으로 구현 권장
        bool TryStoreCountable(ICountableItem countableItem, int amount, out int excess);
        bool TryStoreCountable(ICountableItem countableItem, int amount, int index, out int excess);
        
        // 이미 동일한 아이템이 있는 슬롯에 같은 Countable 아이템 개수 추가 (빈 슬롯에는 사용 불가능)
        bool TryAddCountable(ICountableItem countableItem, int amount, int index, out int excess);
        
        // 동일 Countable 아이템 슬롯 간 개수 병합 (toIndex 슬롯에 최대개수까지 옮기기)
        // Sort(), Trim() 등 구현 시에도 사용
        bool TryMergeStacks(int fromIndex, int toIndex);
        
        // 특정 Countable 아이템의 개수 확인 (저장소 내에 존재하지 않는 아이템이라면 false 반환)
        bool TryGetCountableAmount(ICountableItem countableItem, out int amount);
        bool TryGetCountableAmount(ItemTypeSO itemInfo, out int amount);

        event Action<ItemTypeSO, int> OnCountableAmountModified;
    }
}

