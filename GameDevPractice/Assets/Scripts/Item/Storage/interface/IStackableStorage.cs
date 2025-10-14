using UnityEngine;

namespace TH.Item
{
    public interface IStackableStorage
    {
        bool TryStore(IGameItem item, int amount, out int excess); // 동일한 아이템을 {amount}개 보관, 초과분 존재할 경우 excess로 반환
    }
}

