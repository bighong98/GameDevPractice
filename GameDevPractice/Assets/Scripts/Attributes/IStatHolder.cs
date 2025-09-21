using RPG.Stats;
using UnityEngine;

namespace TH.Attribute.Stat
{
    public interface IStatHolder
    {
        float GetStat(GameStat statType);
        float GetStat(GameStat statType, int level);
    }
}

