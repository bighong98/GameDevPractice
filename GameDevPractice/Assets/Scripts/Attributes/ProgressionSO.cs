using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TH.Attribute.Stat;

namespace RPG.Stats
{
    [CreateAssetMenu(fileName = "ProgressionSO", menuName = "Scriptable Objects/ProgressionSO")]
    public class ProgressionSO : ScriptableObject
    {
        [SerializeField] private ProgressionCharacterClass[] characterClasses;

        private readonly Dictionary<CharacterType, Dictionary<GameStats, float[]>> characterProgressionLookup = new();

        public float GetProgressionStat(GameStats stats, CharacterType characterType, int level)
        {
            InitializeLookupTable(); // lookup 테이블 초기화 시도 (최초 호출 시 초기화 후 결과 반환)

            if (characterProgressionLookup.TryGetValue(characterType, out var progressionStats) &&
                progressionStats.TryGetValue(stats, out var levels))
            {
                if (level < 1 || levels is not { Length: { } length } || length < level) 
                {
                    Util.Log($"[{nameof(ProgressionSO)}.{nameof(GetProgressionStat)}] failed to get stat. character: {Enum.GetName(typeof(CharacterType), characterType)}, stat: {Enum.GetName(typeof(GameStats), stats)}, level: {level}");
                    return 0; // out of boundary exception 방어
                }   
                return levels[level - 1];
            }

            Util.Log($"[{nameof(ProgressionSO)}] failed to Find Progression stat. " +
                     $"\n arguments: stat: {stats}, class: {characterType}, level: {level}");
            return 0; // 테이블에 없다면 0 반환
        }

        public int GetMaxLevel(GameStats stats, CharacterType characterType)
        {
            if (characterProgressionLookup.TryGetValue(characterType, out var progressionStats) &&
                progressionStats.TryGetValue(stats, out var levels))
            {
                return levels.Length;
            }

            return 0;
        }

        private void InitializeLookupTable()
        {
            if (characterProgressionLookup.Count > 0) return; // 이미 초기화된 상태라면 취소

            foreach (var characterProgression in characterClasses)
            {
                Dictionary<GameStats, float[]> progressionStatDict = new();
                foreach (var progressionStat in characterProgression.progressionStats)
                {
                    progressionStatDict[progressionStat.stat] = progressionStat.levels;
                }

                if (progressionStatDict.Count == 0) continue;
                characterProgressionLookup[characterProgression.characterType] = progressionStatDict;
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
        [SerializeField] public GameStats stat; // 스탯 종류
        [SerializeField] public float[] levels; // 레벨에 따른 스탯의 값 (levels[n]: (n+1)레벨의 스탯 값, ex- levels[0]: 1레벨 스탯 값)
    }
}

