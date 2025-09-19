using System;
using GameDevTV.Utils;
using RPG.Saving;
using RPG.Stats;
using TH.Core.Pool;
using UnityEngine;

namespace TH.Attribute
{
    public class PlayerExperience : MonoBehaviour, IExperience, ILevel, ISavable, ITypeDependent
    {
        public event Action<float> OnXpChanged;
        public event Action<int> OnLevelChanged;

        public int GetCurrLevel { get; private set; }
        public float GetCurrXp { get; private set; }

        private LazyValue<int> currentLevel;
        private float currentXp;

        private ProgressionSO progression;
        private Action LevelUpEffectAction;
        
        private void Awake()
        {
            InitBeforeLoad();
            ResourceManager.Instance.ReserveOperation(InitAfterLoad);
        }

        private void OnEnable()
        {
            
        }

        private async void InitBeforeLoad()
        {
            currentLevel = new LazyValue<int>(CalculateLevel);
            GetCurrLevel = currentLevel.value;
            GetCurrXp = currentXp;
        
            // if (GetComponent<ITypeHolder>() is not { } dataHolder) return;
            // if (dataHolder.BaseType is not CharacterTypeSO charInfo)
            // {
            //     var extract = await dataHolder.GetTypeAsync();
            //     if (extract is not CharacterTypeSO extractInfo) return;
            //     charInfo = extractInfo;
            // }
        }
        
        private void InitAfterLoad()
        {
            progression = ResourceManager.Instance.Load<ProgressionSO>("ProgressionSO.asset");
        }
        
        public void ReceiveType(ScriptableObject typeInfo)
        {
            if (typeInfo is PlayerTypeSO playerInfo)
            {
                LevelUpEffectAction = () =>
                {
                    if (transform == null) return;
                    PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(playerInfo.levelUpEffect, null, transform.position);
                };
            }
        }

        public void SetXp(float xp, bool updateLevel = true) // 경험치 설정
        {
            if (xp.IsEqualFloat(currentXp)) return;

            currentXp = xp;
            OnXpChanged?.Invoke(currentXp);

            if (updateLevel)
            {
                currentLevel.value = CalculateLevel(xp);
                OnLevelChanged?.Invoke(currentLevel.value);
            }
        }

        public void GainXp(float xp) // 경험치 획득
        {
            if (xp < 0) return; // 음수는 실행x
            SetXp(currentXp + xp, true);
        }
        
        public void SetLevel(int level) // 레벨 설정
        {
            if (currentLevel is { value: { } result } && result == level) return;
            
            currentLevel.value = level;
            CalculateXpFromLevel(level, out float xp);
            SetXp(xp, false);
        }

        private int CalculateLevel() // 현재 경험치에 해당하는 레벨 계산 (= 현재 레벨 계산)
        {
            return CalculateLevel(currentXp);
        }

        private int CalculateLevel(int level, float xp) // 특정 레벨 {level}에서 {xp}만큼의 경험치를 습득한 경우 레벨 계산
        {
            if (CalculateXpFromLevel(level, out float levelFloorXp))
                xp += levelFloorXp;
            return CalculateLevel(xp);
        }

        private int CalculateLevel(float xp)
        {
            if (progression == null) // ProgressionSO 참조가 없는 경우
            { // 기존 설정값(ex: startingLevel)이 존재하면 해당 값 사용, 없으면 0 반환
                return currentLevel is { value: { } existingLevel } ? existingLevel : 0;
            }
            
            int penultimateLevel = progression.GetMaxLevel(GameStat.ExperienceToLevelUp, CharacterClass.Player);
            
            for (int level = 1; level <= penultimateLevel; level++)
            {
                if (progression.GetProgressionStat(GameStat.ExperienceToLevelUp, CharacterClass.Player, level) is
                        { } xpToLevelUp && xpToLevelUp > xp)
                {
                    return level;
                }
            }

            return penultimateLevel + 1;
        }

        private bool CalculateXpFromLevel(int level, out float xp)
        {
            if (progression != null &&
                progression.GetProgressionStat(GameStat.ExperienceToLevelUp, CharacterClass.Player, level) is
                    float result)
            {
                xp = result;
                return true;
            }

            xp = 0;
            return false;
        }

        #region Save/Load (ISavable)

        public readonly struct PlayerLevelXpData
        {
            public readonly int level;
            public readonly float xp;

            public PlayerLevelXpData(int level, float xp)
            {
                this.level = level;
                this.xp = xp;
            }
        }

        public object CaptureState()
        {
            if (GetCurrLevel is { } level && GetCurrXp is { } xp)
            {
                return new PlayerLevelXpData(level, xp);
            }

            return null;
        }

        public bool RestoreState(object state)
        {
            if (state is PlayerLevelXpData { } data)
            {
                SetLevel(data.level);
                SetXp(data.xp);
                return true;
            }

            return false;
        }

        #endregion
    }
}

