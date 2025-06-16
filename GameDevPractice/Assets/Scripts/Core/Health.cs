using System;
using System.Collections;
using System.Collections.Generic;
using RPG.Saving;
using UnityEngine;

namespace RPG.Core
{
    [Serializable]
    public struct HealthSaveData
    {
        public float hp;
    }
    public class Health : MonoBehaviour, ISavable
    {
        [SerializeField] private float healthPoints = 100f;

        private Animator animator;
        
        private static readonly int DieAnimHash = Animator.StringToHash("die");

        private void Awake()
        {
            animator = GetComponent<Animator>();
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
