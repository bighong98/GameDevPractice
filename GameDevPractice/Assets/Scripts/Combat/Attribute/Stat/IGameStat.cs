using System;
using UnityEngine;

namespace TH.Attribute.Stat
{
    public interface IGameStat
    {
        float Value { get; }
        float BaseValue { get; set; }
        
        void AddModifier(StatModifier mod);
        bool RemoveModifier(StatModifier mod);
        bool RemoveModifiersFromSource(object source);

        event Action OnStatChanged;
        event Action<float> OnStatChangedWithValue;
    }
}
