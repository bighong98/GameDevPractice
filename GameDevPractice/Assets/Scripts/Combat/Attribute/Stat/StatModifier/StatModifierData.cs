using System;

namespace TH.Attribute.Stat
{
    [Serializable]
    public struct StatModifierData
    {
        public StatTypeSO type;
        public StatModCalcType calculation;
        public float value;

        public StatModifier GetModifier(object caller = null)
        {
            return new StatModifier(value: value, type: calculation, source: caller);
        }
    }
}
