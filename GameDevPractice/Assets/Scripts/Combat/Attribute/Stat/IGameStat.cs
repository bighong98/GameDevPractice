using System;
using UnityEngine;

namespace TH.Attribute.Stat
{
    public interface IGameStat
    {
        float Value { get; }
        void AddModifier(StatModifier mod);
        bool RemoveModifier(StatModifier mod);
        bool RemoveModifiersFromSource(object source);

        event Action OnStatChanged;
    }
}
