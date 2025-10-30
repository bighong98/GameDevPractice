using UnityEngine;

namespace TH.UI
{
    public readonly struct DragSlotInfo
    {
        public readonly (IDraggableStorageUI source, int index) From;
        public readonly (IDraggableStorageUI source, int index) To;

        public DragSlotInfo(IDraggableStorageUI from, int fIdx, IDraggableStorageUI to, int tIdx)
        {
            From = (from, fIdx);
            To = (to, tIdx);
        }
    }
}

