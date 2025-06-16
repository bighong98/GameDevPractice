using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG.Movement;
using RPG.Core;

namespace RPG.Combat
{
    public class Fighter : MonoBehaviour, IAction
    {
        [SerializeField] private float weaponRange = 2f;
        [SerializeField] private float weaponDamage = 2f;
        [SerializeField] private float timeBetweenAttacks = 1f; // todo: move to equipped weapon
        private float timeSinceLastAttack = 0;
        
        [SerializeField] private Health target;
        
        private Mover mover;
        private Animator animator;
        
        private static readonly int Attack1 = Animator.StringToHash("attack");
        private static readonly int StopAttack = Animator.StringToHash("stopAttack");
        public ActoinScheduler ActionScheduler { get; private set; }
        

        private void Start()
        {
            mover = GetComponent<Mover>();
            animator = GetComponent<Animator>();
            ActionScheduler = GetComponent<ActoinScheduler>();
        }

        private void Update()
        {
            timeSinceLastAttack += Time.deltaTime;
            if (target == null) return;
            if (target.IsDead) return;
            
            if (!IsInRange)
                mover.Moveto(target.transform.position);
            else
            {
                mover.Cancel();
                AttackBehaviour();
            }
        }

        private bool IsInRange => Vector3.Distance(transform.position, target.transform.position) < weaponRange;

        private void AttackBehaviour()
        {
            transform.LookAt(target.transform);
            if (timeSinceLastAttack > timeBetweenAttacks)
            {
                AnimateAttack(); // trigger Hit() 
                timeSinceLastAttack = 0;
            }
        }
        
        void Hit() // Animation Event Method
        {
            if (target == null) return;
            target.TakeDamage(weaponDamage);
        }
        
        public void Attack(CombatTarget combatTarget)
        {
            ActionScheduler.StartAction(this);
            target = combatTarget.GetComponent<Health>();
        }

        public void Attack(Health targetHealth)
        {
            ActionScheduler.StartAction(this);
            target = targetHealth;
        }

        public bool CanAttack(GameObject combatTarget, out Health targetHealth)
        {
            targetHealth = null;
            if (combatTarget == null) return false;

            if (combatTarget.GetComponent<Health>() is { } health)
            {
                targetHealth = health;
                return true;
            }

            return false;
        }

        private void AnimateAttack()
        {
            animator.ResetTrigger(StopAttack); // 공격이 강제로 취소된 경우를 위한 초기화
            animator.SetTrigger(Attack1);
        }

        private void AnimateStopAttack()
        {
            animator.ResetTrigger(Attack1);
            animator.SetTrigger(StopAttack);
        }
        
        public void Cancel()
        {
            timeSinceLastAttack = 0;
            AnimateStopAttack();
            target = null;
        }

        
    }
}
