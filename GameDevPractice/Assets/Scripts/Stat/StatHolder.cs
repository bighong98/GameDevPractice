using System;
using RPG.Stats;
using UnityEngine;

namespace TH.Attribute.Stat
{
    [RequireComponent(typeof(ITypeHolder))]
    public class StatHolder : MonoBehaviour, ITypeDependent
    {
        [SerializeField] private CharacterClass characterClass;
        [SerializeField] private ProgressionSO progression;
        private int startingLevel; 
        [SerializeField] private int level;

        private ILevel levelHolder;

        private void Awake()
        {
            InitBeforeLoad();
            ResourceManager.Instance.WaitForPreLoadOnlyOnce((dum) => InitAfterLoad());
        }

        private void OnEnable()
        {
            levelHolder.OnLevelChanged += UpdateStatsByLevel;
            UpdateStatsByLevel(levelHolder.GetCurrLevel);
        }
        
        private void OnDisable()
        {
            levelHolder.OnLevelChanged -= UpdateStatsByLevel;
        }

        public void ReceiveType(ScriptableObject typeInfo)
        {
            if (typeInfo is not CharacterTypeSO charInfo) return;

            characterClass = charInfo.characterClass;
            startingLevel = level = charInfo.startingLevel;
        }

        private void InitBeforeLoad()
        {
            levelHolder = GetComponent<ILevel>();
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

