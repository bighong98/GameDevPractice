using System;
using System.Collections.Generic;
using RPG.Stats;
using UnityEngine;
using TH.Utils;
using TH.Resource;

namespace TH.Attribute.Stat
{
    public class StatHolder : MonoBehaviour, IStatHolder, ITypeDependent
    {
        [SerializeField] private CharacterType characterType;
        [SerializeField] private ProgressionSO progression; // serialize for debug
        
        private int startingLevel; 
        [SerializeField] private int level;
        private ILevel levelHolder;
        private bool hasMutableLevel;

        private Dictionary<GameStats, GameStat> stats = new Dictionary<GameStats, GameStat>();
        
        private void Awake()
        {
            InitBeforeLoad();
            ResourceManager.Instance.WaitForPreLoadOnlyOnce(InitAfterLoad);
        }

        private void OnEnable()
        {
            if (!hasMutableLevel || progression == null) return; 
            
            levelHolder.OnLevelChanged += UpdateStatsByLevel;
            UpdateStatsByLevel(levelHolder.GetCurrLevel);
        }
        
        private void OnDisable()
        {
            if (!hasMutableLevel || progression == null) return; 
            
            levelHolder.OnLevelChanged -= UpdateStatsByLevel;
        }

        public void ReceiveType(ScriptableObject typeInfo)
        {
            if (typeInfo == null || typeInfo is not CharacterTypeSO charInfo) return;

            characterType = charInfo.characterType;
            startingLevel = charInfo.startingLevel;
            
            InitializeStats(charInfo.characterBaseStats);

            if (!hasMutableLevel || progression == null) return; 
            level = startingLevel;
            UpdateStatsByLevel(level);
        }

        private void InitBeforeLoad()
        {
            if (TryGetComponent(out ILevel iLevel))
            {
                levelHolder = iLevel;
                hasMutableLevel = true;
            }
        }

        private void InitAfterLoad()
        {
            Logg.Log($"[StatHolder] InitAfterLoad() invoked", Logg.LoggingMode.Completed);
            progression = ResourceManager.Instance.Load<ProgressionSO>("ProgressionSO.asset");
            if (progression == null)
                Logg.LogError($"[{gameObject.name}.{nameof(StatHolder)}] failed to load progression");
        }

        private void InitializeStats(ScriptableObject baseStatData)
        {
            if (baseStatData == null || 
                baseStatData is not BaseStatListSO { list: { } baseStats }) return;
            foreach (var baseStat in baseStats)
            {
                stats[baseStat.type] = new GameStat(baseStat.value);
            }
        }

        private void UpdateStatsByLevel(int lv)
        {
            if (level == lv) return;
            level = lv;
            
            //todo: 레벨에 비례해 변동되는 능력치 반영
        }
        
        public GameStat GetStat(GameStats statType)
        {
            // return progression.GetProgressionStat(statType, characterType, startingLevel);
            if (stats.TryGetValue(statType, out var stat))
            {
                return stat;
            }
            
            Logg.LogError($"[{gameObject.name}] trying to get invalid stat type: {statType}");
            return null;
        }
        
        // public float GetStat(GameStats statType)
        // {
        //     // return progression.GetProgressionStat(statType, characterType, startingLevel);
        //     if (stats.TryGetValue(statType, out var stat))
        //     {
        //         return stat.Value;
        //     }
        //     
        //     Util.LogError($"[{gameObject.name}] trying to get invalid stat type: {statType}");
        //     return 0;
        // }

        public float GetStat(GameStats statType, int lv)
        {
            Logg.Log($"[from '{gameObject.name}'] GetStat({statType}, {characterType}, {lv})", Logg.LoggingMode.Completed);
            return progression.GetProgressionStat(statType, characterType, lv);
        }
    }
}

