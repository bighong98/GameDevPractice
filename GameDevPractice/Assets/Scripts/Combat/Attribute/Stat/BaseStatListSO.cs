using System;
using System.Collections.Generic;
using UnityEngine;


namespace TH.Attribute.Stat
{
    [CreateAssetMenu(fileName = "BaseStatListSO", menuName = "Scriptable Objects/GameStat/BaseStatListSO")]
    public class BaseStatListSO : ScriptableObject
    {
        public List<BaseStat> list;
    }

    [Serializable]
    public struct BaseStat // GameStat 초기화에 사용
    {
        public GameStatSO type; // 스탯 타입
        public float value; // 초기 값
    }
}

