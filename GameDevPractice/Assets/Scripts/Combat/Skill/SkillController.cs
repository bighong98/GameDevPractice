using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat.Service;
using TH.Core.Service;
using TH.Item;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    // 전투 시스템에서 스킬 선택/해결/쿨다운/콤보 진행을 제어 목적 인터페이스
    public interface ISkillController
    {
        // 현재 활성 스킬이 변경될 때
        event Action<SkillTypeSO> OnActiveSkillChanged;
        // 실제로 이번 공격에 적용될 스킬(콤보 해석 후)이 변경될 때
        event Action<SkillTypeSO> OnResolvedSkillChanged;
        // 쿨다운이 끝나 스킬 사용 가능 상태가 될 때
        event Action<SkillTypeSO> OnSkillReady;
        event Action OnSkillBookChanged;
        // 콤보 스텝이 변경될 때 (baseSkill, stepIndex, stepCount)
        event Action<SkillTypeSO, int, int> OnComboStepChanged;

        // 활성 스킬 보유 여부
        bool HasActiveSkill { get; }
        // 현재 선택된 활성 스킬
        SkillTypeSO ActiveSkill { get; }
        // 해석 완료 스킬 보유 여부
        bool HasResolvedSkill { get; }
        // 실제 공격에 적용될 해석 완료 스킬
        SkillTypeSO ResolvedSkill { get; }
        // 현재 활성 스킬의 즉시 사용 가능 여부
        bool IsActiveSkillReady { get; }
        // 현재 공격 미리보기 기준 사거리
        float ActiveSkillRange { get; }
        // 현재 공격 미리보기 기준 SFX
        AudioClip ActiveSkillSFX { get; }
        // 마지막으로 해석된 스킬 기준 SFX
        AudioClip ResolvedSkillSFX { get; }
        // 현재 콤보 스텝 인덱스
        int CurrentComboStepIndex { get; }
        // 현재 콤보 전체 스텝 수
        int CurrentComboStepCount { get; }
        IReadOnlyList<SkillTypeSO> RegisteredSkills { get; }

        // 스킬 등록.
        bool RegisterSkill(SkillTypeSO skill, bool setActive = false);
        // 활성 스킬 전환.
        bool SetActiveSkill(SkillTypeSO skill);
        // 특정 스킬의 남은 쿨다운 조회.
        
        float GetRemainingCooldown(SkillTypeSO skill);
        bool TryGetActiveSequenceTimeout(SkillTypeSO skill, out float remainingTimeout, out float totalTimeout);
        // 활성 스킬을 실제로 소비하고 공격 소스를 생성.
        bool TryConsumeActiveSkill(IAttacker attacker, out AttackSource attackSource);
        // 소비 없이 현재 기준 공격 소스 미리보기 생성.
        bool TryBuildPreviewAttackSource(IAttacker attacker, out AttackSource attackSource);
        bool TryExecutePendingAttack(IAttacker attacker, Health target);
        void SetProjectileExecutor(ISkillProjectileExecutor executor);
        void ClearProjectileExecutor(ISkillProjectileExecutor executor);
    }

    public interface ISkillProjectileExecutor
    {
        bool TryExecuteProjectile(in AttackSource attackSource, Health target, SkillTypeSO skill);
    }

    // 플레이어(또는 전투 유닛)의 스킬 실행 상태를 관리하는 핵심 컨트롤러.
    public sealed class SkillController : MonoBehaviour, ISkillController, ISkillExecutionServices
    {
        // AttackSource 식별자 충돌을 막기 위한 전역 시퀀스.
        private static readonly SkillTypeSO[] EmptySkills = Array.Empty<SkillTypeSO>();
        private static int attackSequence;

        [Header("Initial Skills")]
        // 시작 시 자동 등록할 스킬 목록.
        [SerializeField] private List<SkillTypeSO> initialSkills = new();
        // 시작 시 우선 활성화할 스킬.
        [SerializeField] private SkillTypeSO defaultActiveSkill;
        // 장착 무기 기본 스킬과 자동 동기화할지 여부.
        [SerializeField] private bool syncWithEquippedWeapon = true;
        [Header("Targeting")]
        [SerializeField] private SkillTargetLayerMapSO skillTargetLayerMap;

        // 능력치 기반 데미지 계산에 사용할 스탯 홀더.
        private IStatHolder statHolder;
        // 장비 변경 이벤트 구독 대상.
        private EquipmentHolder equipHolder;
        private ICombatSystem combatSystem;
        private ISkillProjectileExecutor projectileExecutor;
        private SkillTargetingEvaluator targetingEvaluator;

        // 등록 스킬/활성 스킬 저장소.
        private SkillBook skillBook;
        // 쿨다운 및 사용 가능 상태 관리기.
        private SkillCaster skillCaster;
        // 베이스 스킬별 콤보 진행 상태 캐시.
        
        private readonly Dictionary<SkillTypeSO, ComboContext> comboContexts = new();
        private readonly List<float> reusableModifiedHitDamages = new(8);
        private readonly List<float> primaryPendingHitDamages = new(8);
        private readonly List<float> secondaryPendingHitDamages = new(8);
        private bool isActiveComboTimeoutPending;
        private SkillTypeSO activeComboTimeoutBaseSkill;
        private int activeComboTimeoutExpectedNextStepIndex;
        private float activeComboTimeoutAt;

        // 현재 해석 완료된 실제 적용 스킬.
        private SkillTypeSO resolvedSkill;
        // resolvedSkill의 기준이 된 베이스 스킬.
        private SkillTypeSO resolvedBaseSkill;
        // 현재 콤보 스텝 인덱스.
        private int currentComboStepIndex;
        // 현재 콤보 총 스텝 수.
        private int currentComboStepCount = 1;
        private bool hasPendingAttack;
        private AttackSource pendingAttackSource;
        private SkillTypeSO pendingAttackSkill;
        
        private SkillTypeSO pendingBaseSkill;
        private int pendingComboStepIndex;
        private int pendingComboStepCount = 1;
        private IAttacker pendingAttacker;
        private readonly List<Health> areaTargetsBuffer = new();
        private Collider[] overlapBuffer = new Collider[32];

        // 활성 스킬이 바뀔 때 발행.
        public event Action<SkillTypeSO> OnActiveSkillChanged;
        // 해석 스킬이 바뀔 때 발행.
        public event Action<SkillTypeSO> OnResolvedSkillChanged;
        // 스킬이 실제 소비됐을 때 발행.
        public event Action<SkillTypeSO> OnSkillConsumed;
        // 쿨다운 해제(준비 완료) 시 발행.
        public event Action<SkillTypeSO> OnSkillReady;
        public event Action OnSkillBookChanged;
        // 콤보 진행 상태 변경 시 발행.
        public event Action<SkillTypeSO, int, int> OnComboStepChanged;

        // 활성 스킬 존재 여부.
        public bool HasActiveSkill => skillBook != null && skillBook.ActiveSkill.IsNotNull();
        // 현재 활성 스킬.
        public SkillTypeSO ActiveSkill => skillBook?.ActiveSkill;
        // 해석된 스킬 존재 여부.
        public bool HasResolvedSkill => resolvedSkill.IsNotNull();
        // 현재 해석 스킬.
        public SkillTypeSO ResolvedSkill => resolvedSkill;
        // 활성 스킬의 즉시 사용 가능 여부.
        public bool IsActiveSkillReady => HasActiveSkill && skillCaster.IsReady(skillBook.ActiveSkill);
        // 현재 프리뷰 스킬 기준 사거리.
        public float ActiveSkillRange
        {
            get
            {
                var previewSkill = GetPreviewSkill();
                return previewSkill.IsNotNull() ? Mathf.Max(0f, previewSkill.Range) : 0f;
            }
        }

        // 현재 프리뷰 스킬 기준 캐스트 SFX.
        public AudioClip ActiveSkillSFX
        {
            get
            {
                var previewSkill = GetPreviewSkill();
                return previewSkill.IsNotNull() ? previewSkill.CastSFX : null;
            }
        }

        // 마지막 해석 스킬 기준 캐스트 SFX.
        public AudioClip ResolvedSkillSFX => HasResolvedSkill ? resolvedSkill.CastSFX : null;
        // 현재 콤보 인덱스.
        public int CurrentComboStepIndex => currentComboStepIndex;
        // 현재 콤보 스텝 수.
        public int CurrentComboStepCount => currentComboStepCount;
        public IReadOnlyList<SkillTypeSO> RegisteredSkills => skillBook?.Skills ?? EmptySkills;
        public SkillTargetLayerMapSO SkillTargetLayerMap => skillTargetLayerMap;

        // 컴포넌트 참조 및 초기 스킬 상태를 구성한다.
        private void Awake()
        {
            TryGetComponent(out statHolder);
            TryGetComponent(out equipHolder);

            skillBook = new SkillBook();
            skillCaster = new SkillCaster();
            targetingEvaluator = new SkillTargetingEvaluator(new SkillTargetLayerMaskResolver(skillTargetLayerMap));

            for (int i = 0; i < initialSkills.Count; i++)
            {
                RegisterSkill(initialSkills[i]);
            }

            if (defaultActiveSkill.IsNotNull())
            {
                SetActiveSkill(defaultActiveSkill);
            }
            else if (!HasActiveSkill && skillBook.TryGetFirst(out var firstSkill))
            {
                SetActiveSkill(firstSkill);
            }

            UpdateResolvedSkillFromPreview(forceNotify: HasActiveSkill);
            SyncDebugValues();
        }

        // 시작 시 이미 장착된 무기의 기본 스킬을 반영한다.
        private void Start()
        {
            if (syncWithEquippedWeapon && equipHolder is { IsEquippingWeapon: true, GetEquippedWeaponInfo: { } weapon })
            {
                HandleEquipWeapon(weapon);
            }
        }

        // 무기 장착 이벤트 구독.
        private void OnEnable()
        {
            if (syncWithEquippedWeapon && equipHolder.IsNotNull())
            {
                equipHolder.OnEquipWeapon += HandleEquipWeapon;
            }
        }

        // 무기 장착 이벤트 구독 해제.
        private void OnDisable()
        {
            if (syncWithEquippedWeapon && equipHolder.IsNotNull())
            {
                equipHolder.OnEquipWeapon -= HandleEquipWeapon;
            }

            CancelActiveComboTimeoutRoutine();
        }

        // 매 프레임 스킬 준비 상태를 폴링하고 디버그 값을 동기화한다.
        private void Update()
        {
            if (skillBook == null || skillCaster == null) return;

            if (hasPendingAttack && !IsPendingAttackReusableState())
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedByStateChange, refreshResolvedFromPreview: true);
            }

            if (isActiveComboTimeoutPending && Time.time >= activeComboTimeoutAt)
            {
                isActiveComboTimeoutPending = false;

                if (HasActiveSkill && skillBook.ActiveSkill == activeComboTimeoutBaseSkill)
                {
                    var context = GetOrCreateComboContext(activeComboTimeoutBaseSkill);
                    if (context.NextStepIndex == activeComboTimeoutExpectedNextStepIndex)
                    {
                        UpdateResolvedSkillFromPreview(forceNotify: true);
                    }
                }
            }

            skillCaster.PollReady(skillBook.Skills, skill =>
            {
                OnSkillReady?.Invoke(skill);
            });

            SyncDebugValues();
        }

        // 무기 교체 시 해당 무기의 기본 스킬을 등록하고 활성화한다.
        private void HandleEquipWeapon(WeaponTypeSO weapon)
        {
            if (weapon.IsNull() || weapon.DefaultSkill.IsNull()) return;

            RegisterSkill(weapon.DefaultSkill, setActive: true);
        }

        // 스킬을 스킬북/캐스터에 등록하고 필요 시 활성 스킬로 설정한다.
        public bool RegisterSkill(SkillTypeSO skill, bool setActive = false)
        {
            if (skill.IsNull() || skillBook == null || skillCaster == null) return false;

            bool added = skillBook.Register(skill);
            skillCaster.TrackSkill(skill);

            if (added)
            {
                OnSkillBookChanged?.Invoke();
            }

            if (setActive)
            {
                SetActiveSkill(skill);
            }

            return added;
        }

        // 활성 스킬을 교체하고 콤보 상태/프리뷰 해석 결과를 갱신한다.
        public bool SetActiveSkill(SkillTypeSO skill)
        {
            if (skill.IsNull() || skillBook == null) return false;

            if (!skillBook.Contains(skill))
            {
                RegisterSkill(skill);
            }

            bool changed = skillBook.SetActive(skill);
            if (changed)
            {
                CancelActiveComboTimeoutRoutine();
                ResetComboProgress(skill);
                ClearPendingAttack();
                OnActiveSkillChanged?.Invoke(skill);
            }

            UpdateResolvedSkillFromPreview(forceNotify: changed || !HasResolvedSkill);
            SyncDebugValues();
            return changed;
        }

        // 스킬의 남은 쿨다운을 반환한다.
        public float GetRemainingCooldown(SkillTypeSO skill)
        {
            if (skill.IsNull() || skillCaster == null) return 0f;
            return skillCaster.GetRemainingCooldown(skill);
        }

        public bool TryGetActiveSequenceTimeout(SkillTypeSO skill, out float remainingTimeout, out float totalTimeout)
        {
            remainingTimeout = 0f;
            totalTimeout = 0f;

            if (skill.IsNull() || !HasActiveSkill || skillBook == null || skillBook.ActiveSkill != skill)
                return false;

            if (skill.ComboSequence is not { HasSteps: true } comboSequence)
                return false;

            int stepCount = Mathf.Max(1, comboSequence.StepCount);
            if (stepCount <= 1)
                return false;

            float comboTimeout = Mathf.Max(0f, comboSequence.ComboTimeout);
            if (comboTimeout <= 0f)
                return false;

            var context = GetOrCreateComboContext(skill);
            if (context.LastConsumeTime < 0f || context.NextStepIndex <= 0)
                return false;

            float elapsed = Time.time - context.LastConsumeTime;
            if (elapsed >= comboTimeout)
                return false;

            remainingTimeout = comboTimeout - elapsed;
            totalTimeout = comboTimeout;
            return true;
        }


        // 활성 스킬을 실제 소비해 공격 요청에 필요한 AttackSource를 만든다.
        public bool TryConsumeActiveSkill(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;

            if (!HasActiveSkill || attacker.IsNull()) return false;

            if (hasPendingAttack)
            {
                if (TryReusePendingAttack(attacker, out attackSource))
                {
                    SyncDebugValues();
                    return true;
                }

                CancelPendingAttack(PendingCancelReason.InvalidatedOnConsume, refreshResolvedFromPreview: true);
            }

            var baseSkill = skillBook.ActiveSkill;
            if (!skillCaster.IsReady(baseSkill)) return false;
            if (!TryResolveSkillPreview(baseSkill, out var resolved, out var stepIndex, out var stepCount)) return false;

            int attackInstanceId = TakeNextAttackInstanceId();
            if (!TryBuildAttackSource(attacker, resolved, attackInstanceId, out attackSource)) return false;

            float cooldownTimingScale = ResolveSkillTimingScale(attacker);
            float scaledCooldown = baseSkill.Cooldown / Mathf.Max(0.01f, cooldownTimingScale);
            if (!skillCaster.Consume(baseSkill, scaledCooldown))
            {
                attackSource = default;
                return false;
            }

            CommitComboProgress(baseSkill, stepIndex, stepCount);
            ScheduleActiveComboTimeout(baseSkill, stepCount);
            SetPendingAttack(attacker, resolved, attackSource, baseSkill, stepIndex, stepCount);
            UpdateResolvedSkillFromPreview(forceNotify: true);

            if (attacker is Component attackerComponent)
            {
                SkillEffectPlayer.TryPlaySkillEffect(resolved, attackerComponent.transform);
            }

            OnSkillConsumed?.Invoke(resolved);
            SyncDebugValues();
            return true;
        }

        // 현재 시점 기준으로 실제 소비 없이 공격 소스 프리뷰를 생성한다.
        public bool TryBuildPreviewAttackSource(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;
            if (!HasActiveSkill || attacker.IsNull()) return false;

            var previewSkill = GetPreviewSkill();
            if (previewSkill.IsNull()) return false;

            return TryBuildAttackSource(attacker, previewSkill, 0, out attackSource);
        }

        public bool TryExecutePendingAttack(IAttacker attacker, Health target)
        {
            if (!hasPendingAttack) return false;

            if (attacker.IsNull() || target.IsNull())
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedOnExecute, refreshResolvedFromPreview: true);
                return false;
            }

            if (!ReferenceEquals(pendingAttacker, attacker) || !IsPendingAttackReusableState())
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedOnExecute, refreshResolvedFromPreview: true);
                return false;
            }

            bool executed = ExecutePendingAttack(target);
            if (executed)
            {
                ClearPendingAttack();
                UpdateResolvedSkillFromPreview(forceNotify: true);
                return true;
            }

            CancelPendingAttack(PendingCancelReason.ExecutionRejected, refreshResolvedFromPreview: true);
            return false;
        }

        public void SetProjectileExecutor(ISkillProjectileExecutor executor)
        {
            projectileExecutor = executor;
        }

        public void ClearProjectileExecutor(ISkillProjectileExecutor executor)
        {
            if (projectileExecutor == executor)
            {
                projectileExecutor = null;
            }
        }

        // 활성 스킬 기준으로 현재 프리뷰 스킬(콤보 반영)을 반환한다.
        private SkillTypeSO GetPreviewSkill()
        {
            if (TryGetValidPendingState(out _, out _, out _))
            {
                return pendingAttackSkill;
            }

            if (!HasActiveSkill) return null;

            if (TryResolveSkillPreview(skillBook.ActiveSkill, out var previewSkill, out _, out _))
                return previewSkill;

            return skillBook.ActiveSkill;
        }

        // 베이스 스킬을 현재 콤보 문맥에 맞는 실제 사용 스킬로 해석한다.
        private bool TryResolveSkillPreview(SkillTypeSO baseSkill, out SkillTypeSO resolved, out int stepIndex, out int stepCount)
        {
            resolved = null;
            stepIndex = 0;
            stepCount = 1;

            if (baseSkill.IsNull()) return false;

            if (baseSkill.ComboSequence is not { HasSteps: true } comboSequence)
            {
                resolved = baseSkill;
                return true;
            }

            stepCount = Mathf.Max(1, comboSequence.StepCount);
            var context = GetOrCreateComboContext(baseSkill);

            int nextStepIndex = context.NextStepIndex;
            // 타임아웃 초과 시 콤보를 0번 스텝으로 되돌린다.
            if (ShouldResetCombo(comboSequence.ComboTimeout, context))
            {
                nextStepIndex = 0;
            }

            if (nextStepIndex < 0 || nextStepIndex >= stepCount)
            {
                nextStepIndex = 0;
            }

            resolved = comboSequence.GetStepSkill(nextStepIndex, baseSkill);
            stepIndex = nextStepIndex;
            return true;
        }

        // 마지막 소비 시점과 타임아웃을 비교해 콤보 초기화 필요 여부를 계산한다.
        private static bool ShouldResetCombo(float timeout, ComboContext context)
        {
            if (context.LastConsumeTime < 0f) return true;
            if (timeout <= 0f) return true;

            return Time.time > context.LastConsumeTime + timeout;
        }

        // 스킬 소비 후 다음 콤보 스텝 인덱스를 계산해 저장한다.
        private void CommitComboProgress(SkillTypeSO baseSkill, int consumedStepIndex, int stepCount)
        {
            var context = GetOrCreateComboContext(baseSkill);
            context.LastConsumeTime = Time.time;

            if (stepCount <= 1)
            {
                context.NextStepIndex = 0;
                return;
            }

            int nextStep = consumedStepIndex + 1;
            context.NextStepIndex = nextStep < stepCount ? nextStep : 0;
        }

        private void ScheduleActiveComboTimeout(SkillTypeSO baseSkill, int stepCount)
        {
            CancelActiveComboTimeoutRoutine();

            if (!HasActiveSkill || baseSkill.IsNull() || skillBook.ActiveSkill != baseSkill)
                return;

            if (stepCount <= 1 || baseSkill.ComboSequence is not { HasSteps: true } comboSequence)
                return;

            var context = GetOrCreateComboContext(baseSkill);
            if (context.NextStepIndex <= 0)
                return;

            float comboTimeout = Mathf.Max(0f, comboSequence.ComboTimeout);
            isActiveComboTimeoutPending = true;
            activeComboTimeoutBaseSkill = baseSkill;
            activeComboTimeoutExpectedNextStepIndex = context.NextStepIndex;
            activeComboTimeoutAt = Time.time + comboTimeout;
        }



        private void CancelActiveComboTimeoutRoutine()
        {
            isActiveComboTimeoutPending = false;
            activeComboTimeoutBaseSkill = null;
            activeComboTimeoutExpectedNextStepIndex = 0;
            activeComboTimeoutAt = 0f;
        }




        // 활성 스킬 전환 시 해당 베이스 스킬의 콤보 진행을 초기화한다.
        private void ResetComboProgress(SkillTypeSO baseSkill)
        {
            if (baseSkill.IsNull()) return;

            var context = GetOrCreateComboContext(baseSkill);
            context.NextStepIndex = 0;
            context.LastConsumeTime = -1f;
        }

        // 베이스 스킬에 대응하는 콤보 문맥을 조회하거나 신규 생성한다.
        private ComboContext GetOrCreateComboContext(SkillTypeSO baseSkill)
        {
            if (!comboContexts.TryGetValue(baseSkill, out var context))
            {
                context = new ComboContext();
                comboContexts[baseSkill] = context;
            }

            return context;
        }

        // 현재 프리뷰 결과를 resolved 상태에 반영한다.
        private void UpdateResolvedSkillFromPreview(bool forceNotify)
        {
            if (TryGetValidPendingState(out var pendingBase, out var pendingStepIndex, out var pendingStepCount))
            {
                SetResolvedSkill(pendingAttackSkill, pendingBase, pendingStepIndex, pendingStepCount, forceNotify);
                return;
            }

            if (!HasActiveSkill)
            {
                SetResolvedSkill(null, null, 0, 1, forceNotify);
                return;
            }

            if (!TryResolveSkillPreview(skillBook.ActiveSkill, out var preview, out var stepIndex, out var stepCount))
            {
                SetResolvedSkill(skillBook.ActiveSkill, skillBook.ActiveSkill, 0, 1, forceNotify);
                return;
            }

            SetResolvedSkill(preview, skillBook.ActiveSkill, stepIndex, stepCount, forceNotify);
        }

        // resolved 스킬 및 콤보 인덱스를 갱신하고 필요 이벤트를 발행한다.
        private void SetResolvedSkill(SkillTypeSO skill, SkillTypeSO baseSkill, int stepIndex, int stepCount, bool forceNotify)
        {
            bool skillChanged = resolvedSkill != skill;
            bool comboChanged = resolvedBaseSkill != baseSkill ||
                               currentComboStepIndex != stepIndex ||
                               currentComboStepCount != stepCount;

            resolvedSkill = skill;
            resolvedBaseSkill = baseSkill;
            currentComboStepIndex = Mathf.Max(0, stepIndex);
            currentComboStepCount = Mathf.Max(1, stepCount);

            if (forceNotify || skillChanged)
            {
                OnResolvedSkillChanged?.Invoke(resolvedSkill);
            }

            if (forceNotify || comboChanged)
            {
                OnComboStepChanged?.Invoke(resolvedBaseSkill, currentComboStepIndex, currentComboStepCount);
            }
        }

        // 스킬 데이터와 공격자 정보를 바탕으로 최종 AttackSource를 구성한다.
        private bool ExecutePendingAttack(Health target)
        {
            var skill = pendingAttackSkill;
            float timingScale = ResolveSkillTimingScale(pendingAttacker);
            var context = new SkillExecutionContext(pendingAttacker, target, skill, pendingAttackSource, timingScale);
            if (!CanTargetWithPolicy(context, target))
            {
                return false;
            }

            if (skill.IsNotNull() && skill.ExecutionProfile is { HasActions: true } executionProfile)
            {
                StartCoroutine(executionProfile.Execute(context, this));
                return true;
            }

            if (skill.IsNotNull() && skill.HasProjectile)
            {
                if (projectileExecutor.IsNotNull() &&
                    projectileExecutor.TryExecuteProjectile(pendingAttackSource, target, skill))
                {
                    return true;
                }

                Logg.LogWarning($"[{gameObject.name}.{nameof(SkillController)}] Projectile executor missing. Falling back to direct hit.");
            }

            combatSystem ??= ServiceLocator.Get<ICombatSystem>();
            if (combatSystem == null)
            {
                return false;
            }

            combatSystem.ApplyHit(pendingAttackSource.ToRequest(target));
            return true;
        }

        private static float ResolveSkillTimingScale(IAttacker attacker)
        {
            if (attacker is Component component &&
                component.TryGetComponent<ISkillTimingScaleProvider>(out var provider))
            {
                return Mathf.Max(0.01f, provider.SkillTimingScale);
            }

            return 1f;
        }



        #region ISkillExecutionServices

        public bool TryApplyHit(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0)
        {
            if (!CanTargetWithPolicy(context, target))
            {
                return false;
            }

            var attackSource = BuildModifiedAttackSource(context.AttackSource, damageScale, hitCountOverride, allowReusableList: true);
            return TryApplyHitWithSource(attackSource, target);
        }

        public bool TryLaunchProjectile(SkillExecutionContext context, Health target, float damageScale = 1f, int hitCountOverride = 0)
        {
            if (!CanTargetWithPolicy(context, target))
            {
                return false;
            }

            bool canExecuteProjectile = projectileExecutor.IsNotNull();
            var attackSource = BuildModifiedAttackSource(context.AttackSource, damageScale, hitCountOverride,
                allowReusableList: !canExecuteProjectile);

            if (canExecuteProjectile && projectileExecutor.TryExecuteProjectile(attackSource, target, context.Skill))
            {
                return true;
            }

            return TryApplyHitWithSource(attackSource, target);
        }

        public IReadOnlyList<Health> FindTargetsInRadius(
            SkillExecutionContext context,
            Vector3 center,
            float radius,
            int maxTargets,
            Health primaryTarget,
            bool includePrimary)
        {
            areaTargetsBuffer.Clear();

            if (radius <= 0f || maxTargets <= 0)
            {
                return areaTargetsBuffer;
            }

            int layerMask = ResolveTargetLayerMask(context);
            if (layerMask == 0)
            {
                return areaTargetsBuffer;
            }

            EnsureOverlapBufferSize(maxTargets);
            int hitCount = Physics.OverlapSphereNonAlloc(
                center,
                radius,
                overlapBuffer,
                layerMask,
                QueryTriggerInteraction.Ignore);

            if (includePrimary && primaryTarget.IsNotNull() &&
                Vector3.Distance(center, primaryTarget.transform.position) <= radius &&
                CanTargetWithPolicy(context, primaryTarget))
            {
                areaTargetsBuffer.Add(primaryTarget);
            }

            int scanCount = Mathf.Min(hitCount, overlapBuffer.Length);
            for (int i = 0; i < scanCount && areaTargetsBuffer.Count < maxTargets; i++)
            {
                var collider = overlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                if (!collider.TryGetComponent<Health>(out var health))
                {
                    continue;
                }

                if (!includePrimary && health == primaryTarget)
                {
                    continue;
                }

                if (areaTargetsBuffer.Contains(health))
                {
                    continue;
                }

                if (!CanTargetWithPolicy(context, health))
                {
                    continue;
                }

                areaTargetsBuffer.Add(health);
            }

            return areaTargetsBuffer;
        }

        #endregion

        private SkillTargetingEvaluator GetTargetingEvaluator()
        {
            if (targetingEvaluator != null)
            {
                return targetingEvaluator;
            }

            targetingEvaluator = new SkillTargetingEvaluator(new SkillTargetLayerMaskResolver(skillTargetLayerMap));
            return targetingEvaluator;
        }

        private bool CanTargetWithPolicy(in SkillExecutionContext context, Health target)
        {
            if (target.IsNull())
            {
                return false;
            }

            if (context.Attacker.IsNull() || context.Skill.IsNull())
            {
                return !target.IsDead;
            }

            return GetTargetingEvaluator().CanTarget(context, target);
        }

        private int ResolveTargetLayerMask(in SkillExecutionContext context)
        {
            if (context.Attacker.IsNull() || context.Skill.IsNull())
            {
                return 0;
            }

            return GetTargetingEvaluator().ResolveTargetLayerMask(context);
        }

        private bool TryApplyHitWithSource(in AttackSource attackSource, Health target)
        {
            combatSystem ??= ServiceLocator.Get<ICombatSystem>();
            if (combatSystem == null)
            {
                return false;
            }

            combatSystem.ApplyHit(attackSource.ToRequest(target));
            return true;
        }

        private void EnsureOverlapBufferSize(int requiredSize)
        {
            if (requiredSize <= overlapBuffer.Length)
            {
                return;
            }

            int resized = Mathf.NextPowerOfTwo(requiredSize);
            overlapBuffer = new Collider[Mathf.Max(32, resized)];
        }

        private AttackSource BuildModifiedAttackSource(
            in AttackSource source,
            float damageScale,
            int hitCountOverride,
            bool allowReusableList)
        {
            float resolvedDamageScale = Mathf.Max(0f, damageScale);
            int resolvedHitCount = Mathf.Max(0, hitCountOverride);
            float sourceBaseDamage = source.AttackSourceStat?.Value ?? source.BaseDamage;

            if (source.HitDamages != null && source.HitDamages.Count > 0)
            {
                bool keepSourceHitDamages = Mathf.Approximately(resolvedDamageScale, 1f) &&
                                            (resolvedHitCount <= 0 || resolvedHitCount == source.HitDamages.Count);
                if (keepSourceHitDamages)
                    return source;

                int targetCount = resolvedHitCount > 0 ? resolvedHitCount : source.HitDamages.Count;
                var scaledHitDamages = allowReusableList ? reusableModifiedHitDamages : new List<float>(targetCount);
                if (allowReusableList)
                {
                    scaledHitDamages.Clear();
                    if (scaledHitDamages.Capacity < targetCount)
                        scaledHitDamages.Capacity = targetCount;
                }

                for (int i = 0; i < source.HitDamages.Count; i++)
                {
                    scaledHitDamages.Add(source.HitDamages[i] * resolvedDamageScale);
                }

                if (resolvedHitCount > 0 && resolvedHitCount != scaledHitDamages.Count)
                {
                    float perHitDamage = scaledHitDamages.Count > 0
                        ? scaledHitDamages[0]
                        : sourceBaseDamage * resolvedDamageScale;
                    scaledHitDamages.Clear();
                    for (int i = 0; i < resolvedHitCount; i++)
                    {
                        scaledHitDamages.Add(perHitDamage);
                    }
                }

                float firstDamage = scaledHitDamages.Count > 0
                    ? scaledHitDamages[0]
                    : sourceBaseDamage * resolvedDamageScale;
                return new AttackSource(source.Attacker, null, firstDamage, source.DamageType, source.AttackInstanceId,
                    scaledHitDamages, source.Skill);
            }

            if (resolvedHitCount > 1)
            {
                float perHitDamage = sourceBaseDamage * resolvedDamageScale;
                var hitDamages = allowReusableList ? reusableModifiedHitDamages : new List<float>(resolvedHitCount);
                if (allowReusableList)
                {
                    hitDamages.Clear();
                    if (hitDamages.Capacity < resolvedHitCount)
                        hitDamages.Capacity = resolvedHitCount;
                }

                for (int i = 0; i < resolvedHitCount; i++)
                {
                    hitDamages.Add(perHitDamage);
                }

                return new AttackSource(source.Attacker, null, perHitDamage, source.DamageType, source.AttackInstanceId,
                    hitDamages, source.Skill);
            }

            float scaledBaseDamage = sourceBaseDamage * resolvedDamageScale;
            return new AttackSource(source.Attacker, null, scaledBaseDamage, source.DamageType, source.AttackInstanceId,
                null, source.Skill);
        }

        private void SetPendingAttack(IAttacker attacker, SkillTypeSO skill, in AttackSource attackSource, SkillTypeSO baseSkill, int stepIndex, int stepCount)
        {
            pendingAttacker = attacker;
            pendingAttackSkill = skill;
            pendingAttackSource = attackSource;
            pendingBaseSkill = baseSkill;
            pendingComboStepIndex = Mathf.Max(0, stepIndex);
            pendingComboStepCount = Mathf.Max(1, stepCount);
            hasPendingAttack = true;
        }

        private void ClearPendingAttack()
        {
            hasPendingAttack = false;
            pendingAttackSource = default;
            pendingAttackSkill = null;
            pendingAttacker = null;
            pendingBaseSkill = null;
            pendingComboStepIndex = 0;
            pendingComboStepCount = 1;
        }

        private bool TryReusePendingAttack(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;

            if (!TryGetValidPendingState(out var baseSkill, out var stepIndex, out var stepCount))
            {
                return false;
            }

            if (attacker.IsNull() || !ReferenceEquals(pendingAttacker, attacker))
            {
                return false;
            }

            attackSource = pendingAttackSource;
            SetResolvedSkill(pendingAttackSkill, baseSkill, stepIndex, stepCount, forceNotify: true);
            return true;
        }

        private bool TryGetValidPendingState(out SkillTypeSO baseSkill, out int stepIndex, out int stepCount)
        {
            baseSkill = null;
            stepIndex = 0;
            stepCount = 1;

            if (!hasPendingAttack || pendingAttackSkill.IsNull())
            {
                return false;
            }

            baseSkill = pendingBaseSkill;
            stepIndex = Mathf.Max(0, pendingComboStepIndex);
            stepCount = Mathf.Max(1, pendingComboStepCount);

            return IsPendingAttackReusableStateFor(baseSkill, stepIndex, stepCount);
        }

        private bool IsPendingAttackReusableState()
        {
            return IsPendingAttackReusableStateFor(pendingBaseSkill, pendingComboStepIndex, pendingComboStepCount);
        }

        private bool IsPendingAttackReusableStateFor(SkillTypeSO baseSkill, int stepIndex, int stepCount)
        {
            if (!hasPendingAttack || pendingAttackSkill.IsNull() || pendingAttackSource.Skill.IsNull())
            {
                return false;
            }

            if (pendingAttackSource.Skill != pendingAttackSkill)
            {
                return false;
            }

            if (baseSkill.IsNull() || !HasActiveSkill || skillBook.ActiveSkill != baseSkill)
            {
                return false;
            }

            if (baseSkill.ComboSequence is not { HasSteps: true } comboSequence)
            {
                return stepIndex == 0 && stepCount <= 1 && pendingAttackSkill == baseSkill;
            }

            int resolvedStepCount = Mathf.Max(1, comboSequence.StepCount);
            int resolvedStepIndex = Mathf.Clamp(stepIndex, 0, resolvedStepCount - 1);
            if (resolvedStepCount != Mathf.Max(1, stepCount))
            {
                return false;
            }

            var context = GetOrCreateComboContext(baseSkill);
            float comboTimeout = Mathf.Max(0f, comboSequence.ComboTimeout);
            if (comboTimeout > 0f &&
                context.LastConsumeTime >= 0f &&
                Time.time > context.LastConsumeTime + comboTimeout)
            {
                return false;
            }

            return comboSequence.GetStepSkill(resolvedStepIndex, baseSkill) == pendingAttackSkill;
        }

        private void CancelPendingAttack(PendingCancelReason reason, bool refreshResolvedFromPreview)
        {
            _ = reason;

            if (!hasPendingAttack)
            {
                return;
            }

            ClearPendingAttack();

            if (refreshResolvedFromPreview)
            {
                UpdateResolvedSkillFromPreview(forceNotify: true);
            }
        }





        private bool TryBuildAttackSource(IAttacker attacker, SkillTypeSO skill, int attackInstanceId, out AttackSource attackSource)
        {
            attackSource = default;

            if (skill.IsNull() || attacker.IsNull()) return false;

            bool hasAttackSourceStat = TryResolveAttackSourceStat(skill, out var attackSourceStat);
            float sourceDamage = hasAttackSourceStat ? attackSourceStat.Value : skill.BaseDamage;
            float perHitDamage = Mathf.Max(0f, sourceDamage * skill.AttackCoefficient);
            int hitCount = Mathf.Max(1, skill.HitCount);

            // 단일 히트 + 계수 1인 경우에는 스탯 참조형 AttackSource로 전달해 후처리 확장성을 유지한다.
            if (hitCount <= 1)
            {
                if (hasAttackSourceStat && Mathf.Approximately(skill.AttackCoefficient, 1f))
                {
                    attackSource = new AttackSource(
                        attacker,
                        attackSourceStat,
                        0f,
                        skill.DamageType,
                        attackInstanceId,
                        null,
                        skill);
                    return true;
                }

                attackSource = new AttackSource(
                    attacker,
                    null,
                    perHitDamage,
                    skill.DamageType,
                    attackInstanceId,
                    null,
                    skill);
                return true;
            }

            // 다단 히트는 히트별 데미지 배열을 함께 전달한다.
            var hitDamages = BuildHitDamages(perHitDamage, hitCount);
            attackSource = new AttackSource(attacker, null, perHitDamage, skill.DamageType, attackInstanceId, hitDamages, skill);
            return true;
        }

        // 멀티스레드 환경에서도 중복되지 않는 공격 인스턴스 ID를 발급한다.
        private static int TakeNextAttackInstanceId()
        {
            int next = Interlocked.Increment(ref attackSequence);
            if (next > 0)
                return next;

            Interlocked.CompareExchange(ref attackSequence, 1, next);
            return 1;
        }

        // 스킬이 참조하는 공격 스탯을 실제 스탯 홀더에서 조회한다.
        private bool TryResolveAttackSourceStat(SkillTypeSO skill, out IGameStat attackSourceStat)
        {
            attackSourceStat = null;
            if (skill.IsNull()) return false;

            if (statHolder.IsNotNull() &&
                skill.AttackSourceStatSO.IsNotNull() &&
                statHolder.TryGetStat(skill.AttackSourceStatSO, out var resolvedAttackSourceStat))
            {
                attackSourceStat = resolvedAttackSourceStat;
                return true;
            }

            return false;
        }

        // 다단 히트용 데미지 리스트를 생성한다.
        private List<float> BuildHitDamages(float perHitDamage, int hitCount)
        {
            int resolvedHitCount = Mathf.Max(1, hitCount);
            var hitDamages = hasPendingAttack && ReferenceEquals(pendingAttackSource.HitDamages, primaryPendingHitDamages)
                ? secondaryPendingHitDamages
                : primaryPendingHitDamages;

            hitDamages.Clear();
            if (hitDamages.Capacity < resolvedHitCount)
                hitDamages.Capacity = resolvedHitCount;

            for (int i = 0; i < resolvedHitCount; i++)
            {
                hitDamages.Add(perHitDamage);
            }

            return hitDamages;
        }

        // 등록 스킬 목록과 활성 스킬만 보관하는 간단한 컬렉션 래퍼.
        private enum PendingCancelReason
        {
            InvalidatedOnConsume,
            InvalidatedByStateChange,
            InvalidatedOnExecute,
            ExecutionRejected
        }

        private sealed class SkillBook
        {
            // 등록된 전체 스킬 목록.
            private readonly List<SkillTypeSO> skills = new();

            // 외부 읽기 전용 스킬 목록.
            public IReadOnlyList<SkillTypeSO> Skills => skills;
            // 현재 활성 스킬.
            public SkillTypeSO ActiveSkill { get; private set; }

            // 스킬을 등록하고 비어 있으면 활성 스킬로도 설정한다.
            public bool Register(SkillTypeSO skill)
            {
                if (skill.IsNull() || Contains(skill)) return false;

                skills.Add(skill);
                if (ActiveSkill.IsNull())
                {
                    ActiveSkill = skill;
                }

                return true;
            }

            // 스킬 등록 여부 확인
            public bool Contains(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                return skills.Contains(skill);
            }

            // 현재 활성화된 스킬을 변경
            public bool SetActive(SkillTypeSO skill)
            {
                if (!Contains(skill)) return false;
                if (ActiveSkill == skill) return false;

                ActiveSkill = skill;
                return true;
            }

            // 첫 번째 유효 스킬을 반환
            public bool TryGetFirst(out SkillTypeSO firstSkill)
            {
                if (skills.Count > 0 && skills[0].IsNotNull())
                {
                    firstSkill = skills[0];
                    return true;
                }

                firstSkill = null;
                return false;
            }
        }

        // 스킬 쿨다운/준비 상태를 추적하는 런타임 캐시용 클래스
        [Serializable]
        private sealed class SkillCaster
        {
            // 스킬별 다음 사용 가능 시각 (쿨타임)
            private readonly Dictionary<SkillTypeSO, float> nextReadyAt = new();
            // 직전 프레임 기준 준비 상태 캐시
            private readonly Dictionary<SkillTypeSO, bool> cachedReadyState = new();

            // 스킬 추적 등록(초기 상태는 즉시 준비 완료)
            public void TrackSkill(SkillTypeSO skill)
            {
                if (skill.IsNull()) return;

                if (!nextReadyAt.ContainsKey(skill))
                {
                    nextReadyAt[skill] = 0f;
                }

                cachedReadyState[skill] = true;
            }

            // 스킬이 현재 시점에 사용 가능한지 확인
            public bool IsReady(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                if (!nextReadyAt.TryGetValue(skill, out var readyTime)) return true;

                return Time.time >= readyTime;
            }

            // 스킬 사용을 소비하고 다음 사용 가능 시각을 갱신
            public bool Consume(SkillTypeSO skill, float cooldown)
            {
                if (skill.IsNull() || !IsReady(skill)) return false;

                float appliedCooldown = Mathf.Max(0f, cooldown);
                nextReadyAt[skill] = appliedCooldown > 0f ? Time.time + appliedCooldown : Time.time;
                cachedReadyState[skill] = appliedCooldown <= 0f;
                return true;
            }

            // 남은 쿨다운을 반환
            public float GetRemainingCooldown(SkillTypeSO skill)
            {
                if (skill.IsNull()) return 0f;
                if (!nextReadyAt.TryGetValue(skill, out var readyTime)) return 0f;

                return Mathf.Max(0f, readyTime - Time.time);
            }

            // 준비 상태 변화를 감지해 Ready 콜백을 발행
            public void PollReady(IReadOnlyList<SkillTypeSO> skills, Action<SkillTypeSO> onReady)
            {
                if (skills == null) return;

                for (int i = 0; i < skills.Count; i++)
                {
                    var skill = skills[i];
                    if (skill.IsNull()) continue;

                    bool wasReady = cachedReadyState.TryGetValue(skill, out var prev) && prev;
                    bool isReady = IsReady(skill);
                    cachedReadyState[skill] = isReady;

                    if (!wasReady && isReady)
                    {
                        onReady?.Invoke(skill);
                    }
                }
            }
        }

        // 베이스 스킬의 콤보 진행 인덱스/마지막 소비 시각을 저장
        [Serializable]
        private sealed class ComboContext
        {
            // 다음에 사용할 콤보 스텝 인덱스
            public int NextStepIndex;
            // 마지막 콤보 소비 시각
            public float LastConsumeTime = -1f;
        }

#region For Debug (Editor Only)
#if UNITY_EDITOR
        [Header("Debug")]// 인스펙터 디버그용
        // 현재 활성 스킬
        [SerializeField] private SkillTypeSO activeSkillDebug;
        // 현재 해석 스킬
        [SerializeField] private SkillTypeSO resolvedSkillDebug;
        // 현재 콤보 인덱스
        [SerializeField] private int comboStepIndexDebug;
        // 현재 콤보 스텝 수
        [SerializeField] private int comboStepCountDebug;
        // 활성 스킬 남은 쿨다운
        [SerializeField] private float activeSkillRemainCooldownDebug;
#endif

        // 에디터 전용 디버깅 관련 필드 동기화 매서드
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void SyncDebugValues()
        {
#if UNITY_EDITOR
            activeSkillDebug = ActiveSkill;
            resolvedSkillDebug = ResolvedSkill;
            comboStepIndexDebug = CurrentComboStepIndex;
            comboStepCountDebug = CurrentComboStepCount;
            activeSkillRemainCooldownDebug = HasActiveSkill
                ? skillCaster.GetRemainingCooldown(skillBook.ActiveSkill)
                : 0f;
#endif
        }
#endregion
    }
}
