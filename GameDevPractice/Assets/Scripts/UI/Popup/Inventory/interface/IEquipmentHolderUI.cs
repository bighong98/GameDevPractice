using UnityEngine;
namespace TH.UI
{
    public interface IEquipmentHolderUI : 
        IStorageUI<IEquipmentSlotUI>, 
        IHighlightableStorageUI,
        IHoverableStorageUI,
        IClickableStorageUI,
        ISubClickableStorageUI,
        IDraggableStorageUI
    {
    
    }
}

