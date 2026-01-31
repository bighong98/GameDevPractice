using System;

namespace TH.Attribute.Stat
{
    public interface IStatHolder
    {
        IGameStat GetStat(GameStatSO statType);
        float GetStat(GameStatSO statType, int level);
        bool AddModifier(GameStatSO type, StatModifier mod);
        bool RemoveModifier(GameStatSO type, StatModifier mod);
        bool RemoveModifier(object source);

        IGameStat BindEvent(GameStatSO type, Action action);
        void UnBindEvent(GameStatSO type, Action action);
        void BindStatChanged(GameStatSO statSO, Action<float> callback);
        void UnbindStatChanged(GameStatSO statSO, Action<float> callback);
    }
}

