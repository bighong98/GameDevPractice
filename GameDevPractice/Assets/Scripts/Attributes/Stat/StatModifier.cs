using UnityEngine;
using StatModCalcType = Enums.StatModCalcType;

namespace TH.Attribute.Stat
{
    public class StatModifier
    {
        public readonly float Value;
        public readonly StatModCalcType Type;
        public readonly int Order;
        public readonly object Source;

        public StatModifier(float value, StatModCalcType type, int order, object source = null)
        {
            Value = value;
            Type = type;
            Order = order;
            Source = source;
        }
    
        public StatModifier(float value, StatModCalcType type) : this (value, type, (int) type, null) {} 
        public StatModifier(float value, StatModCalcType type, object source) : this (value, type, (int)type, source) {}

    }
}

