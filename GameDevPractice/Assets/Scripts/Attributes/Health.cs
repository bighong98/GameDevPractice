using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using RPG.Core;
using RPG.Saving;
using RPG.Stats;
using GameDevTV.Utils;
using RPG.UI;
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
        private CharacterStats stats;
        
        private static readonly int DieAnimHash = Animator.StringToHash("die");
        public bool IsDead { get; private set; }
        public float GetCurrentHealth => hp.value;
        public float GetMaxHealth => maxHp.value;
        public float GetCurrentHealthRatio => (hp.value / maxHp.value);

        public Action<float> OnHealthRatioChanged; // 현재 체력에 변동이 생긴 경우 (피격, 회복 등)
        public Action<float> OnMaxHealthChanged; // 최대 체력에 변동이 생긴 경우 (레벨 업, 장비 변경 등)

        [SerializeField] private GameObject HPBarPrefab; // testing
        
        private void Awake()
        {
            animator = GetComponent<Animator>();
            stats = GetComponent<CharacterStats>();

            maxHp = new LazyValue<float>(GetInitialHealth);
            hp = new LazyValue<float>(GetInitialHealth);
        }

        private void Start()
        {
            maxHp.ForceInit();
            hp.ForceInit();
            
            UIManager.Instance.ReserveOperation(() =>
            {
                if (this != null)
                {
                    UIManager.Instance.GetUIFromPool<HPBar>(HPBarPrefab, UICanvas.AnchoredOverlay).SetOwner(this);
                }
            });
            // await UniTask.Delay(TimeSpan.FromSeconds(3), cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
            // if (this != null)
            // {
            //     UIManager.Instance.GetUIFromPool<HPBar>(HPBarPrefab, UICanvas.Overlay).SetOwner(this);
            // }
        }

        private void OnEnable()
        {
            stats.OnLevelUp += this.OnLevelUp;
        }

        private void OnDisable()
        {
            stats.OnLevelUp -= this.OnLevelUp;
        }

        private float GetInitialHealth()
        {
            return GetComponent<CharacterStats>().GetStat(GameStat.Health);
        }

        private void SetCurrentHealth(float amount)
        {
            if (maxHp.value is not ({ } max and > 0))
            {
                Util.Log("Max Hp is less or equal to 0. failed to set HP");
                return;
            }
            
            var curr = hp.value = Mathf.Clamp(amount, 0, max);
            OnHealthRatioChanged?.Invoke(curr / max);
        }

        private void SetMaxHealth(float amount)
        {
            maxHp.value = amount;
            OnMaxHealthChanged?.Invoke(maxHp.value);
        }
        
        public void TakeDamage(float damage)
        {
            SetCurrentHealth(hp.value - damage); 
            RefreshAliveState(); // SetCurrentHealth()로 옮길지 고려
            
            Util.Log($"health: {hp.value}");
        }

        private void RefreshAliveState()
        {
            if (hp.value <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (IsDead) return;
            IsDead = true;
            animator.SetTrigger(DieAnimHash);
            GetComponent<ActoinScheduler>().CancelCurrentAction();
        }

        private const int LevelUpRegenerationPercentage = 50;
        private void OnLevelUp(int level)
        {
            SetMaxHealth(stats.GetStat(GameStat.Health, level));
            SetCurrentHealth(hp.value + maxHp.value * ((float)LevelUpRegenerationPercentage / 100));
            // Util.Log($"OnLevelUp: hp: {hp.value}");
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
                hp = hp.value
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

        public void TakeDamage(in HitResult hitResult)
        {
            
        }
    }
}
