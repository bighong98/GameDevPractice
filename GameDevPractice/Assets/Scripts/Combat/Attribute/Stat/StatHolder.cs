using System;
using System.Collections.Generic;
using TH.Core.Service;
using TH.Stats;
using TH.Item;
using UnityEngine;
using TH.Utils;
using TH.Resource;

namespace TH.Attribute.Stat
{
    public class StatHolder : MonoBehaviour, IStatHolder, ITypeDependent
    {
        [SerializeField] private CharacterType characterType;
        [SerializeField] private ProgressionSO progression; // serialize for debug
        
        private Dictionary<GameStats, GameStat> stats = new Dictionary<GameStats, GameStat>();
        
        private int startingLevel; 
        [SerializeField] private int level; // serialize for debug
        private ILevel levelHolder;
        private bool hasMutableLevel;

        private IEquipHandler equipHandler;
        private bool hasEquipHandler;
        
        private void Awake()
        {
            InitBeforeLoad();
            ResourceManager.Instance.WaitForPreLoadOnlyOnce(InitAfterLoad);
        }

        private void OnEnable()
        {
            if (equipHandler != null)
            {
                equipHandler.OnEquipmentChanged += this.OnEquipmentChanged;
            }
            
            if (!hasMutableLevel || progression == null) return; 
            
            levelHolder.OnLevelChanged += UpdateStatsByLevel;
            UpdateStatsByLevel(levelHolder.GetCurrLevel);
        }
        
        private void OnDisable()
        {
            if (equipHandler != null)
            {
                equipHandler.OnEquipmentChanged += this.OnEquipmentChanged;
            }
            
            if (!hasMutableLevel || progression == null) return; 
            
            levelHolder.OnLevelChanged -= UpdateStatsByLevel;
        }

        #region Initialization

        private void InitBeforeLoad()
        {
            // if (TryGetComponent(out ILevel iLevel))
            // {
            //     levelHolder = iLevel;
            //     hasMutableLevel = true;
            // }
            
            // if (TryGetComponent(out IEquipHandler iEquipHandler))
            //     equipHandler = iEquipHandler;

            hasMutableLevel = TryGetComponent(out ILevel iLevel);
            if (hasMutableLevel) levelHolder = iLevel;

            hasEquipHandler = TryGetComponent(out IEquipHandler iEquipHandler);
            if (hasEquipHandler) equipHandler = iEquipHandler;
        }

        private void InitAfterLoad()
        {
            Logg.Log($"[StatHolder] InitAfterLoad() invoked", Logg.LoggingMode.Completed);
            progression = ResourceManager.Instance.Load<ProgressionSO>("ProgressionSO.asset");
            if (progression == null)
                Logg.LogError($"[{gameObject.name}.{nameof(StatHolder)}] failed to load progression");
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
        
        private void InitializeStats(ScriptableObject baseStatData) // call by ReceiveType()
        {
            if (baseStatData == null || 
                baseStatData is not BaseStatListSO { list: { } baseStats }) return;
            foreach (var baseStat in baseStats)
            {
                stats[baseStat.type] = new GameStat(baseStat.value);
            }
        }

        #endregion
        

        private void UpdateStatsByLevel(int lv)
        {
            if (level == lv) return;
            level = lv;
            
            //todo: 레벨에 비례해 변동되는 능력치 반영
        }

        #region Get Stat

#nullable enable
        public GameStat? GetStat(GameStats statType)
        {
            if (stats.TryGetValue(statType, out var stat))
            {
                return stat;
            }
            
            Logg.LogError($"[{gameObject.name}] Invalid stat type requested: {statType}. Available stats: {string.Join(", ", stats.Keys)}");
            return null;
        }

        public float GetStat(GameStats statType, int lv)
        {
            Logg.Log($"[from '{gameObject.name}'] GetStat({statType}, {characterType}, {lv})", Logg.LoggingMode.Completed);
            return progression.GetProgressionStat(statType, characterType, lv);
        }
#nullable restore

        #endregion

        #region Update Stat (Apply Stat Modifier)

        public bool AddModifier(GameStats type, StatModifier mod)
        {
            if (!stats.TryGetValue(type, out var stat)) return false;
            
            stat.AddModifier(mod);
            Logg.Log($"[{gameObject.name}.{nameof(StatHolder)}.{nameof(AddModifier)}] '{type}' is changed to ({stat.Value})", Logg.LoggingMode.Completed);
            return true;
        }

        public bool RemoveModifier(GameStats type, StatModifier mod)
        {
            if (!stats.TryGetValue(type, out var stat)) return false;
            
            stat.RemoveModifier(mod);
            return true;
        }

        public bool RemoveModifier(object source)
        {
            foreach (var stat in stats.Values)
            {
                stat.RemoveModifiersFromSource(source);
            }

            return true;
        }

        #endregion
        
        public void BindEvent(GameStats type, Action action)
        {
            if (!stats.TryGetValue(type, out var stat))
            {
                Logg.Log($"[{gameObject.name}.{nameof(StatHolder)}] failed to bind event to stat '{type}'");
                return;
            }

            stat.OnStatChanged += action;
        }
        
        public void UnBindEvent(GameStats type, Action action)
        {
            if (!stats.TryGetValue(type, out var stat))
            {
                Logg.Log($"[{gameObject.name}.{nameof(StatHolder)}] failed to bind event to stat '{type}'");
                return;
            }

            stat.OnStatChanged -= action;
        }


        #region Handle Events

        private void OnEquipmentChanged(object sender, EquipArgs args)
        {
            //todo: sender 검사
            if (args.Item is not IEquipmentItem equipment)
            {
                Logg.LogError($"[{gameObject.name}.StatHolder] Empty EquipArgs delivered");
                return;
            }

            switch (args.State)
            {
                case EquipArgs.EquipEventState.Equip:
                    AddModifiersFromEquipment(equipment);
                    break;
                case EquipArgs.EquipEventState.UnEquip:
                    RemoveModifiersFromEquipment(equipment);
                    break;
                default:
                    Logg.LogError($"[{gameObject.name}.OnEquipmentChanged] Invalid EquipEventState");
                    break;
            }
        }

        private void AddModifiersFromEquipment(IEquipmentItem equipment)
        {
            if (equipment.EquipmentStats is not { } equipmentStats)
            {
                Logg.LogError($"[{gameObject.name}.StatHolder] Empty Equipment StatModifier Data from '{equipment}'");
                return;
            }

            foreach (var statData in equipmentStats)
            {
                AddModifier(statData.type, statData.GetModifier(equipment));
            }
        }

        private void RemoveModifiersFromEquipment(IEquipmentItem equipment)
        {
            RemoveModifier(equipment);
        }

        #endregion
        
        
        
    }
}

