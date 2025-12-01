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
    }
}

