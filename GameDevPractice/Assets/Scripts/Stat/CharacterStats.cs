using System;
using Cysharp.Threading.Tasks;
using GameDevTV.Utils;
using RPG.Attribute;
using UnityEngine;
using TH.Core.Pool;

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
        ExperienceToLevelUp, // 레벨업에 필요한 경험치 필요량 (반드시 배열 길이가 (최대레벨-1)이어야함)
    }
    
    public class CharacterStats : MonoBehaviour
    {
        [Range(1, 99)] 
        [SerializeField] private int startingLevel = 1;
        [SerializeField] private CharacterClass characterClass;
        [SerializeField] private ProgressionSO progression;

        // 레벨 (레벨, 레벨업 이펙트 관련 기능 이관 고려)
        // [SerializeField] private int currentLevel = 0;
        private LazyValue<int> currentLevel;
        [SerializeField] private GameObject levelUpEffectPrefab;
        private bool hasLevelUpEffect;
        public event Action<int> OnLevelUp;
        
        // 경험치
        private Experience experience;
        private bool hasExperience;

        private void Awake()
        {
            if (GetComponent<Experience>() is { } result)
            {
                experience = result;
                hasExperience = true;
            }

            hasLevelUpEffect = levelUpEffectPrefab != null;
            currentLevel = new LazyValue<int>(CalculateLevel);
        }

        private void Start()
        {
            currentLevel.ForceInit();
            OnLevelUp?.Invoke(currentLevel.value);
            
            LevelUpTestMethod().Forget(); // 테스트용 매서드
        }

        private void OnEnable()
        {
            if (experience == null) return;
            experience.OnExperienceChanged += UpdateLevel;
        }

        private void OnDisable()
        {
            if (experience == null) return;
            experience.OnExperienceChanged -= UpdateLevel;
        }

        private readonly TimeSpan oneSecond = TimeSpan.FromSeconds(1);
        private async UniTaskVoid LevelUpTestMethod()
        {
            if (!gameObject.CompareTag("Player")) return;

            int count = 0;
            while (count < 5)
            {
                await UniTask.Delay(oneSecond, DelayType.DeltaTime);
                if (this == null || gameObject == null) break;
                if (hasExperience)
                {
                    count++;
                    experience.GainExperience(10);
                    Util.Log("Experience Gained", Util.LoggingMode.Completed);
                }
            }
        } 

        public float GetStat(GameStat statType)
        {
            return progression.GetProgressionStat(statType, characterClass, startingLevel);
        }

        public float GetStat(GameStat statType, int level)
        {
            return progression.GetProgressionStat(statType, characterClass, level);
        }

        #region Level

        public int GetCurrentLevel()
        {
            return currentLevel.value;
        }

        private void UpdateLevel(float xp)
        {
            int newLevel = CalculateLevel(xp);
            if (newLevel > currentLevel.value)
            {
                currentLevel.value = newLevel;
                Util.Log($"level up: {gameObject.name}.{currentLevel.value}", Util.LoggingMode.Completed);
                OnLevelUp?.Invoke(currentLevel.value);
                if (hasLevelUpEffect)
                {
                    ShowLevelUpEffect();
                }
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
                        { } xpToLevelUp && xpToLevelUp > currentXP)
                {
                    return level;
                }
            }

            return penultimateLevel + 1;
        }

        #endregion

        private void ShowLevelUpEffect()
        {
            PoolingManager.Instance.GetFromPool<SimplePooledParticlePlayer>(levelUpEffectPrefab, null, transform.position);
        }
    }
}

