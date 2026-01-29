using System;
using System.Collections.Generic;
using TH.Utils;
using UnityEngine;


namespace TH.Attribute.Stat
{
    [CreateAssetMenu(fileName = "BaseStatListSO", menuName = "Scriptable Objects/GameStat/BaseStatListSO")]
    public class BaseStatListSO : KeyValueListSO<GameStatSO, float>
    {
        
    }
}

