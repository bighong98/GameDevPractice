using UnityEngine;

namespace TH.UI
{
    public interface IPlayerStorageUI : 
        IStorageUI<IInvenSlotUI>, 
        IHighlightableStorageUI,
        IHoverableStorageUI,
        IClickableStorageUI,
        ISubClickableStorageUI,
        IDraggableStorageUI,
        IMutableCapacityStorageUI
    {
    
    }

    public interface IMutableCapacityStorageUI
    {
        void SetCapacity(int capa);
    }
}

