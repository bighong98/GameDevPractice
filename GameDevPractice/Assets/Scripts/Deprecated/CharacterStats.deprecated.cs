using System;
using Cysharp.Threading.Tasks;
using GameDevTV.Utils;
using RPG.Attribute;
using TH.Attribute;
using UnityEngine;
using TH.Core.Pool;
using TH.Attribute.Stat;
using UnityEngine.Serialization;

namespace RPG.Stats
{
    [RequireComponent(typeof(CharacterTypeHolder))]
    public class CharacterStats : MonoBehaviour
    {
        [Range(1, 99)] 
        [SerializeField] private int startingLevel = 1;
        [FormerlySerializedAs("characterClass")] [SerializeField] private CharacterType characterType;
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

        private PlayerExperience playerExp;

        private void Awake()
        {
            if (GetComponent<Experience>() is { } result)
            {
                experience = result;
                hasExperience = true;
            }

            if (TryGetComponent(out PlayerExperience pExp))
            {
                playerExp = pExp;
            }

            hasLevelUpEffect = levelUpEffectPrefab != null;
            currentLevel = new LazyValue<int>(CalculateLevel);
        }

        private void Start()
        {
            Init().ContinueWith(() =>
            {
                currentLevel.ForceInit();
                OnLevelUp?.Invoke(currentLevel.value);
                // LevelUpTestMethod().Forget();
            });
        }

        private void OnEnable()
        {
            // if (experience == null) return;
            // experience.OnExperienceChanged += UpdateLevel;
            if (playerExp != null)
            {
                playerExp.OnLevelChanged += UpdateLevel;
            }
        }

        private void OnDisable()
        {
            // if (experience == null) return;
            // experience.OnExperienceChanged -= UpdateLevel;
            if (playerExp != null)
            {
                playerExp.OnLevelChanged -= UpdateLevel;
            }
        }

        private async UniTask Init()
        {
            if (GetComponent<CharacterTypeHolder>() is not { } typeHolder) return;
            
            var charInfo = await typeHolder.GetTypeAsync();
            characterType = charInfo.characterType;
            startingLevel = charInfo.startingLevel;
        }

        

        public float GetStat(GameStats stats)
        {
            return progression.GetProgressionStat(stats, characterType, startingLevel);
        }

        public float GetStat(GameStats stats, int level)
        {
            return progression.GetProgressionStat(stats, characterType, level);
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
                OnLevelUp?.Invoke(currentLevel.value);
                // if (hasLevelUpEffect)
                // {
                //     ShowLevelUpEffect();
                // }
            }
        }

        private void UpdateLevel(int level)
        {
            if (level > currentLevel.value)
            {
                currentLevel.value = level;
                //todo: 레벨에 영향을 받는 스탯 변경
            }
        }

        public int CalculateLevel()
        {
            if (!hasExperience) return startingLevel;
            
            return CalculateLevel(experience.GetCurrentXP());
        }

        private int CalculateLevel(float currentXP)
        {
            int penultimateLevel = progression.GetMaxLevel(GameStats.ExperienceToLevelUp, characterType);
            
            for (int level = 1; level <= penultimateLevel; level++)
            {
                if (progression.GetProgressionStat(GameStats.ExperienceToLevelUp, characterType, level) is
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
            // PoolingManager.Instance.GetFromPool<SimplePooledParticlePlayer>(levelUpEffectPrefab, null, transform.position);
            PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(levelUpEffectPrefab, null, transform.position);
        }
    }
}

