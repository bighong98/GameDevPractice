using System.Collections;
using System.Collections.Generic;

namespace TH.UI
{
    public interface IStorageUI
    { 
        IEnumerable Slots { get; }
        void DrawSlot(int index, object data);
        void ShowSlot(int index);
        void HideSlot(int index);
    }
    public interface IStorageUI<out T> : IStorageUI where T : ISlotUI
    {
        new IReadOnlyCollection<T> Slots { get; }
    }
}


