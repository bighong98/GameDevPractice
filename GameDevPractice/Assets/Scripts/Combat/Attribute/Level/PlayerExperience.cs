using System;
// using GameDevTV.Utils;
using TH.SaveLoad;
using TH.Stats;
using TH.Core.Pool;
using UnityEngine;
using Cysharp.Threading.Tasks;
using TH.Resource;
using TH.Attribute.Stat;
using TH.Utils;
using TH.Core.Service;
using UnityEngine.Scripting;

namespace TH.Attribute
{
    public class PlayerExperience : MonoBehaviour, IExperience, ILevel, ISavable, ITypeDependent
    {
        #region Events (IExperience, ILevel)
        public event Action<int> OnLevelChanged;

        public event Action<float> OnXpGained;
        public event Action<float> OnXpChanged;
        public event Action<float> OnXpBaselineChanged;
        public event Action<float> OnXpToLevelUpChanged;
        #endregion

        #region Properties (IExperience, ILevel)
        public int GetCurrLevel => currentLevel;
        public float GetCurrXp => currentXp;
        public float GetCurrXpToLevelUp => currXpToLevelUp;
        public float GetCurrBaselineXp => currBaselineXp;
        #endregion
        
        #region Fields
        private int currentLevel = 1;
        private float currentXp = 0;
        private float currXpToLevelUp = 1;
        private float currBaselineXp = 0;
        
        private int startingLevel = 1;
        #endregion

        private Action LevelUpEffectAction;
        private ProgressionSO progression;

        private IFloatingTextSpawner textSpawner;
        
        private void Awake()
        {
            InitBeforeLoad();
            ResourceManager.Instance.WaitForPreLoadOnlyOnce(InitAfterLoad);
        }

        void OnDestroy()
        {
            textSpawner?.UnRegister(this, FloatingTextEventType.GetXp);
        }

        #region Initialization
        private void InitBeforeLoad()
        {
            textSpawner = ServiceLocator.Get<IFloatingTextSpawner>();
        }
        
        private void InitAfterLoad()
        {
            progression = ResourceManager.Instance.Load<ProgressionSO>("ProgressionSO.asset");
            textSpawner.Register(this, FloatingTextEventType.GetXp);

            LevelUpTestMethod().Forget();
        }

        #endregion
        
        #region ITypeDependent
        public void ReceiveType(ScriptableObject typeInfo)
        {
            if (typeInfo is not PlayerTypeSO playerInfo) return;
            
            LevelUpEffectAction = () =>
            {
                if (transform == null) return;
                PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(playerInfo.levelUpEffect, transform, transform.position);
            };

            if (playerInfo.startingLevel is {} defaultStartLv and > 0 && this.currentLevel > defaultStartLv)
            {
                SetLevel(defaultStartLv, byForce: true);
            }
        }
        #endregion

        #region IExpereince
        public void SetXp(float xp, bool updateLevel = true) // 경험치 설정
        {
            if (xp.IsEqualFloat(currentXp)) return;
            
            var delta = Mathf.Clamp(xp - currentXp, min: 0f, max: currentXp);
            currentXp = xp;

            OnXpChanged?.Invoke(currentXp);
            if (delta > 0) OnXpGained?.Invoke(delta);

            if (updateLevel)
                SetLevel(CalculateLevel(xp));
        }

        public void GainXp(float xp) // 경험치 획득
        {
            if (xp < 0) return; // 음수는 실행x
            Logg.Log($"Experience Gained ({xp})", Logg.LoggingMode.Completed);
            SetXp(currentXp + xp, true);
        }

        #endregion
        
        #region ILevel
        // 레벨 설정
        // byForce: 강제 지정 여부 (강제 지정 시 해당 레벨의 경험치 0 상태로 변경)
        // notifyCallbacks: 콜백 실행 여부 (OnLevelChanged)
        public void SetLevel(int level, bool byForce = false, bool notifyCallbacks = true) 
        {
            if (currentLevel == level) return; // 현재 레벨과 동일하면 실행x
            // 레벨 강제 변경 시 해당 레벨의 경험치 0 상태로 변경
            if (byForce && CalculateXpFromLevel(level, out float xp))
            {
                SetXp(xp);
                return;
            }

            var prevLevel = currentLevel;
            currentLevel = level;

            currentLevel = level;
            if (prevLevel < level)
            {
                LevelUpEffectAction?.Invoke();
                Logg.Log($"Level up: ({level})", Logg.LoggingMode.Completed);
            }

            OnLevelChanged?.Invoke(currentLevel);
            if (CalculateXpFromLevel(currentLevel - 1, out var newBaselineXp))
            {
                currBaselineXp = newBaselineXp;
                OnXpBaselineChanged?.Invoke(newBaselineXp);
            }   
            if (CalculateXpFromLevel(currentLevel, out var newXptoLevelUp))
            {
                currXpToLevelUp = newXptoLevelUp;
                OnXpToLevelUpChanged?.Invoke(newXptoLevelUp);
            }
                
        }

        #endregion

        private int CalculateLevel(float xp)
        {
            if (progression == null) // ProgressionSO 참조가 없는 경우 startingLevel 반환
                return startingLevel;
            //todo: ExperienceToLevelUp 스탯 관리 정책 확정 후 재검토
            int penultimateLevel = progression.GetMaxLevel(GameStats.ExperienceToLevelUp, CharacterType.Player);
            
            for (int level = 1; level <= penultimateLevel; level++)
            {
                if (progression.GetProgressionStat(GameStats.ExperienceToLevelUp, CharacterType.Player, level) is
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
                progression.GetProgressionStat(GameStats.ExperienceToLevelUp, CharacterType.Player, level) is
                    float result)
            {
                xp = result;
                return true;
            }

            xp = 0;
            return false;
        }

        #region ISavable (Save/Load)

        [Preserve]
        public struct PlayerLevelXpData
        {
            public int level;
            public float xp;

            public PlayerLevelXpData(int level, float xp)
            {
                this.level = level;
                this.xp = xp;
            }
        }

        public object CaptureState()
        {
            this.Log($"- ({gameObject.name}) CaptureState invoked", Logg.LoggingMode.Completed);

            if (GetCurrLevel is { } level && GetCurrXp is { } xp)
            {
                this.Log($"- ({gameObject.name}) CaptureState invoked - level: {level}), xp: {xp}", Logg.LoggingMode.Completed);
                return new PlayerLevelXpData(level, xp);
            }

            return null;
        }

        public bool RestoreState(object state)
        {
            this.Log($"- ({gameObject.name}) RestoreState invoked", Logg.LoggingMode.Completed);
            if (state is PlayerLevelXpData { } data)
            {
                this.Log($"- ({gameObject.name}) RestoreState invoked - SetLevel({data.level}), SetXp({data.xp})", Logg.LoggingMode.Completed);
                // SetLevel(data.level);
                SetXp(data.xp, updateLevel: true);
                return true;
            }

            return false;
        }

        public void ResetToDefaultState()
        {
            SetXp(0f, updateLevel: true);
        }

        #endregion
        
        #region Test (Editor Only)

#if UNITY_EDITOR
        private readonly TimeSpan oneSecond = TimeSpan.FromSeconds(1);
        private async UniTaskVoid LevelUpTestMethod()
        {
            Logg.Log($"[{nameof(PlayerExperience)}] '{nameof(LevelUpTestMethod)}' started", Logg.LoggingMode.Completed);
            int count = 0;
            while (count < 10)
            {
                await UniTask.Delay(oneSecond, DelayType.DeltaTime);
                if (this == null || gameObject == null) break;
                
                count++;
                GainXp(50);
            }
        } 
#endif
        #endregion

    }
}

