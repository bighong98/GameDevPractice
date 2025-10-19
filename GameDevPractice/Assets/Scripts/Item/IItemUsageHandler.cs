using UnityEngine;

namespace TH.Item
{
    public interface IItemUsageHandler
    {
        void Transfer(IGameItemStorage source, IGameItemStorage destination, IGameItemSlot slot); // 저장소 간 아이템 이동
        void TransferOrSwap(IGameItemStorage oneSource, IGameItemStorage anotherSource, IGameItemSlot oneSlot,
            IGameItemSlot anotherSlot); // 저장소 간 아이템 이동 (도착 슬롯 특정 및 자리 교환) //todo: 추가 파라미터로 취소/덮어쓰기/자리교환 선택하도록 변경
        void Consume(IGameItemStorage source, object destination, IGameItemSlot slot, int amount = 1);
    }
}

