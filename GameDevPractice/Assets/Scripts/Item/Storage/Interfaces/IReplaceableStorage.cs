using UnityEngine;

namespace TH.Item
{
    public interface IReplaceableStorage
    {
        bool TryReplace(IGameItem item, out IGameItem existing);
        bool TryReplace(IGameItem item, out IGameItemSlot storedSlot, out IGameItem existing);
        bool TryReplaceAt(IGameItem item, int index, out IGameItem existing);
        bool TryTakeOut(int index, out IGameItem item); // 아이템 제거 후 제거된 아이템 확인 (아이템 이동 등에 사용)
    }
}

