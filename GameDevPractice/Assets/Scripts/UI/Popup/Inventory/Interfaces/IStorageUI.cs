using System.Collections;
using System.Collections.Generic;
using TH.Item;

namespace TH.UI
{
    public interface IStorageUI
    { 
        IEnumerable Slots { get; }
        void DrawSlot(int index, IGameItem instance);
        void CleanSlot(int index);
        void ShowSlot(int index);
        void HideSlot(int index);
    }
    public interface IStorageUI<out T> : IStorageUI where T : ISlotUI
    {
        new IReadOnlyCollection<T> Slots { get; }
    }
}


