using System;
using RPG.Attribute;
using RPG.Combat;
using RPG.Core;
using RPG.Movement;
using RPG.Saving;
using TH.Animate;
using TH.Attribute.Stat;
using TH.Core.Service;
using TH.Item;
using TH.Utils;
using UnityEngine;
using TH.Resource;

namespace TH.Combat
{
    public class Fighter : MonoBehaviour, IFighter, IAction, IAttackable
    {
        public ActoinScheduler ActionScheduler { get; private set; }

        public event Action OnAttack; // 공격 시도 시
        public event Action<Health> OnTargetChanged; // 공격 타겟(target) 변경 시
        
        private ICombatSystem combatSystem;
        private Mover mover;
        private Animator animator;
        private IStatHolder statHolder;
        private IEquipmentHolder equipHolder;

        private IWeaponSpawner weaponSpawner = new WeaponSpawner();
        private IAnimatorOverrideHandler animatorOverrider;

        private static readonly int Attack1 = Animator.StringToHash("attack");
        private static readonly int StopAttack = Animator.StringToHash("stopAttack");

        private float timeSinceLastAttack = 0;
        [SerializeField] private float timeBetweenAttacks = 1f; // todo: move to equipped weapon
        [SerializeField] private Health target;

        private LazyValue<WeaponTypeSO> currentWeapon;
        
        private void Awake()
        {
            mover = GetComponent<Mover>();
            ActionScheduler = GetComponent<ActoinScheduler>();
            statHolder = GetComponent<IStatHolder>();
            equipHolder = GetComponent<IEquipmentHolder>();
            
            if (TryGetComponent<Animator>(out animator))
                animatorOverrider = new WeaponAnimatorOverrideHandler(animator);
            
            InitializeWeaponSpawner();

            combatSystem = ServiceLocator.Get<ICombatSystem>();
        }
        
        private void Update()
        {
            timeSinceLastAttack += Time.deltaTime;
            if (!IsEquippingWeapon) return;
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

        #region Intiailization

        private const string DefaultRootName = "Root";
        private const string DefaultRightHandContainerName = "hand_r";
        private const string DefaultLeftHandContainerName = "hand_l";
        private const string DefaultRightWeaponContainerName = "weapon_r";
        private const string DefaultLeftWeaponContainerName = "weapon_l";
        
        private void InitializeWeaponSpawner()
        {
            if (Util.FindChild(gameObject, DefaultRootName, recursive: true) is { } root)
            {
                if (Util.FindChildContainName<Transform>(root, DefaultRightHandContainerName, true, false) is {} rResult)
                {
                    var right = new GameObject(DefaultRightWeaponContainerName);
                    var rightTrs = right.transform;
                    rightTrs.SetParent(rResult, worldPositionStays: false);
                    weaponSpawner.SetHandRoot(WeaponTypeSO.Hand.Right, rightTrs);
                }
                
                if (Util.FindChildContainName<Transform>(root, DefaultLeftHandContainerName, true, false) is {} lResult)
                {
                    var left = new GameObject(DefaultLeftWeaponContainerName);
                    var leftTrs = left.transform;
                    leftTrs.SetParent(lResult, worldPositionStays: false);
                    weaponSpawner.SetHandRoot(WeaponTypeSO.Hand.Left, leftTrs);
                }
            }
        }

        #endregion
        
        public bool IsEquippingWeapon => currentWeapon != null;
        private bool IsInRange => Vector3.Distance(transform.position, target.transform.position) < currentWeapon.Value.GetRange;
        
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
        
        private AttackSource currAttackSource;

        private void ChangeAttackSource()
        {
            
        }
        
        #region Animation Event Method

        void Hit() // Animation Event Method
        {
            if (target == null) return;
            OnAttack?.Invoke();
            combatSystem.ApplyHit(currAttackSource.ToRequest(target));
            // target.TakeDamage(currentWeapon.value.GetDamage);
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
    }
}

