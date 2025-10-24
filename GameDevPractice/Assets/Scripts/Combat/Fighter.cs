using System;
using GameDevTV.Utils;
using UnityEngine;
using RPG.Movement;
using RPG.Core;
using RPG.Attribute;
using TH.Attribute.Stat;
using TH.Combat;
using TH.Core.Service;
using TH.Item;

namespace RPG.Combat
{
    public class Fighter : MonoBehaviour, IAction, IAttackable
    {
        [SerializeField] private float timeBetweenAttacks = 1f; // todo: move to equipped weapon
        private float timeSinceLastAttack = 0;
        
        private LazyValue<WeaponTypeSO> currentWeapon; // 현재 장착 중인 무기
        [SerializeField] private WeaponTypeSO defaultWeapon; // 장비 장착해제시 적용되어야할 무기종(ex-Unarmed)

        private ICombatSystem combatSystem; 
        [SerializeField] private Health target;
        
        private Mover mover;
        private Animator animator;
        private IStatHolder statHolder;
        private IEquipmentHolder equipHolder;
        
        private static readonly int Attack1 = Animator.StringToHash("attack");
        private static readonly int StopAttack = Animator.StringToHash("stopAttack");
        public ActoinScheduler ActionScheduler { get; private set; }
        
        public event Action<WeaponTypeSO, Animator> OnEquipWeapon; // 장비 변경(장착, 장착해제) 시, Equipper.cs 에서 사용
        public event Action OnAttack; // 공격 시도 시
        public event Action<Health> OnTargetChanged; // 공격 타겟(target) 변경 시
        
        public bool IsEquippingWeapon => currentWeapon != null;
        public (WeaponTypeSO weapon, Animator animator) GetWeaponEquipperInfo => (this.currentWeapon.value, this.animator);

        private void Awake()
        {
            mover = GetComponent<Mover>();
            animator = GetComponent<Animator>();
            ActionScheduler = GetComponent<ActoinScheduler>();
            statHolder = GetComponent<IStatHolder>();
            equipHolder = GetComponent<IEquipmentHolder>();

            currentWeapon = new LazyValue<WeaponTypeSO>(SetDefaultWeapon);
        }

        private void Start()
        {
            equipHolder.OnEquipmentChanged += OnEquipmentChanged;
            
            if (ServiceLocator.TryGet(out ICombatSystem combat))
            {
                combatSystem = combat;
            }
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

        private bool IsInRange => Vector3.Distance(transform.position, target.transform.position) < currentWeapon.value.GetRange;
        
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

        #region CombatSystem Base (임시)

        private AttackSource currAttackSource;

        private void ChangeAttackSource()
        {
            if (statHolder.GetStat(statType: GameStats.AD) is { } result)
            {
                currAttackSource = new AttackSource(this, result.Value);
            }
            // if (currentWeapon is { value: { } weaponData })
            // {
            //     currAttackSource = new AttackSource(this, weaponData.GetDamage);
            // }
        }

        #endregion

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

        #region Weapon

        private void OnEquipmentChanged(object sender, EquipArgs args)
        {
            if (args.Item is not { GetItemInfo: WeaponTypeSO weaponData }) return;
            if (args.State == EquipArgs.EquipEventState.Equip)
            {
                EquipWeapon(weaponData);
            }
            else
            {
                UnEquipWeapon();
            }
        }

        private WeaponTypeSO SetDefaultWeapon() 
        {
            return defaultWeapon; // defaultWeapon:null 인 케이스 방어 없음
        }
        
        public void EquipWeapon(WeaponTypeSO weaponTypeSO)
        {
            this.currentWeapon.value = weaponTypeSO;
            ChangeAttackSource();
            OnEquipWeapon?.Invoke(weaponTypeSO, animator);
        }

        public void UnEquipWeapon()
        {
            if (defaultWeapon != null)
            {
                currentWeapon.value = defaultWeapon;
                OnEquipWeapon?.Invoke(defaultWeapon, animator);
            }
        }

        #endregion
    }
}
