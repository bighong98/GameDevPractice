using System;
using System.Collections.Generic;
using UnityEngine;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat.Service;
using TH.Item;
using TH.Resource;

namespace TH.Combat
{
    // 스킬 상태/콤보 상태/보류 공격 상태 통합 제어 구현체
    public sealed partial class SkillController : MonoBehaviour, ISkillController, ISkillExecutionServices
    {
        // 빈 등록 목록 폴백 배열
        private static readonly SkillTypeSO[] EmptySkills = Array.Empty<SkillTypeSO>();
        // 전역 공격 인스턴스 시퀀스
        private static int attackSequence;

        [Header("Initial Skills")]
        // 시작 시 자동 등록 스킬 목록
        [SerializeField] private List<SkillTypeSO> initialSkills = new();
        // 시작 시 기본 활성 스킬
        [SerializeField] private SkillTypeSO defaultActiveSkill;
        // 장착 무기 기본 스킬 동기화 옵션
        [SerializeField] private bool syncWithEquippedWeapon = true;

        [Header("Targeting")]
        // 스킬 대상 레이어 매핑 자원
        [SerializeField] private SkillTargetLayerMapSO skillTargetLayerMap;

        // [Header("UI Slots")]

        // 공격 원천 스탯 조회 홀더
        private IStatHolder statHolder;
        // 장비 상태 이벤트 소스
        private EquipmentHolder equipHolder;
        // 어드레서블 로더 서비스 캐시
        private IResourceLoader resourceLoader;
        // 직접 타격 전투 서비스 캐시
        private ICombatSystem combatSystem;
        // 투사체 실행기 주입 참조
        private ISkillProjectileExecutor projectileExecutor;
        // 타게팅 정책 평가기 캐시
        private SkillTargetingEvaluator targetingEvaluator;
        // 슬롯 정렬 카테고리 프로필 캐시
        private SkillCategorySortProfileSO categorySortProfile;

        // 등록/활성 스킬 저장소
        private SkillBook skillBook;
        // 현재 장착 무기 기본 스킬 캐시
        private SkillTypeSO equippedWeaponDefaultSkill;
        // 쿨다운 준비 상태 추적기
        private SkillCaster skillCaster;

        // 스킬별 콤보 진행 컨텍스트 맵
        private readonly Dictionary<SkillTypeSO, ComboContext> comboContexts = new();
        // 슬롯 정렬 결과 캐시
        private readonly List<SkillTypeSO> orderedAvailableSkills = new();
        // 슬롯 정렬 이전 스냅샷 캐시
        private readonly List<SkillTypeSO> orderedAvailableSkillsSnapshot = new();
        // 등록 순서 캐시
        private readonly Dictionary<SkillTypeSO, int> registeredSkillOrder = new();
        // 중복 제거 버퍼
        private readonly HashSet<SkillTypeSO> uniqueSkillBuffer = new();
        // 카테고리 우선순위 맵 캐시
        private readonly Dictionary<SkillCategory, int> categoryPriorityMap = new();

        // 공격 소스 변형 재사용 버퍼
        private readonly List<float> reusableModifiedHitDamages = new(8);
        // 보류 공격 1차 히트 데미지 버퍼
        private readonly List<float> primaryPendingHitDamages = new(8);
        // 보류 공격 2차 히트 데미지 버퍼
        private readonly List<float> secondaryPendingHitDamages = new(8);

        // 활성 콤보 타임아웃 예약 상태
        private bool isActiveComboTimeoutPending;
        // 활성 콤보 타임아웃 대상 베이스 스킬
        private SkillTypeSO activeComboTimeoutBaseSkill;
        // 타임아웃 시 기대 단계 인덱스
        private int activeComboTimeoutExpectedNextStepIndex;
        // 타임아웃 만료 시각
        private float activeComboTimeoutAt;

        // 현재 실행 고정 스킬
        private SkillTypeSO executingSkill;
        // 현재 해석 완료 스킬
        private SkillTypeSO resolvedSkill;
        // 현재 해석 완료 베이스 스킬
        private SkillTypeSO resolvedBaseSkill;
        // 현재 콤보 단계 인덱스
        private int currentComboStepIndex;
        // 현재 콤보 총 단계 수
        private int currentComboStepCount = 1;

        // 보류 공격 존재 상태
        private bool hasPendingAttack;
        // 보류 공격 소스
        private AttackSource pendingAttackSource;
        // 보류 공격 스킬
        private SkillTypeSO pendingAttackSkill;
        // 보류 공격 베이스 스킬
        private SkillTypeSO pendingBaseSkill;
        // 보류 공격 단계 인덱스
        private int pendingComboStepIndex;
        // 보류 공격 총 단계 수
        private int pendingComboStepCount = 1;
        // 보류 공격 생성 주체
        private IAttacker pendingAttacker;

        // 광역 대상 수집 버퍼
        private readonly List<Health> areaTargetsBuffer = new();
        // OverlapSphere 결과 버퍼
        private Collider[] overlapBuffer = new Collider[32];

        // 활성 스킬 변경 알림 이벤트
        public event Action<SkillTypeSO> OnActiveSkillChanged;
        // 해석 스킬 변경 알림 이벤트
        public event Action<SkillTypeSO> OnResolvedSkillChanged;
        // 스킬 소비 완료 알림 이벤트
        public event Action<SkillTypeSO> OnSkillConsumed;
        // 스킬 준비 완료 알림 이벤트
        public event Action<SkillTypeSO> OnSkillReady;
        // 스킬북 변경 알림 이벤트
        public event Action OnSkillBookChanged;
        // 사용 가능 스킬 목록 변경 알림 이벤트
        public event Action OnAvailableSkillsChanged;
        // 슬롯별 스킬 변경 알림 이벤트
        public event Action<int, SkillTypeSO> OnSkillSlotChanged;
        // 콤보 단계 변경 알림 이벤트
        public event Action<SkillTypeSO, int, int> OnComboStepChanged;
        // 슬롯 하이라이트 요청 알림 이벤트
        public event Action<SkillTypeSO> OnSkillSlotHighlightRequested;

        // 활성 스킬 보유 상태
        public bool HasActiveSkill => skillBook != null && skillBook.ActiveSkill.IsNotNull();
        // 현재 활성 스킬 참조
        public SkillTypeSO ActiveSkill => skillBook?.ActiveSkill;
        // 해석 완료 스킬 보유 상태
        public bool HasResolvedSkill => resolvedSkill.IsNotNull();
        // 실행 고정 스킬 보유 상태
        public bool HasExecutingSkill => executingSkill.IsNotNull();
        // 현재 실행 고정 스킬 참조
        public SkillTypeSO ExecutingSkill => executingSkill;
        // 현재 해석 완료 스킬 참조
        public SkillTypeSO ResolvedSkill => resolvedSkill;
        // 활성 스킬 즉시 사용 가능 상태
        public bool IsActiveSkillReady => HasActiveSkill && skillCaster.IsReady(skillBook.ActiveSkill);

        // 현재 프리뷰 스킬 기준 사거리
        public float ActiveSkillRange
        {
            get
            {
                var previewSkill = GetPreviewSkill();
                return previewSkill.IsNotNull() ? Mathf.Max(0f, previewSkill.Range) : 0f;
            }
        }

        // 현재 프리뷰 스킬 기준 캐스팅 SFX
        public AudioClip ActiveSkillSFX
        {
            get
            {
                var previewSkill = GetPreviewSkill();
                return previewSkill.IsNotNull() ? previewSkill.CastSFX : null;
            }
        }

        // 해석 완료 스킬 기준 캐스팅 SFX
        public AudioClip ResolvedSkillSFX => HasResolvedSkill ? resolvedSkill.CastSFX : null;
        // 현재 콤보 단계 인덱스
        public int CurrentComboStepIndex => currentComboStepIndex;
        // 현재 콤보 총 단계 수
        public int CurrentComboStepCount => currentComboStepCount;
        // 등록 스킬 읽기 전용 목록
        public IReadOnlyList<SkillTypeSO> RegisteredSkills => skillBook?.Skills ?? EmptySkills;
        // 사용 가능 스킬 읽기 전용 목록
        public IReadOnlyList<SkillTypeSO> AvailableSkills => skillBook?.AvailableSkills ?? EmptySkills;
        // 슬롯 정렬 반영 사용 가능 스킬 읽기 전용 목록
        public IReadOnlyList<SkillTypeSO> OrderedAvailableSkills => orderedAvailableSkills;
        // 타게팅 레이어 맵 외부 노출 참조
        public SkillTargetLayerMapSO SkillTargetLayerMap => skillTargetLayerMap;

        // 컴포넌트 참조 캐시 + 초기 스킬 구성 진입점
        private void Awake()
        {
            // 필수 참조 캐시
            TryGetComponent(out statHolder);
            TryGetComponent(out equipHolder);

            // 내부 상태 저장소 생성
            skillBook = new SkillBook();
            skillCaster = new SkillCaster();
            targetingEvaluator = new SkillTargetingEvaluator(new SkillTargetLayerMaskResolver(skillTargetLayerMap));

            // 초기 스킬 자동 등록
            for (int i = 0; i < initialSkills.Count; i++)
            {
                RegisterSkill(initialSkills[i]);
            }

            // 기본 활성 스킬 우선 적용
            if (defaultActiveSkill.IsNotNull())
            {
                SetActiveSkill(defaultActiveSkill);
            }
            // 기본값 미설정 시 첫 등록 스킬 활성화
            else if (!HasActiveSkill && skillBook.TryGetFirst(out var firstSkill))
            {
                SetActiveSkill(firstSkill);
            }

            // 초기 프리뷰 해석 결과 동기화
            UpdateResolvedSkillFromPreview(forceNotify: HasActiveSkill);
            SyncDebugValues();
        }

        // 시작 시 무기 장착 상태 기반 기본 스킬 반영
        private void Start()
        {
            if (syncWithEquippedWeapon && equipHolder is { IsEquippingWeapon: true, GetEquippedWeaponInfo: { } weapon })
            {
                HandleEquipWeapon(weapon);
            }
        }

        // 장착 무기 변경 이벤트 구독 구간
        private void OnEnable()
        {
            if (syncWithEquippedWeapon && equipHolder.IsNotNull())
            {
                equipHolder.OnEquipWeapon += HandleEquipWeapon;
            }

            if (resourceLoader != null)
            {
                resourceLoader.OnLabelResourcesLoadedAll += HandleResourceLabelLoaded;
            }
        }

        // 장착 무기 변경 이벤트 해제 + 타임아웃 예약 정리 구간
        private void OnDisable()
        {
            if (syncWithEquippedWeapon && equipHolder.IsNotNull())
            {
                equipHolder.OnEquipWeapon -= HandleEquipWeapon;
            }

            if (resourceLoader != null)
            {
                resourceLoader.OnLabelResourcesLoadedAll -= HandleResourceLabelLoaded;
            }

            CancelActiveComboTimeoutRoutine();
        }

        // 매 프레임 상태 무결성 유지 + 준비 완료 이벤트 폴링
        private void Update()
        {
            // 초기화 이전 프레임 가드
            if (skillBook == null || skillCaster == null) return;

            // 보류 공격 상태 불일치 즉시 취소
            if (hasPendingAttack && !IsPendingAttackReusableState())
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedByStateChange, refreshResolvedFromPreview: true);
            }

            // 활성 콤보 타임아웃 만료 처리
            if (isActiveComboTimeoutPending && Time.time >= activeComboTimeoutAt)
            {
                isActiveComboTimeoutPending = false;

                // 타임아웃 예약 시점과 동일 베이스 스킬 검증
                if (HasActiveSkill && skillBook.ActiveSkill == activeComboTimeoutBaseSkill)
                {
                    var context = GetOrCreateComboContext(activeComboTimeoutBaseSkill);
                    // 예약 당시 기대 단계와 현재 단계 일치 시 프리뷰 갱신
                    if (context.NextStepIndex == activeComboTimeoutExpectedNextStepIndex)
                    {
                        UpdateResolvedSkillFromPreview(forceNotify: true);
                    }
                }
            }

            // 쿨다운 준비 완료 전이 감지 + 이벤트 발행
            skillCaster.PollReady(skillBook.Skills, skill =>
            {
                OnSkillReady?.Invoke(skill);
            });

            SyncDebugValues();
        }

        // 장착 무기 기본 스킬 등록 + 활성화 처리
        private void HandleEquipWeapon(WeaponTypeSO weapon)
        {
            if (weapon.IsNull() || weapon.DefaultSkill.IsNull()) return;

            equippedWeaponDefaultSkill = weapon.DefaultSkill;
            bool added = RegisterSkill(weapon.DefaultSkill, setActive: true);
            if (!added)
            {
                SyncAvailableSkillSet(forceNotify: true);
            }
        }
    }
}
