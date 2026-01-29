using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TH.Attribute.Stat;
using TH.Utils;

namespace TH.Stats
{
    [CreateAssetMenu(fileName = "ProgressionSO", menuName = "Scriptable Objects/GameStat/ProgressionSO")]
    public class ProgressionSO : ScriptableObject
    {
        [SerializeField] private ProgressionCharacterClass[] characterClasses;

        private readonly Dictionary<CharacterType, Dictionary<GameStatSO, float[]>> progressionLookup = new();

        public float GetProgressionStat(GameStatSO statType, CharacterType characterType, int level)
        {
            if (statType == null)
            {
                Logg.Log($"[{nameof(ProgressionSO)}.{nameof(GetProgressionStat)}] statType is null.");
                return 0;
            }

            InitializeLookupTable(); // lookup 테이블 초기화 시도 (최초 호출 시 초기화 후 결과 반환)

            if (progressionLookup.TryGetValue(characterType, out var progressionStats) &&
                progressionStats.TryGetValue(statType, out var levels))
            {
                if (level < 1 || levels is not { Length: { } length } || length < level) 
                {
                    Logg.Log($"[{nameof(ProgressionSO)}.{nameof(GetProgressionStat)}] failed to get stat. character: {Enum.GetName(typeof(CharacterType), characterType)}, stat: {statType?.DisplayName ?? "null"}, level: {level}");
                    return 0; // out of boundary exception 방어
                }   
                return levels[level - 1];
            }

            Logg.Log($"[{nameof(ProgressionSO)}] failed to Find Progression stat. " +
                     $"\n arguments: stat: {statType}, class: {characterType}, level: {level}");
            return 0; // 테이블에 없다면 0 반환
        }

#nullable enable
        public List<(GameStatSO, float)>? GetProgressionStats(CharacterType characterType, int level)
        {
            if (!progressionLookup.TryGetValue(characterType, out var progression))
                return null;
            
            List<(GameStatSO, float)> values = new();
            foreach (var pair in progression)
            {
                if (level >= 0 && level < pair.Value.Length)
                    values.Add((pair.Key, pair.Value[level]));
            }
            return values;
        }

        // 호출자 측에서 제공하는 리스트를 재사용하는 non-allocation 버전
        public bool GetProgressionStatsNonAlloc(CharacterType characterType, int level, List<(GameStatSO, float)> results)
        {
            if (!progressionLookup.TryGetValue(characterType, out var progression))
                return false;
                
            results.Clear();
            foreach (var pair in progression)
            {
                if (level >= 0 && level < pair.Value.Length)
                    results.Add((pair.Key, pair.Value[level]));
            }
            
            return true;
        }
#nullable restore

        public int GetMaxLevel(GameStatSO statType, CharacterType characterType)
        {
            if (statType == null)
            {
                Logg.Log($"[{nameof(ProgressionSO)}.{nameof(GetMaxLevel)}] statType is null.");
                return 0;
            }

            InitializeLookupTable();
            
            if (progressionLookup.TryGetValue(characterType, out var progressionStats) &&
                progressionStats.TryGetValue(statType, out var levels))
            {
                return levels.Length;
            }

            return 0;
        }

        private void InitializeLookupTable()
        {
            if (progressionLookup.Count > 0) return; // 이미 초기화된 상태라면 취소

            foreach (var characterProgression in characterClasses)
            {
                Dictionary<GameStatSO, float[]> progressionStatDict = new();
                foreach (var progressionStat in characterProgression.progressionStats)
                {
                    if (progressionStat.stat == null) continue;
                    progressionStatDict[progressionStat.stat] = progressionStat.levels;
                }

                if (progressionStatDict.Count == 0) continue;
                progressionLookup[characterProgression.characterType] = progressionStatDict;
            }
        }
    }
    
    [System.Serializable]
    public class ProgressionCharacterClass
    {
        [FormerlySerializedAs("characterClass")] [SerializeField] public CharacterType characterType;
        [SerializeField] public ProgressionStat[] progressionStats;
    }

    [System.Serializable]
    public class ProgressionStat
    {
        [SerializeField] public GameStatSO stat; // 스탯 종류
        [SerializeField] public float[] levels; // 레벨에 따른 스탯의 값 (levels[n]: (n+1)레벨의 스탯 값, ex- levels[0]: 1레벨 스탯 값)
    }
}

