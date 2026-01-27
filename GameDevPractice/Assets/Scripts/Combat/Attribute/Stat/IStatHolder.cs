using System;

namespace TH.Attribute.Stat
{
    public interface IStatHolder
    {
        GameStat GetStat(GameStatSO statType);
        float GetStat(GameStatSO statType, int level);
        bool AddModifier(GameStatSO type, StatModifier mod);
        bool RemoveModifier(GameStatSO type, StatModifier mod);
        bool RemoveModifier(object source);
        void BindEvent(GameStatSO type, Action action);
        void UnBindEvent(GameStatSO type, Action action);
    }
}

