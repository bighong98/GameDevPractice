using UnityEngine;

namespace TH.Item
{
    public interface IRearrangeableStorage
    {
        bool TryTransferItem(IGameItemSlot from, IGameItemSlot to); // from 슬롯에 보관된 아이템을 to 슬롯으로 이동, to슬롯이 빈 슬롯이 아닐 경우 처리는 구현 클래스에서 결정
    }
}

