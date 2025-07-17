using System;
using UnityEngine;
using RPG.Core;
using RPG.Saving;
using RPG.Stats;

namespace RPG.Attribute
{
    [Serializable]
    public struct HealthSaveData
    {
        public float hp;
    }
    public class Health : MonoBehaviour, ISavable
    {
        [SerializeField] private float healthPoints = -1f; // -1 means not initialized(= not Start() && not RestoreState())

        private Animator animator;
        private CharacterStats stats;
        
        private static readonly int DieAnimHash = Animator.StringToHash("die");

        private void Awake()
        {
            animator = GetComponent<Animator>();
            stats = GetComponent<CharacterStats>();
        }

        private void Start()
        {
            if (healthPoints < 0 && stats != null) // healthPoints가 초기화되지 않은 경우에만 초기화 시도
            {
                healthPoints = stats.GetStat(GameStat.Health); // 레벨에 맞는 최대체력값 불러오기
            }
        }

        public bool IsDead { get; private set; }

        public void TakeDamage(float damage)
        {
            healthPoints = Mathf.Max(healthPoints - damage, 0); print($"health: {healthPoints}");
            RefreshAliveState();
        }

        private void RefreshAliveState()
        {
            if (healthPoints <= 0)
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

        public object CaptureState()
        {
            #region For Debug

            // Debug.Log($"[Health.CaptureState()] \n"+ 
            //           $"id: {GetComponent<SavableEntity>().GetUniqueIdentifier()} \n" + 
            //           $"hp: {healthPoints}");

            #endregion
            
            return new HealthSaveData { hp = healthPoints };
        }

        public bool RestoreState(object state)
        {
            if (state is not HealthSaveData data) return false;
            
            // Debug.Log($"RestoreState for Health: hp to {data.hp}"); 

            healthPoints = data.hp;
            RefreshAliveState();

            return true;
        }
    }
}
