using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPG.Movement;
using RPG.Core;
using UnityEngine.Serialization;

namespace RPG.Combat
{
    public class Fighter : MonoBehaviour, IAction
    {
        [SerializeField] private float timeBetweenAttacks = 1f; // todo: move to equipped weapon
        private float timeSinceLastAttack = 0;
        
        [SerializeField] private WeaponTypeSO currentWeapon; // 현재 장착 중인 무기
        [SerializeField] private WeaponTypeSO defaultWeapon; // 장비 장착해제시 적용되어야할 무기종(ex-Unarmed)
        
        [SerializeField] private Health target;
        
        private Mover mover;
        private Animator animator;
        
        private static readonly int Attack1 = Animator.StringToHash("attack");
        private static readonly int StopAttack = Animator.StringToHash("stopAttack");
        public ActoinScheduler ActionScheduler { get; private set; }
        
        public event Action<WeaponTypeSO, Animator> OnEquipWeapon; // 장비 변경(장착, 장착해제) 시, Equipper.cs 에서 사용
        public event Action OnAttack; // 공격 시도 시
        public event Action<Health> OnTargetChanged; // 공격 타겟(target) 변경 시
        
        public bool IsEquippingWeapon => currentWeapon != null;
        public (WeaponTypeSO weapon, Animator animator) GetWeaponEquipperInfo => (this.currentWeapon, this.animator);
        
        private void Start()
        {
            mover = GetComponent<Mover>();
            animator = GetComponent<Animator>();
            ActionScheduler = GetComponent<ActoinScheduler>();

            EquipWeapon(currentWeapon == null ? defaultWeapon : currentWeapon);
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

        private bool IsInRange => Vector3.Distance(transform.position, target.transform.position) < currentWeapon?.GetRange;
        
        private void AttackBehaviour()
        {
            transform.LookAt(target.transform);
            if (timeSinceLastAttack > timeBetweenAttacks)
            {
                AnimateAttack(); // trigger Hit() 
                timeSinceLastAttack = 0;
            }
        }
        
        public void Cancel()
        {
            timeSinceLastAttack = 0;
            AnimateStopAttack();
            target = null;
        }

        #region Animation Event Method

        void Hit() // Animation Event Method
        {
            if (target == null) return;
            OnAttack?.Invoke();
            target.TakeDamage(currentWeapon?.GetDamage ?? 0);
        }

        void Shoot()
        {
            OnAttack?.Invoke();
        }

        #endregion

        #region Attack

        private void ChangeTarget(Health newTarget)
        {
            // if (target is { } prevTarget && prevTarget == newTarget) return;
            
            target = newTarget;
            OnTargetChanged?.Invoke(target);
        }
        public void Attack(CombatTarget combatTarget)
        {
            ActionScheduler.StartAction(this);
            if (combatTarget.GetComponent<Health>() is {} newTarget)
            {
                ChangeTarget(newTarget);
            }
        }

        public void Attack(Health targetHealth)
        {
            ActionScheduler.StartAction(this);
            ChangeTarget(targetHealth);
        }

        public bool CanAttack(GameObject combatTarget, out Health targetHealth)
        {
            if (combatTarget == null || combatTarget == gameObject)
            {
                targetHealth = null;
                return false;
            }
            
            if (combatTarget.GetComponent<Health>() is { } health)
            {
                targetHealth = health;
                return true;
            }

            targetHealth = null;
            return false;
        }

        #endregion
        
        #region Animate

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

        #endregion

        #region Weapon
        
        public void EquipWeapon(WeaponTypeSO weaponTypeSO)
        {
            this.currentWeapon = weaponTypeSO;
            OnEquipWeapon?.Invoke(weaponTypeSO, animator);
        }

        public void UnEquipWeapon()
        {
            if (defaultWeapon != null)
            {
                currentWeapon = defaultWeapon;
                OnEquipWeapon?.Invoke(defaultWeapon, animator);
            }
        }

        #endregion
        
    }
}
