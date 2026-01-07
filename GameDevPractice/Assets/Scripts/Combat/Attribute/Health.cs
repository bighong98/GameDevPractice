using System;
using UnityEngine;
using TH.Core;
using TH.SaveLoad;
using TH.Utils;
using TH.UI;
using TH.Attribute.Stat;
using TH.Combat;
using TH.Core.Service;

namespace TH.Attribute
{
    [Serializable]
    public struct HealthSaveData
    {
        public float maxHp;
        public float hp;
    }
    public class Health : MonoBehaviour, IDamageable, IHealable, ISavable
    {
        private LazyValue<float> maxHp;
        private LazyValue<float> hp;
        
        private IStatHolder statHolder;
        private ILevel levelHolder;
        private bool hasMutableLevel;
        
        public event Action OnDead;
        public event Action OnRevived;
        public event HitEvent OnDamaged; // 피해를 입은 경우
        public event Action<float> OnHealed; // 회복 받은 경우
        
        public Action<float> OnHealthRatioChanged; // 현재 체력에 변동이 생긴 경우 (피격, 회복 등)
        public Action<float> OnMaxHealthChanged; // 최대 체력에 변동이 생긴 경우 (레벨 업, 장비 변경 등)
        public Action<float> OnCurrHealthChanged; // 현재 체력에 변동이 생긴 경우 (피격, 회복 등)
        
        public float GetCurrentHealth => hp.Value;
        public float GetMaxHealth => maxHp.Value;
        public float GetCurrentHealthRatio => (hp.Value / maxHp.Value);
        public bool IsDead { get; private set; }
        
        [SerializeField] private GameObject HPBarPrefab; // serialize for debug

        private IAttacker lastAttacker; // 가장 최근 자신에게 피해를 입힌 대상
        private LazyValue<float> rewardXp;

        private IFloatingTextSpawner textSpawner;

        private void Awake()
        {
            TryGetComponent(out statHolder);
            hasMutableLevel = TryGetComponent(out levelHolder);

            textSpawner = ServiceLocator.Get<IFloatingTextSpawner>();

            maxHp = new LazyValue<float>(GetInitialHealth);
            hp = new LazyValue<float>(GetInitialHealth);
            rewardXp = new LazyValue<float>(() =>
            {
                if (statHolder?.GetStat(GameStats.ExperienceReward) is { } result)
                {
                    return result.Value;
                }
                Logg.LogError($"[{gameObject.name}.Health] Failed to initialize rewardXp field. Stat 'ExperienceReward' not found.");
                return 0;
            });
            this.Log($"{gameObject.name} - Awake() in scene({gameObject.scene.name}) done", Logg.LoggingMode.Completed);
        }

        private void Start()
        {
            UIManager.Instance.GetUIFromPool<HPBar>(HPBarPrefab, UICanvas.AnchoredOverlay).SetOwner(this);
            maxHp.ForceInit();
            hp.ForceInit();
        }

        private void OnEnable()
        {
            textSpawner.Register(this, FloatingTextEventType.Damage);
            textSpawner.Register(this, FloatingTextEventType.Heal);
            if (!hasMutableLevel || !levelHolder.IsNotNull()) return;
            levelHolder.OnLevelChanged += this.OnLevelUp;
        }

        private void OnDisable()
        {
            textSpawner.UnRegister(this, FloatingTextEventType.Damage);
            textSpawner.UnRegister(this, FloatingTextEventType.Heal);
            if (!hasMutableLevel || !levelHolder.IsNotNull()) return;
            levelHolder.OnLevelChanged -= this.OnLevelUp;
        }

        private float GetInitialHealth()
        {
            if (!statHolder.IsNotNull() || statHolder.GetStat(GameStats.Health) is not { } stat)
            {
                Logg.LogError($"[{gameObject.name}.Health] Failed to initialize health stat");
                return 0;
            }
            
            // 스탯 변경 시 SetMaxHp를 통해 비율 기반으로 체력 조정
            stat.OnStatChanged += () => 
            { 
                SetMaxHp(stat.Value);
                Logg.Log($"[{gameObject.name}.Health] HP stat changed. MaxHp updated to {stat.Value}", Logg.LoggingMode.Completed); 
            };
            this.Log($"{gameObject.name} - GetInitialHealth({stat.Value})", Logg.LoggingMode.Completed);
            return stat.Value;
        }

        // 최대 체력 + 현재 체력 조정
        // 강제로 현재 체력을 조정하기 때문에 사망한 캐릭터에 사용 시 부활하므로 주의
        private void SetHp(float amount, bool modifyMax = false)
        {
            if (modifyMax) SetMaxHp(amount, byForce: true);
            else SetCurrentHp(amount, byForce: true);
        }
 
        // 현재 체력 조정
        // byForce: true => 사망 상태 무시하고 체력 변경 및 사망 상태 갱신
        private void SetCurrentHp(float amount, bool byForce = false) 
        {
            if (IsDead && !byForce) return; // 사망 상태인 경우 체력 조정x
            
            if (!maxHp.TryGet(out var max) || max.IsEqualFloat(0f))
            {
                this.LogWarning($"({gameObject.name}, {nameof(SetCurrentHp)}) - Max Hp is less or equal to 0. failed to set HP");
                Die(); // 최대 체력이 세팅되어있지 않다면 사망 처리
                return;
            }
            
            Logg.Log($"[{gameObject.name}.{GetType()}] SetCurrentHp ({amount})", Logg.LoggingMode.Completed);
            var curr = hp.Value = Mathf.Clamp(amount, 0, max);
            OnHealthRatioChanged?.Invoke(curr / max);
            OnCurrHealthChanged?.Invoke(curr);
            RefreshAliveState();
        }

        private void SetMaxHp(float amount, bool byForce = false)
        {
            if (!byForce && (amount < 0 || amount.IsEqualFloat(0f))) return; // 최대체력 0 이하로 설정 불가능
            
            // 최초 초기화인지 확인 (maxHp가 아직 초기화되지 않았으면 최초)
            bool isFirstInit = !maxHp.Initialized;
            
            // 기존 체력 비율 계산 (최초 초기화라면 100%, 아니면 현재 비율 유지)
            float healthRatio = isFirstInit ? 1f : GetCurrentHealthRatio;
            
            Logg.Log($"[{gameObject.name}.{GetType()}] SetMaxHp ({amount}), isFirstInit: {isFirstInit}, healthRatio: {healthRatio:F2}", Logg.LoggingMode.Completed);
            maxHp.Value = amount;
            OnMaxHealthChanged?.Invoke(maxHp.Value);
            
            // 체력 비율에 맞게 현재 체력 조정
            SetCurrentHp(maxHp.Value * healthRatio, byForce: true);
        }

        #region IDamageable

        private void TakeDamage(float damage)
        {
            SetCurrentHp(hp.Value - damage); 
            Logg.Log($"[{gameObject.name}.{nameof(TakeDamage)}]: hp: {hp.Value}", Logg.LoggingMode.Completed);
        }
        
        public void TakeDamage(in HitResult hitResult)
        {
            OnDamaged?.Invoke(hitResult);
            TakeDamage(hitResult.Damage);
            lastAttacker = hitResult.Attacker;
        }

        #endregion
        
        private void RefreshAliveState()
        {
            if (!IsDead && hp.Value.IsEqualFloat(0f))
            {
                Die();
            }
            else if (IsDead && hp.Value > 0f)
            {
                Revive();
            }
        }

        private void Die()
        {
            if (IsDead) return;
            
            IsDead = true;
            OnDead?.Invoke();

            if (lastAttacker is Component c && 
                c.IsNotNull() &&
                c.TryGetComponent(out IExperience xp))
            {
                xp.GainXp(rewardXp.Value);
            }
        }

        private void Revive()
        {
            if (!IsDead) return;
            IsDead = false;
            OnRevived?.Invoke();
        }

        private const int LevelUpRegenerationPercentage = 50;
        private void OnLevelUp(int level)
        {
            SetMaxHp(statHolder.GetStat(GameStats.Health, level));
            SetCurrentHp(hp.Value + maxHp.Value * ((float)LevelUpRegenerationPercentage / 100));
            Logg.Log($"OnLevelUp: hp: {hp.Value}", Logg.LoggingMode.Completed);
        }

        #region ISavable
        
        public object CaptureState()
        {
            this.Log($"[{gameObject.name}]CaptureState - hp: {hp.Value}" ,Logg.LoggingMode.Completed); 
            
            return new HealthSaveData
            {
                hp = hp.Value,
                maxHp = maxHp.Value,
            };
        }

        public bool RestoreState(object state)
        {
            if (state is not HealthSaveData data) return false;
            this.Log($"[{gameObject.name}]RestoreState for Health: (maxHp: {data.maxHp}, hp: {data.hp})", Logg.LoggingMode.Completed); 
            
            SetMaxHp(data.maxHp, byForce: true);
            SetCurrentHp(data.hp, byForce: true);

            return true;
        }

        #endregion

        #region IHealable

        public bool Heal(int amount, bool byForce = false)
        {
            SetCurrentHp(hp.Value + amount, byForce);
            OnHealed?.Invoke(amount);
            return true;
        }

        public bool HealRatio(float ratio, bool byForce = false)
        {
            if (maxHp is not {Initialized: true, Value: {} maxHpValue}) return false;
            return Heal((int)(maxHpValue * ratio), byForce);
        }

        #endregion
    }
}
