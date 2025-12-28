using System;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat;
using TH.Core.Service;
using TH.Item;
using TH.Resource;
using TH.Utils;
using UnityEngine;

public interface IFighter : IAttacker, IWeaponEquipHandler {}
public class FighterRefactoring : MonoBehaviour, IFighter
{
    [SerializeField] private Health target;
    
    // IAttacker
    public event Action OnAttack; // 공격 시도 시
    public event Action<Health> OnTargetChanged; // 공격 타겟(target) 변경 시
    public event Action OnAttackReady; // 공격 준비 완료 시 (공격 가능한 적 한정)
    
    
    public bool IsTargetInRange 
        => target.IsNotNull() && (Vector3.Distance(transform.position, target.transform.position) <= currentWeapon.Value.GetRange);
    public bool IsTargetValid => target.IsNotNull() && !target.IsDead;
    public Health Target => target;

    // IWeaponEquipHandler
    public event Action<WeaponTypeSO, Animator> OnEquipWeapon;
    
    public bool IsEquippingWeapon => currentWeapon != null;
    public (WeaponTypeSO weapon, Animator animator) GetWeaponEquipperInfo => (currentWeapon.Value, animator);
    
    private LazyValue<WeaponTypeSO> currentWeapon; // 현재 장착 중인 무기
    [SerializeField] private WeaponTypeSO defaultWeapon; // 장비 장착해제시 적용되어야할 무기종(ex-Unarmed)
    
    // 외부 서비스
    private ICombatSystem combatSystem;
    // 객체 컴포넌트
    private IStatHolder statHolder;
    private IEquipmentHolder equipHolder;
    private Animator animator;
    
    private void Awake()
    {
        combatSystem = ServiceLocator.Get<ICombatSystem>();
        
        if (!TryGetComponent(out statHolder))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No IStatHolder found");
        if (!TryGetComponent(out equipHolder))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No IEquipmentHolder found");
        if (!TryGetComponent(out animator))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No Animator found");

        currentWeapon = new LazyValue<WeaponTypeSO>(SetDefaultWeapon);
    }

    private void Start()
    {
        var equipSlots = equipHolder.ItemSlots;
        if (equipSlots.Count == 0)
        {
            EquipWeapon(defaultWeapon);
            return;
        }

        foreach (var slot in equipSlots)
        {
            if (slot?.GetItem is { GetItemInfo: WeaponTypeSO weaponData })
            {
                EquipWeapon(weaponData);
                break;
            }
        }
    }

    private void OnEnable()
    {
        equipHolder.OnEquipmentChanged += OnEquipmentChanged;
    }

    private void OnDisable()
    {
        if (!equipHolder.IsNotNull()) return;
        equipHolder.OnEquipmentChanged -= OnEquipmentChanged;
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
    
    public void Attack(Health attackTarget)
    {
        ChangeTarget(attackTarget);
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
    
    private void ChangeTarget(Health newTarget)
    {
        // if (!newTarget.IsNotNull() || (target.IsNotNull() && target == newTarget)) return;
        if (!newTarget.IsNotNull()) return;

        target = newTarget;
        OnTargetChanged?.Invoke(target);
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
        SoundManager.Instance.Play(Enums.AudioType.Effect, currentWeapon.Value.AttackSFX);
        OnAttack?.Invoke();
    }

    #endregion

    #region CombatSystem Base (임시)

    private AttackSource currAttackSource;

    private void ChangeAttackSource()
    {
        if (statHolder?.GetStat(statType: GameStats.AD) is { } result)
        {
            currAttackSource = new AttackSource(this, result.Value);
        }
        else
        {
            Logg.LogError($"[{gameObject.name}.Fighter] Failed to get AD stat for attack source");
        }
    }

    #endregion

    #region IWeaponEquipHandler

    private void OnEquipmentChanged(object sender, EquipArgs args)
    {
        Logg.Log($"[{gameObject.name}.Fighter] OnEquipmentChanged called {args.Item.GetItemInfo.nameString}", Logg.LoggingMode.Completed);
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
        
    private void EquipWeapon(WeaponTypeSO weaponTypeSO)
    {
        this.currentWeapon.Value = weaponTypeSO;
        ChangeAttackSource();
        OnEquipWeapon?.Invoke(weaponTypeSO, animator);
    }

    private void UnEquipWeapon()
    {
        if (defaultWeapon != null)
        {
            currentWeapon.Value = defaultWeapon;
            OnEquipWeapon?.Invoke(defaultWeapon, animator);
        }
    }

    #endregion
    
}
