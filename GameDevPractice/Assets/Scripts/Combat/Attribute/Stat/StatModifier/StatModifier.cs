using UnityEngine;

namespace TH.Attribute.Stat
{
    public enum StatModCalcType
    {
        // 별도의 순서 지정이 없으면 Add -> PerAdd -> PerMul 순서로 계산됨
        Add, // 고정값 합연산
        PerAdd, // 퍼센트 합연산
        PerMul, // 퍼센트 곱연산
    }
    public class StatModifier
    {
        public readonly float Value;
        public readonly StatModCalcType Type;
        public int Order;
        public readonly object Source;

        public StatModifier(float value, StatModCalcType type, int order = 0, object source = null)
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

