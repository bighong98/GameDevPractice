using System;
using RPG.Stats;
using UnityEngine;
using TH.Utils;
using TH.Resource;

namespace TH.Attribute.Stat
{
    public class StatHolder : MonoBehaviour, IStatHolder, ITypeDependent
    {
        [SerializeField] private CharacterClass characterClass;
        [SerializeField] private ProgressionSO progression;
        private int startingLevel; 
        [SerializeField] private int level;
        
        private ILevel levelHolder;
        private bool hasMutableLevel;
        
        private void Awake()
        {
            InitBeforeLoad();
            ResourceManager.Instance.WaitForPreLoadOnlyOnce((dum) => InitAfterLoad());
        }

        private void OnEnable()
        {
            if (!hasMutableLevel || levelHolder == null) return;
            
            levelHolder.OnLevelChanged += UpdateStatsByLevel;
            UpdateStatsByLevel(levelHolder.GetCurrLevel);
        }
        
        private void OnDisable()
        {
            if (!hasMutableLevel || levelHolder == null) return;
            
            levelHolder.OnLevelChanged -= UpdateStatsByLevel;
        }

        public void ReceiveType(ScriptableObject typeInfo)
        {
            if (typeInfo is not CharacterTypeSO charInfo) return;

            characterClass = charInfo.characterClass;
            startingLevel = charInfo.startingLevel;

            // if (hasMutableLevel || levelHolder != null) return; 
            level = startingLevel;
            UpdateStatsByLevel(level);
        }

        private void InitBeforeLoad()
        {
            // levelHolder = GetComponent<ILevel>();
            if (TryGetComponent(out ILevel iLevel))
            {
                levelHolder = iLevel;
                hasMutableLevel = true;
            }
        }

        private void InitAfterLoad()
        {
            progression = ResourceManager.Instance.Load<ProgressionSO>("ProgressionSO.asset");
            if (progression == null)
                Util.LogError($"[{gameObject.name}.{nameof(StatHolder)}] failed to load progression");
        }

        private void UpdateStatsByLevel(int lv)
        {
            if (level == lv) return;
            level = lv;
            
            //todo: 레벨에 비례해 변동되는 능력치 반영
        }
        
        public float GetStat(GameStat statType)
        {
            return progression.GetProgressionStat(statType, characterClass, startingLevel);
        }

        public float GetStat(GameStat statType, int lv)
        {
            return progression.GetProgressionStat(statType, characterClass, lv);
        }
    }
}

