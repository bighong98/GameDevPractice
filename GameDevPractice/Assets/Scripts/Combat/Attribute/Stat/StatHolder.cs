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

        private readonly Dictionary<int, GameStat> statIdMap = new();
        private Dictionary<GameStatSO, GameStat> stats = new Dictionary<GameStatSO, GameStat>();
        public IReadOnlyDictionary<GameStatSO, GameStat> Stats => stats;

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
                baseStatData is not BaseStatListSO { Items: { } baseStats }) return;
            foreach (var baseStat in baseStats)
            {
                if (baseStat.key is not { } statSO || statSO.IsNull())
                {
                    Logg.LogWarning($"[{gameObject.name}.{nameof(StatHolder)}] base stat type is null");
                    continue;
                }
                
                var gameStat = new GameStat(baseStat.value);
                AddNewStat(statSO, gameStat);
            }
        }

        private void AddNewStat(GameStatSO statSO, GameStat gameStat)
        {
            stats[statSO] = gameStat;
            statIdMap[statSO.LegacyId] = gameStat;
            RegisterStat(statSO, gameStat);
            AddEditorStatList(statSO, gameStat);
        }

        #endregion


        private readonly List<(GameStatSO, float)> progressionStatBuffer = new();
        private void UpdateStatsByLevel(int lv)
        {
            if (level == lv) return;
            level = lv;
            
            // 레벨에 비례해 변동되는 능력치 반영
            // ProgressionSO.asset 으로부터 캐릭터의 타입/레벨에 해당하는 데이터 받아오기
            if (progression == null) return;
            if (!progression.GetProgressionStatsNonAlloc(characterType, lv, progressionStatBuffer)
                || progressionStatBuffer.Count == 0) return;
            // 받아온 데이터 캐릭터 능력치의 기본값(BaseValue)에 반영
            foreach ((var statSO, var statValue) in progressionStatBuffer)
            {
                // notice: Progression.asset 데이터에는 있지만 캐릭터 능력치 목록에 없는 경우 강제로 추가함
                if (GetOrAddStat(statSO) is not {} stat) continue;
                stat.BaseValue = statValue;
            }
            
        }

        #region Get Stat

#nullable enable
        // statData 기반으로 능력치 조회 + 등록된 능력치가 없을 경우 임의의 기본값으로 생성 및 추가 
        // (해당 GameStatSO가 해당 캐릭터에게 유효하다는 확신이 있는 경우에만 사용)
        private GameStat? GetOrAddStat(GameStatSO statData, float defaultValue = 0f)
        {
            if (statData == null)
            {
                Logg.LogError($"[{gameObject.name}] Null stat type requested - scene: {gameObject.scene.name}", this);
                return null;
            }

            int id = statData.LegacyId;
            if (!statIdMap.ContainsKey(id))
            {
                var newStat = new GameStat(defaultValue);
                AddNewStat(statData, newStat);
            }

            return GetStat(statData);
        }

        public GameStat? GetStat(GameStatSO statData)
        {
            if (statData == null)
            {
                Logg.LogError($"[{gameObject.name}] Null stat type requested - scene: {gameObject.scene.name}", this);
                return null;
            }

            if (statIdMap.TryGetValue(statData.LegacyId, out var stat))
                return stat;
            
            Logg.LogWarning($"[{gameObject.name}] Invalid stat type requested: {statData} - scene: {gameObject.scene.name}", this);
            return null;
        }

        public float GetStat(GameStatSO statType, int lv)
        {
            Logg.Log($"[from '{gameObject.name}'] GetStat({statType}, {characterType}, {lv})", Logg.LoggingMode.Completed);
            return progression.GetProgressionStat(statType, characterType, lv);
        }
#nullable restore

        #endregion

        #region Update Stat (Apply Stat Modifier)

        public bool AddModifier(GameStatSO type, StatModifier mod)
        {
            if (type.IsNull()) return false;
            if (!statIdMap.TryGetValue(type.LegacyId, out var stat)) return false;
            
            stat.AddModifier(mod);
            Logg.Log($"[{gameObject.name}.{nameof(StatHolder)}.{nameof(AddModifier)}] '{type}' is changed to ({stat.Value})", Logg.LoggingMode.Completed);
            return true;
        }

        public bool RemoveModifier(GameStatSO type, StatModifier mod)
        {
            if (type.IsNull()) return false;
            if (!statIdMap.TryGetValue(type.LegacyId, out var stat)) return false;

            stat.RemoveModifier(mod);
            return true;
        }

        public bool RemoveModifier(object source)
        {
            foreach (var stat in statIdMap.Values)
            {
                stat.RemoveModifiersFromSource(source);
            }

            return true;
        }

        #endregion
        
        #region bind/unbind stat event

        public void BindEvent(GameStatSO type, Action action)
        {
            if (type.IsNull() || !statIdMap.TryGetValue(type.LegacyId, out var stat))
            {
                Logg.Log($"[{gameObject.name}.{nameof(StatHolder)}] failed to bind event to stat '{type}'");
                return;
            }

            stat.OnStatChanged += action;
        }
        
        public void UnBindEvent(GameStatSO type, Action action)
        {
            if (type.IsNull() || !statIdMap.TryGetValue(type.LegacyId, out var stat))
            {
                Logg.Log($"[{gameObject.name}.{nameof(StatHolder)}] failed to bind event to stat '{type}'");
                return;
            }

            stat.OnStatChanged -= action;
        }

        // 아직 생성되지 않은 스탯을 기다리는 대기 콜백 목록
        private Dictionary<GameStatSO, List<Action<float>>> pendingListeners = new();

        // 스탯 이벤트 구독 (외부 호출용)
        public void BindStatChanged(GameStatSO statSO, Action<float> callback)
        {
            if (statSO == null || callback == null) return;

            // Case 1: 스탯이 이미 존재하는 경우 -> 즉시 연결
            if (stats.TryGetValue(statSO, out var stat))
            {
                stat.OnStatChangedWithValue += callback;
                callback.Invoke(stat.Value); // 현재 값으로 초기화 보장
            }
            // Case 2: 스탯이 아직 없는 경우 -> 대기 명단(Pending)에 등록
            else
            {
                if (!pendingListeners.ContainsKey(statSO))
                    pendingListeners[statSO] = new List<Action<float>>();
                
                pendingListeners[statSO].Add(callback);
                
                // 주의: 스탯이 없으므로 invokeImmediately가 true여도 
                // 현재 값을 줄 수 없음. 생성될 때 호출되기를 기다려야 함.
            }
        }

        // 스탯 이벤트 구독 해제 
        public void UnbindStatChanged(GameStatSO statSO, Action<float> callback)
        {
            if (statSO == null) return;

            // 이미 존재하는 스탯에서 해제
            if (stats.TryGetValue(statSO, out var stat))
            {
                stat.OnStatChangedWithValue -= callback;
            }

            // 대기 명단에서도 제거 (아직 생성 안 됐는데 리스너가 죽는 경우 대비)
            if (pendingListeners.TryGetValue(statSO, out var list))
            {
                list.Remove(callback);
                if (list.Count == 0) pendingListeners.Remove(statSO);
            }
        }

        // 실제로 스탯이 생성(초기화)되는 시점에 호출
        // TypeHolder에서 데이터를 받아 스탯을 생성한 직후 이 메서드를 불러줘야 함
        private void RegisterStat(GameStatSO statSO, GameStat newStat)
        {
            // 대기 중이던 리스너가 있다면 일괄 연결 및 처리
            if (!pendingListeners.TryGetValue(statSO, out var callbacks)) return;
            
            foreach (var callback in callbacks)
            {
                newStat.OnStatChangedWithValue += callback; // 이벤트 연결
                callback.Invoke(newStat.Value);    // 초기값 동기화 (Health의 MaxHp 갱신)
            }
            // 대기 목록에서 제거 
            pendingListeners.Remove(statSO);
        }

        #endregion

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
        
        #region Editor Methods
#if UNITY_EDITOR
        [SerializeField] private List<SerializablePair<GameStatSO, GameStat>> editorStats;

        private void AddEditorStatList(GameStatSO statSO, GameStat stat)
        {
            editorStats.Add(new SerializablePair<GameStatSO, GameStat>(statSO, stat));
        }

#endif
        #endregion
        
    }
}

