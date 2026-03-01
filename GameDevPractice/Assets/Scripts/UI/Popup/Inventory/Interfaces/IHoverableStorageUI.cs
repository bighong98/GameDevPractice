using System;
using UnityEngine;
namespace TH.UI
{
    public interface IHoverableStorageUI
    {
        event Action<int> OnSlotHovered;
        event Action<int> OffSlotHovered;
    }
}

