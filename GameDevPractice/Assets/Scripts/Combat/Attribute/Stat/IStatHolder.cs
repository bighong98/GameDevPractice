using System;

namespace TH.Attribute.Stat
{
    public interface IStatHolder
    {
        IGameStat GetStat(GameStatSO statType);
        bool TryGetStat(GameStatSO statType, out IGameStat stat);
        float GetStatForLevel(GameStatSO statType, int level);
        bool AddModifier(GameStatSO type, StatModifier mod);
        bool RemoveModifier(GameStatSO type, StatModifier mod);
        bool RemoveModifier(object source);

        IGameStat BindEvent(GameStatSO type, Action action, bool pending = true);
        void UnBindEvent(GameStatSO type, Action action);
        void BindStatChanged(GameStatSO statSO, Action<float> callback, bool pending = true);
        void UnbindStatChanged(GameStatSO statSO, Action<float> callback);
    }
}

