using UnityEngine;
using TH.Resource;

namespace TH.Control
{
    public interface IInteractable
    {
        void Interact(); // 상호작용의 결과를 구현
        Transform Trs { get; }
    }

    public interface IDropItem : IInteractable
    {
        ItemTypeSO ItemData { get; }
        int Amount { get; }
        bool UseImmediately { get; }
    }
}


