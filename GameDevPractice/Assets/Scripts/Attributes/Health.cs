using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RPG.Core;
using RPG.Saving;
using RPG.Stats;
using TH.Utils;
using RPG.UI;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat;

namespace RPG.Attribute
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
        // private CharacterStats stats;
        private IStatHolder statHolder;
        private ILevel levelHolder;
        private bool hasMutableLevel;
        
        private static readonly int DieAnimHash = Animator.StringToHash("die");
        public bool IsDead { get; private set; }
        public event Action OnDead;
        // public event Action OnRevived;
        
        public float GetCurrentHealth => hp.Value;
        public float GetMaxHealth => maxHp.Value;
        public float GetCurrentHealthRatio => (hp.Value / maxHp.Value);

        public Action<float> OnHealthRatioChanged; // 현재 체력에 변동이 생긴 경우 (피격, 회복 등)
        public Action<float> OnMaxHealthChanged; // 최대 체력에 변동이 생긴 경우 (레벨 업, 장비 변경 등)

        [SerializeField] private GameObject HPBarPrefab; // serialize for debug

        private IAttackable lastAttacker; // 가장 최근 자신에게 피해를 입힌 대상
        private LazyValue<float> rewardXp;
        
        private void Awake()
        {
            animator = GetComponent<Animator>();
            statHolder = GetComponent<IStatHolder>();
            if (TryGetComponent(out ILevel iLevel))
            {
                levelHolder = iLevel;
                hasMutableLevel = true;
            }

            maxHp = new LazyValue<float>(GetInitialHealth);
            hp = new LazyValue<float>(GetInitialHealth);
            rewardXp = new LazyValue<float>(() =>
            {
                if (statHolder != null && statHolder.GetStat(GameStat.ExperienceReward) is {} result)
                {
                    return result;
                }
                Util.Log($"[{gameObject.name}.{nameof(Health)}] failed to initialize rewardXp field");
                return 0;
            });
        }

        private void Start()
        {
            // maxHp.ForceInit();
            // hp.ForceInit();
            
            UIManager.Instance.ReserveOperation(() =>
            {
                if (this != null)
                {
                    UIManager.Instance.GetUIFromPool<HPBar>(HPBarPrefab, UICanvas.AnchoredOverlay).SetOwner(this);
                }
            });
        }

        private void OnEnable()
        {
            // stats.OnLevelUp += this.OnLevelUp;
            if (!hasMutableLevel || levelHolder == null) return;
            levelHolder.OnLevelChanged += this.OnLevelUp;
        }

        private void OnDisable()
        {
            // stats.OnLevelUp -= this.OnLevelUp;
            if (!hasMutableLevel || levelHolder == null) return;
            levelHolder.OnLevelChanged -= this.OnLevelUp;
        }

        private float GetInitialHealth()
        {
            // return GetComponent<CharacterStats>().GetStat(GameStat.Health);
            return statHolder?.GetStat(GameStat.Health) ?? 0; 
        }

        private void SetCurrentHealth(float amount)
        {
            if (maxHp.Value is not ({ } max and > 0))
            {
                Util.Log($"[{gameObject.name}.{nameof(Health)}]Max Hp is less or equal to 0. failed to set HP");
                return;
            }
            
            var curr = hp.Value = Mathf.Clamp(amount, 0, max);
            OnHealthRatioChanged?.Invoke(curr / max);
        }

        private void SetMaxHealth(float amount)
        {
            maxHp.Value = amount;
            OnMaxHealthChanged?.Invoke(maxHp.Value);
        }
        
        private void TakeDamage(float damage)
        {
            SetCurrentHealth(hp.Value - damage); 
            RefreshAliveState(); // SetCurrentHealth()로 옮길지 고려
            
            Util.Log($"health: {hp.Value}");
        }
        
        public void TakeDamage(in HitResult hitResult)
        {
            TakeDamage(hitResult.Damage);
            lastAttacker = hitResult.Attacker;
        }

        private void RefreshAliveState()
        {
            if (hp.Value <= 0)
            {
                Die();
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

        private const int LevelUpRegenerationPercentage = 50;
        private void OnLevelUp(int level)
        {
            SetMaxHealth(statHolder.GetStat(GameStat.Health, level));
            SetCurrentHealth(hp.Value + maxHp.Value * ((float)LevelUpRegenerationPercentage / 100));
            Util.Log($"OnLevelUp: hp: {hp.Value}", Util.LoggingMode.Completed);
        }

        public object CaptureState()
        {
            #region For Debug

            // Debug.Log($"[Health.CaptureState()] \n"+ 
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
            
            // Debug.Log($"RestoreState for Health: hp to {data.hp}"); 
            
            SetCurrentHealth(data.hp);
            RefreshAliveState();

            return true;
        }
    }
}
