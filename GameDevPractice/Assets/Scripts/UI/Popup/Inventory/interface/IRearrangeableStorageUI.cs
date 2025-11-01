using System;
using UnityEngine;

namespace TH.UI
{
    public interface IRearrangeableStorageUI
    {
        event Action OnSortButtonPressed;
        event Action OnTrimButtonPressed;
    }
}

