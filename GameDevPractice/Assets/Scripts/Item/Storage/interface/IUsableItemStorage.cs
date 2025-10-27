using System;
using UnityEngine;

namespace TH.Item
{
    // 내부 아이템을 사용(소비, 장착/장착해제 등)하는 것이 허가된 스토리지
    public interface IUsableItemStorage : IGameItemStorage
    {
        event Action<IGameItemSlot> OnItemTryUsed; 
        bool TryStoreAndUse(IGameItem item, object user = null); // 저장과 동시에 아이템 사용 시도
        bool TryStoreAndUse(IGameItem item, int index, object user = null); // + 저장할 슬롯 특정
    }
}

