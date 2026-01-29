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

namespace TH.Attribute
{
    public class PlayerExperience : MonoBehaviour, IExperience, ILevel, ISavable, ITypeDependent
    {
        public event Action<float> OnXpGained;
        public event Action<float> OnXpChanged;
        public event Action<int> OnLevelChanged;

        public int GetCurrLevel => currentLevel.Value;
        public float GetCurrXp => currentXp;

        private LazyValue<int> currentLevel;
        private float currentXp;
        
        private int startingLevel;
        
        private Action LevelUpEffectAction;
        private ProgressionSO progression;
        private IFloatingTextSpawner textSpawner;
        
        private void Awake()
        {
            InitBeforeLoad();
            textSpawner = ServiceLocator.Get<IFloatingTextSpawner>();
            ResourceManager.Instance.WaitForPreLoadOnlyOnce(InitAfterLoad);
        }

        void OnDestroy()
        {
            if (textSpawner == null) return;
            textSpawner.UnRegister(this, FloatingTextEventType.GetXp);
        }

        private void InitBeforeLoad()
        {
            currentLevel = new LazyValue<int>(CalculateLevel);
        }
        
        private void InitAfterLoad()
        {
            progression = ResourceManager.Instance.Load<ProgressionSO>("ProgressionSO.asset");
            currentLevel.ForceInit();

            textSpawner.Register(this, FloatingTextEventType.GetXp);

            LevelUpTestMethod().Forget();
        }
        
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
        
        public void ReceiveType(ScriptableObject typeInfo)
        {
            if (typeInfo is PlayerTypeSO playerInfo)
            {
                LevelUpEffectAction = () =>
                {
                    if (transform == null) return;
                    PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(playerInfo.levelUpEffect, transform, transform.position);
                };
            }
        }

        public void SetXp(float xp, bool updateLevel = true) // 경험치 설정
        {
            if (xp.IsEqualFloat(currentXp)) return;
            
            var delta = xp - currentXp;
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
        
        // 레벨 설정
        // byForce: 강제 지정 여부 (강제 지정 시 해당 레벨의 경험치 0 상태로 변경)
        // notifyCallbacks: 콜백 실행 여부 (OnLevelChanged)
        public void SetLevel(int level, bool byForce = false, bool notifyCallbacks = true) 
        {
            if (currentLevel is not { Value: { } currLv } || currLv == level) return; // 현재 레벨과 동일하면 실행x
            
            currentLevel.Value = level;
            if (byForce)
            {
                CalculateXpFromLevel(level, out float xp);
                SetXp(xp, false);
            }
            else if (currLv < level)
            {
                LevelUpEffectAction?.Invoke();
                Logg.Log($"Level up! ({level})", Logg.LoggingMode.Completed);
            }
            OnLevelChanged?.Invoke(currentLevel.Value);
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
            if (progression == null) // ProgressionSO 참조가 없는 경우 startingLevel 반환
                return startingLevel;
            
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

        #region Save/Load (ISavable)

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
                SetLevel(data.level);
                SetXp(data.xp);
                return true;
            }

            return false;
        }

        #endregion
    }
}

