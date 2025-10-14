using System.Collections.Generic;

namespace TH.UI
{
    public interface IStorageUI<out T> where T : ISlotUI
    {
        IReadOnlyCollection<T> Slots { get; }

        void DrawSlot(int index, object data);
        void ShowSlot(int index);
        void HideSlot(int index);
    }
}


