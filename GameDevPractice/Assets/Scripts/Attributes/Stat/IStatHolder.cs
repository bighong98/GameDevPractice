using RPG.Stats;
using UnityEngine;

namespace TH.Attribute.Stat
{
    public interface IStatHolder
    {
        GameStat GetStat(GameStats statType);
        // float GetStat(GameStats statType);
        float GetStat(GameStats statType, int level);
    }
}

