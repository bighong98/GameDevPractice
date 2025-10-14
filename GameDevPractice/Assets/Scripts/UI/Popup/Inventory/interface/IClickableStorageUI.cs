using System;
using UnityEngine;

namespace TH.UI
{
    public interface IClickableStorageUI
    {
        event Action<int> OnSlotClicked;
    }
}

