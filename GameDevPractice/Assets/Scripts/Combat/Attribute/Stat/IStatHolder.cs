using System;

namespace TH.Attribute.Stat
{
    public interface IStatHolder
    {
        GameStat GetStat(StatTypeSO statType);
        float GetStat(StatTypeSO statType, int level);
        bool AddModifier(StatTypeSO type, StatModifier mod);
        bool RemoveModifier(StatTypeSO type, StatModifier mod);
        bool RemoveModifier(object source);
        void BindEvent(StatTypeSO type, Action action);
        void UnBindEvent(StatTypeSO type, Action action);
    }
}

