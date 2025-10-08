using System;
using RPG.Stats;
using UnityEngine;

namespace TH.Attribute.Stat
{
    public interface IStatHolder
    {
        GameStat GetStat(GameStats statType);
        // float GetStat(GameStats statType);
        float GetStat(GameStats statType, int level);
        bool AddModifier(GameStats type, StatModifier mod);
        bool RemoveModifier(GameStats type, StatModifier mod);
        bool RemoveModifier(object source);
        void BindEvent(GameStats type, Action action);
        void UnBindEvent(GameStats type, Action action);
    }
}

