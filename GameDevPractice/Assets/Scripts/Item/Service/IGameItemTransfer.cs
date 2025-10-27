using UnityEngine;

namespace TH.Item
{
    public interface IGameItemTransfer
    {
        // 다른 저장소에 아이템 이동 (해당 저장소 인스턴스의 아이템 저장 규칙대로)
        void Transfer(IGameItemStorage source, IGameItemSlot slot, IGameItemStorage destination);
        // 다른 저장소 특정 슬롯으로 아이템 이동
        void Transfer(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other, IGameItemSlot otherSlot);
        // 다른 저장소에 아이템 이동 + 양쪽 다 빈 슬롯이 아니면 스왑
        void TransferOrSwap(IGameItemStorage source, IGameItemSlot slot, IGameItemStorage destination);
        // 다른 저장소 특정 슬롯으로 아이템 이동 + 양쪽 다 빈 슬롯이 아니면 스왑
        void TransferOrSwap(IGameItemStorage source, IGameItemSlot sourceSlot,
            IGameItemStorage other, IGameItemSlot otherSlot);
        
        // deprecated
        // void Transfer(IGameItemStorage source, IGameItemStorage destination, IGameItemSlot slot); // 저장소 간 아이템 이동
        //
        // void Transfer(IGameItemStorage oneStorage, IGameItemStorage anotherStorage, 
        //     IGameItemSlot oneSlot, IGameItemSlot anotherSlot);
        // void TransferOrSwap(IGameItemStorage oneStorage, IGameItemStorage anotherStorage, IGameItemSlot oneSlot,
        //     IGameItemSlot anotherSlot); // 저장소 간 아이템 이동 (도착 슬롯 특정 및 자리 교환) //todo: 추가 파라미터로 취소/덮어쓰기/자리교환 선택하도록 변경
        
        void Consume(IGameItemStorage source, object destination, IGameItemSlot slot, int amount = 1);
    }
}

