using System;
using System.Collections.Generic;
using TH.Utils;
using UnityEngine;

namespace TH.Attribute.Stat
{
    [CreateAssetMenu(fileName = "StatCorrelationMapSO", menuName = "Scriptable Objects/GameStat/StatCorrelationMapSO")]
    public class StatCorrelationMapSO : KeyValueListSO<GameStatSO, List<StatInfluenceData>>
    {
        
    }

    [Serializable]
    public struct StatInfluenceData
    {
        public GameStatSO source;
        public StatModCalcType calculation;
        public float valuePerPoint;

        public StatModifier GetModifier(float sourceStatValue, object source = null)
        {
            return new StatModifier(sourceStatValue * valuePerPoint, calculation, source);
        }
    }
}
