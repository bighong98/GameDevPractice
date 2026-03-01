using System;
using UnityEngine;

namespace TH.UI
{
    public interface IDraggableStorageUI
    {
        event Action<int> OnSlotDragged;
        event Action<int> OffSlotDragged;
    }
}

