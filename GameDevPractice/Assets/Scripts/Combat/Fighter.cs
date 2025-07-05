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
        
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private WeaponTypeSO currentWeapon; // 현재 장착 중인 무기
        [SerializeField] private WeaponTypeSO defaultWeapon; // 장비 장착해제시 적용되어야할 무기종(ex-Unarmed)
        
        [SerializeField] private Health target;
        
        private Mover mover;
        private Animator animator;
        
        private static readonly int Attack1 = Animator.StringToHash("attack");
        private static readonly int StopAttack = Animator.StringToHash("stopAttack");
        public ActoinScheduler ActionScheduler { get; private set; }
        
        public event Action<WeaponTypeSO, Animator> OnEquipWeapon;
        
        public bool IsEquippingWeapon => currentWeapon != null;
        public (WeaponTypeSO weapon, Animator animator) GetWeaponEquipperInfo => (this.currentWeapon, this.animator);
        

        private void Awake()
        {
            // if (Util.FindChild(gameObject, "Root", recursive: true) is not { } root)
            // {
            //     Util.Log($"failed to find root for hand: {gameObject.name}");
            //     return;
            // }
            //
            // if (rightHandTransform == null && 
            //     Util.FindChildContainName<Transform>(root, "hand_r", true, false) is {} rResult)
            // {
            //     rightHandTransform = rResult;
            // }
            //
            // if (leftHandTransform == null && 
            //     Util.FindChildContainName<Transform>(root, "hand_l", true, false) is {} lResult)
            // {
            //     leftHandTransform = lResult;
            // }
        }

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
        
        void Hit() // Animation Event Method
        {
            if (target == null) return;
            target.TakeDamage(currentWeapon?.GetDamage ?? 0);
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
        
        
        public void Cancel()
        {
            timeSinceLastAttack = 0;
            AnimateStopAttack();
            target = null;
        }

        #region Weapon

        private void SpawnWeapon()
        {
            if (currentWeapon == null || animator == null) return;
            Transform handTransform = currentWeapon.GetGripHand switch
            {
                WeaponTypeSO.Hand.Right => rightHandTransform,
                WeaponTypeSO.Hand.Left => leftHandTransform,
                _ => leftHandTransform
            };
            currentWeapon.Spawn(handTransform, animator);
        }

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
