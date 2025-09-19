using UnityEngine;
using System;

namespace TH.Attribute
{
    public interface ILevel
    {
        event Action<int> OnLevelChanged;
        int GetCurrLevel { get; }
        void SetLevel(int level);
    }
}

