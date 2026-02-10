using System;
using TH.Attribute;
using TH.Combat;
using TH.Combat.Service;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;

// 공격 가능한 전투 주체를 나타내는 인터페이스
public interface IFighter : IAttacker { }

// 타겟 지정, 스킬 소비, 실제 피격 적용을 담당하는 전투 실행 컴포넌트
public class Fighter : MonoBehaviour, IFighter
{
    // 현재 공격 대상
    [SerializeField] private Health target;

    // 공격 애니메이션 이벤트에서 실제 투사/타격이 발생했을 때 알림
    public event Action OnAttack;
    // 타겟 변경 시 알림
    public event Action<Health> OnTargetSet;
    // 공격 가능 조건이 충족되었을 때 알림
    public event Action OnAttackReady;

    // 현재 타겟이 있고 활성 스킬 사거리 안에 있는지 여부 검사
    public bool IsTargetInRange
    {
        get
        {
            if (!IsTargetValid) return false;
            if (skillController.IsNull() || !skillController.HasActiveSkill) return false;

            return Vector3.Distance(transform.position, target.transform.position) <= skillController.ActiveSkillRange;
        }
    }

    // 현재 타겟이 유효하고 생존 상태인지 여부 검사
    public bool IsTargetValid => target.IsNotNull() && !target.IsDead;
    // 현재 타겟 참조
    public Health Target => target;

    // 실제 데미지 적용 시스템
    private ICombatSystem combatSystem;
    // 스킬 소비/해석 컨트롤러
    private ISkillController skillController;

    // 현재 타격에 사용할 공격 소스 캐시
    private AttackSource currAttackSource;
    // 이번 타격에 우선 사용할 SFX 캐시
    private AudioClip currAttackSfx;

    private void Awake()
    {
        combatSystem = ServiceLocator.Get<ICombatSystem>();

        if (!TryGetComponent(out skillController))
            Logg.LogWarning($"[{gameObject.name}.{GetType().Name}] No ISkillController found");
    }

    private void Start()
    {
        RefreshPreviewAttackSource();
    }

    private void OnEnable()
    {
        if (skillController.IsNotNull())
            skillController.OnActiveSkillChanged += HandleActiveSkillChanged;
    }

    private void OnDisable()
    {
        if (skillController.IsNotNull())
            skillController.OnActiveSkillChanged -= HandleActiveSkillChanged;
    }

    private void Update()
    {
        if (!IsTargetValid) return;
        if (skillController.IsNull() || !skillController.HasActiveSkill) return;
        if (!skillController.IsActiveSkillReady) return;

        // 스킬 사용 조건이 만족 시 상태머신 트리거 이벤트를 발행
        OnAttackReady?.Invoke();
    }

    #region IAttackable

    // 활성 스킬을 사용해 대미지 계산에 사용되는 AttackSource 구조체 생성 
    public void Attack()
    {
        if (skillController.IsNull()) return;
        currAttackSfx = null;
        if (!skillController.TryConsumeActiveSkill(this, out currAttackSource)) return;

        currAttackSfx = skillController.ResolvedSkillSFX;
    }

    // 공격 타겟 지정
    public void SetTarget(Health attackTarget)
    {
        target = attackTarget;
        OnTargetSet?.Invoke(target);
    }

    // 대상(attackTarget)이 공격 가능한 오브젝트인지 확인하고 Health 컴포넌트 반환
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

    // 애니메이션 이벤트: 실제 타격 프레임에서 호출되어 피격을 적용한다
    void Hit()
    {
        if (!IsTargetValid) return;

        Shoot();
        combatSystem.ApplyHit(currAttackSource.ToRequest(target));
    }

    // 애니메이션 이벤트: 투사/사운드/공격 이벤트를 발생 (Hit()과 함께 사용, 현재는 원거리 애니메이션에서 사용)
    void Shoot()
    {
        var attackSfx = ResolveCurrentAttackSfx();
        if (attackSfx != null)
            SoundManager.Instance.Play(Enums.AudioType.Effect, attackSfx);

        OnAttack?.Invoke();
    }

    #endregion

    // 활성 스킬 변경 시 프리뷰 공격 소스를 재생성
    private void HandleActiveSkillChanged(SkillTypeSO _)
    {
        RefreshPreviewAttackSource();
    }

    // 스킬이 바뀌면 다음 Attack 전까지 사용할 미리보기 공격 소스를 갱신
    private void RefreshPreviewAttackSource()
    {
        if (skillController.IsNull()) return;

        if (skillController.TryBuildPreviewAttackSource(this, out var previewAttackSource))
            currAttackSource = previewAttackSource;
    }

    // 현재 공격에 사용할 SFX를 우선순위 기반으로 선택
    private AudioClip ResolveCurrentAttackSfx()
    {
        if (currAttackSfx != null)
            return currAttackSfx;

        if (skillController.IsNotNull() && skillController.HasResolvedSkill)
            return skillController.ResolvedSkillSFX;

        if (skillController.IsNotNull() && skillController.HasActiveSkill)
            return skillController.ActiveSkillSFX;

        return null;
    }
}
