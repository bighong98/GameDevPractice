using System;
using RPG.Attribute;
using UnityEngine;

namespace RPG.Stats
{
    public enum CharacterClass
    {
        Default, // means not initialized
        
        Player,
        EnemyTest1,
        EnemyTest2,
        EnemyTest3,
        
        Max, // means End of CharacterClass Enum (Always must be end of enum)
    }

    public enum GameStat
    {
        Health, // 최대체력
        ExperienceReward, // 경험치량(몬스터 처치 시, 플레이어에게는 없음)
        ExperienceToLevelUp, // 레벨업에 필요한 경험치 필요량
    }
    
    public class CharacterStats : MonoBehaviour
    {
        [Range(1, 99)] 
        [SerializeField] private int startingLevel = 1;
        [SerializeField] private CharacterClass characterClass;
        [SerializeField] private ProgressionSO progression;

        [SerializeField] private int currentLevel = 0;

        private Experience experience;
        private bool hasExperience;

        private void Awake()
        {
            if (GetComponent<Experience>() is { } result)
            {
                experience = result;
                hasExperience = true;
            }
        }

        private void Start()
        {
            if (experience == null) return;
            
            currentLevel = CalculateLevel();
            experience.OnExperienceGained += UpdateLevel;
        }

        public float GetStat(GameStat statType)
        {
            return progression.GetProgressionStat(statType, characterClass, startingLevel);
        }

        public int GetCurrentLevel()
        {
            if (currentLevel < 1)
            {
                currentLevel = CalculateLevel();
            }
            return currentLevel;
        }

        private void UpdateLevel(float xp)
        {
            int newLevel = CalculateLevel(xp);
            if (newLevel > currentLevel)
            {
                currentLevel = newLevel;
                Util.Log($"level up: {gameObject.name}");
            }
        }

        public int CalculateLevel()
        {
            if (!hasExperience) return startingLevel;
            
            return CalculateLevel(experience.GetCurrentXP());
        }

        private int CalculateLevel(float currentXP)
        {
            int penultimateLevel = progression.GetMaxLevel(GameStat.ExperienceToLevelUp, characterClass);
            
            for (int level = 1; level <= penultimateLevel; level++)
            {
                if (progression.GetProgressionStat(GameStat.ExperienceToLevelUp, characterClass, level) is
                        { } XPToLevelUp && XPToLevelUp > currentXP)
                {
                    return level;
                }
            }

            return penultimateLevel + 1;
        }
    }
}

