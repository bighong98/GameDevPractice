using System;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat;
using TH.Core.Service;
using TH.Combat.Service;
using TH.Item;
using TH.Resource;
using TH.Utils;
using UnityEngine;

public interface IFighter : IAttacker {}
public class Fighter : MonoBehaviour, IFighter
{
    [SerializeField] private Health target;
    
    // IAttacker
    public event Action OnAttack; // 공격 시도 시
    public event Action<Health> OnTargetSet; // 공격 타겟(target) 변경 시
    public event Action OnAttackReady; // 공격 준비 완료 시 (공격 가능한 적 한정)
    public bool IsTargetInRange 
        => target.IsNotNull() && currentWeapon.IsNotNull() &&
           (Vector3.Distance(transform.position, target.transform.position) <= currentWeapon.AttackRange);
    public bool IsTargetValid => target.IsNotNull() && !target.IsDead;
    public Health Target => target;
    private bool IsEquippingWeapon => currentWeapon != null;
    private WeaponTypeSO currentWeapon; // 현재 장착 중인 무기
    
    // 외부 서비스
    private ICombatSystem combatSystem;
    // 객체 컴포넌트
    private IStatHolder statHolder;
    private EquipmentHolder equipHolder;
    
    private void Awake()
    {
        combatSystem = ServiceLocator.Get<ICombatSystem>();
        
        if (!TryGetComponent(out statHolder))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No IStatHolder found");
        if (!TryGetComponent(out equipHolder))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No EquipmentHolder found");
    }

    private void Start()
    {
        SyncEquippedWeaponFromHolder();
    }

    private void OnEnable()
    {
        if (equipHolder.IsNotNull())
            equipHolder.OnEquipWeapon += HandleEquipWeapon;
    }

    private void OnDisable()
    {
        if (equipHolder.IsNotNull())
            equipHolder.OnEquipWeapon -= HandleEquipWeapon;
    }

    private float timeBetweenAttacks = 1f; // todo: move to equipped weapon
    private float timeSinceLastAttack;
    private void Update()
    {
        timeSinceLastAttack += Time.deltaTime;

        if (!IsEquippingWeapon) return;
        if (!IsTargetValid) return;
        if (timeSinceLastAttack >= timeBetweenAttacks)
        {
            this.Log($"[FighterRefactoring] OnAttackReady.Invoke()", Logg.LoggingMode.Completed);
            OnAttackReady?.Invoke();
        }
    }

    #region IAttackable

    public void Attack()
    {
        timeSinceLastAttack = 0f;
    }
    
    public void SetTarget(Health attackTarget)
    {
        // if (!newTarget.IsNotNull()) return;
        target = attackTarget;
        OnTargetSet?.Invoke(target);
    }

    public bool CanAttack(GameObject attackTarget, out Health targetHealth)
    {
        if (attackTarget == null || attackTarget == gameObject)
        {
            targetHealth = null;
            return false;
        }
            
        if (attackTarget.GetComponent<Health>() is { } health)
        {
            targetHealth = health;
            return true;
        }

        targetHealth = null;
        return false;
    }

    #endregion
    
    #region Animation Event Method

    void Hit() // Animation Event Method
    {
        if (!IsTargetValid) return;
        
        Shoot();
        combatSystem.ApplyHit(currAttackSource.ToRequest(target));
    }

    void Shoot()
    {
                SoundManager.Instance.Play(Enums.AudioType.Effect, currentWeapon.AttackSFX);
        OnAttack?.Invoke();
    }

    #endregion

    #region CombatSystem Base (임시)

        private AttackSource currAttackSource;
    private GameStatSO pendingAttackStat;
    private bool isAttackStatPending;

    private void ChangeAttackSource()
    {
        if (currentWeapon is not { } currentWeaponValue || currentWeaponValue.IsNull() ||
            currentWeaponValue.AttackSourceStatSO is not { } newAtkSrcStatSO || newAtkSrcStatSO.IsNull() || 
            statHolder.IsNull()) 
        {
            this.LogWarning($"ChangeAttackSource() - invalid currentWeapon value", context: this);
            return;
        }

        if (!statHolder.TryGetStat(newAtkSrcStatSO, out var atkSrcStat))
        {
            QueueAttackSourceRefresh(newAtkSrcStatSO);
            return;
        }

        ClearPendingAttackStat();
        currAttackSource = new AttackSource(this, atkSrcStat, currentWeaponValue.DamageType);
    }

    private void QueueAttackSourceRefresh(GameStatSO statSO)
    {
        if (statSO.IsNull() || statHolder.IsNull()) return;
        if (isAttackStatPending) return;

        pendingAttackStat = statSO;
        isAttackStatPending = true;
        statHolder.BindEvent(statSO, HandleAttackStatReady, pending: true);
    }

    private void HandleAttackStatReady()
    {
        if (!isAttackStatPending || pendingAttackStat.IsNull() || statHolder.IsNull()) return;
        if (!statHolder.TryGetStat(pendingAttackStat, out _)) return;

        
        ClearPendingAttackStat();
        
        ChangeAttackSource();
    }

    private void ClearPendingAttackStat()
    {
        isAttackStatPending = false;
        pendingAttackStat = null;
    }


    #endregion
    #region Weapon Sync

    private void HandleEquipWeapon(WeaponTypeSO weaponType)
    {
        ApplyEquippedWeapon(weaponType, forceNotify: true);
    }

    private void SyncEquippedWeaponFromHolder()
    {
        if (equipHolder.IsNull()) return;

        var weapon = equipHolder.GetEquippedWeaponInfo;
        if (weapon.IsNotNull())
            ApplyEquippedWeapon(weapon);
    }

    private void ApplyEquippedWeapon(WeaponTypeSO weaponType, bool forceNotify = false)
    {
        if (weaponType.IsNull()) return;
        if (!forceNotify && currentWeapon == weaponType) return;

        currentWeapon = weaponType;
        ChangeAttackSource();
        
    }

    #endregion
    
}
