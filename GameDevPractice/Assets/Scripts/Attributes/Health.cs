using System;
using UnityEngine;
using RPG.Core;
using RPG.Saving;
using RPG.Stats;
using GameDevTV.Utils;

namespace RPG.Attribute
{
    [Serializable]
    public struct HealthSaveData
    {
        public float hp;
    }
    public class Health : MonoBehaviour, ISavable
    {
        // [SerializeField] private float healthPoints = -1f; // -1 means not initialized(= not Start() && not RestoreState())
        private LazyValue<float> hp;
        
        private Animator animator;
        private CharacterStats stats;
        
        private static readonly int DieAnimHash = Animator.StringToHash("die");
        public bool IsDead { get; private set; }
        
        private void Awake()
        {
            animator = GetComponent<Animator>();
            stats = GetComponent<CharacterStats>();

            hp = new LazyValue<float>(GetInitialHealth);
        }

        private void Start()
        {
            hp.ForceInit();
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
        
        public void TakeDamage(float damage)
        {
            hp.value = Mathf.Max(hp.value - damage, 0); print($"health: {hp.value}");
            RefreshAliveState();
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
            float newMaxHp = stats.GetStat(GameStat.Health, level);
            hp.value = Mathf.Min(newMaxHp, hp.value + newMaxHp * ((float)LevelUpRegenerationPercentage/100));
            Util.Log($"OnLevelUp: hp: {hp.value}");
        }

        public object CaptureState()
        {
            #region For Debug

            // Debug.Log($"[Health.CaptureState()] \n"+ 
            //           $"id: {GetComponent<SavableEntity>().GetUniqueIdentifier()} \n" + 
            //           $"hp: {healthPoints}");

            #endregion
            
            return new HealthSaveData { hp = hp.value };
        }

        public bool RestoreState(object state)
        {
            if (state is not HealthSaveData data) return false;
            
            // Debug.Log($"RestoreState for Health: hp to {data.hp}"); 

            hp.value = data.hp;
            RefreshAliveState();

            return true;
        }
    }
}
