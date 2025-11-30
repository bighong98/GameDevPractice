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
        public float hp;
    }
    public class Health : MonoBehaviour, IDamageable, ISavable
    {
        private LazyValue<float> maxHp;
        private LazyValue<float> hp;
        
        private Animator animator;
        private IStatHolder statHolder;
        private ILevel levelHolder;
        private bool hasMutableLevel;
        
        private static readonly int DieAnimHash = Animator.StringToHash("die");
        public bool IsDead { get; private set; }
        public event Action OnDead;
        public event Action OnRevived;
        
        public float GetCurrentHealth => hp.Value;
        public float GetMaxHealth => maxHp.Value;
        public float GetCurrentHealthRatio => (hp.Value / maxHp.Value);

        // public event Action<HitResult> OnDamaged;
        public event HitEvent OnDamaged;
        public Action<float> OnHealthRatioChanged; // 현재 체력에 변동이 생긴 경우 (피격, 회복 등)
        public Action<float> OnMaxHealthChanged; // 최대 체력에 변동이 생긴 경우 (레벨 업, 장비 변경 등)
        public Action<float> OnCurrHealthChanged; // 현재 체력에 변동이 생긴 경우 (피격, 회복 등)
        
        [SerializeField] private GameObject HPBarPrefab; // serialize for debug

        private IAttackable lastAttacker; // 가장 최근 자신에게 피해를 입힌 대상
        private LazyValue<float> rewardXp;

        private IFloatingTextSpawner textSpawner;
        
        private void Awake()
        {
            animator = GetComponent<Animator>();
            statHolder = GetComponent<IStatHolder>();
            if (TryGetComponent(out ILevel iLevel))
            {
                levelHolder = iLevel;
                hasMutableLevel = true;
            }

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
        }

private void Start()
        {
            UIManager.Instance.ReserveOperation(() =>
            {
                if (!this.IsAlive()) return;
                UIManager.Instance.GetUIFromPool<HPBar>(HPBarPrefab, UICanvas.AnchoredOverlay).SetOwner(this);
            });
        }

        private void OnEnable()
        {
            textSpawner.Register(this, FloatingTextEventType.Damage);
            if (!hasMutableLevel || levelHolder == null) return;
            levelHolder.OnLevelChanged += this.OnLevelUp;
        }

        private void OnDisable()
        {
            textSpawner.UnRegister(this, FloatingTextEventType.Damage);
            if (!hasMutableLevel || levelHolder == null) return;
            levelHolder.OnLevelChanged -= this.OnLevelUp;
        }

private float GetInitialHealth()
        {
            if (statHolder?.GetStat(GameStats.Health) is not { } stat)
            {
                Logg.LogError($"[{gameObject.name}.Health] Failed to initialize health stat");
                return 0;
            }
            
            stat.OnStatChanged += () => 
            { 
                hp.Value = stat.Value; 
                Logg.Log($"[{gameObject.name}.Health] HP stat changed. Setting HP to {stat.Value}", Logg.LoggingMode.Completed); 
            };
            return stat.Value;
        }

        // 최대 체력 + 현재 체력 조정
        // 강제로 현재 체력을 조정하기 때문에 사망한 캐릭터에 사용 시 부활하므로 주의
        private void SetHp(float amount)
        {
            SetMaxHp(amount, byForce: true);
            SetCurrentHp(amount, byForce: true);
        }
 
        // 현재 체력 조정
        // byForce: true => 사망 상태 무시하고 체력 변경 및 사망 상태 갱신
        private void SetCurrentHp(float amount, bool byForce = false) 
        {
            if (IsDead && !byForce) return; // 사망 상태인 경우 체력 조정x
            
            if (maxHp.Value is not ({ } max and > 0))
            {
                Logg.Log($"[{gameObject.name}.{nameof(SetCurrentHp)}]Max Hp is less or equal to 0. failed to set HP", Logg.LoggingMode.Completed);
                Die(); // 최대 체력이 세팅되어있지 않다면 사망 처리
                return;
            }
            
            var curr = hp.Value = Mathf.Clamp(amount, 0, max);
            OnHealthRatioChanged?.Invoke(curr / max);
            OnCurrHealthChanged?.Invoke(curr);
            RefreshAliveState();
        }

        private void SetMaxHp(float amount, bool byForce = false)
        {
            if (!byForce && (amount < 0 || amount.IsEqualFloat(0f))) return; // 최대체력 0 이하로 설정 불가능
            maxHp.Value = amount;
            OnMaxHealthChanged?.Invoke(maxHp.Value);
        }
        
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
            animator.SetTrigger(DieAnimHash);
            GetComponent<ActoinScheduler>().CancelCurrentAction();

            if (lastAttacker is Component c && c.TryGetComponent(out IExperience xp))
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

        public object CaptureState()
        {
            #region For Debug

            // Logg.Log($"[Health.CaptureState()] \n"+ 
            //           $"id: {GetComponent<SavableEntity>().GetUniqueIdentifier()} \n" + 
            //           $"hp: {healthPoints}");

            #endregion
            
            return new HealthSaveData
            {
                hp = hp.Value
            };
        }

        public bool RestoreState(object state)
        {
            if (state is not HealthSaveData data) return false;
            
            Logg.Log($"[{gameObject.name}]RestoreState for Health: hp to {data.hp}" ,Logg.LoggingMode.Completed); 
            
            SetHp(data.hp);
            // RefreshAliveState();

            return true;
        }
    }
}
