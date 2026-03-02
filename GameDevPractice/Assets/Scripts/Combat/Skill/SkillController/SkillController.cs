using System;
using System.Collections.Generic;
using UnityEngine;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat.Service;
using TH.Core.Service;
using TH.Item;
using TH.Resource;

namespace TH.Combat
{
    // 전투 스킬 상태 통합 제어 컴포넌트
    public sealed partial class SkillController : MonoBehaviour, ISkillController, ISkillExecutionServices
    {
        #region static fields
        // 빈 스킬 목록 반환용 정적 캐시
        private static readonly SkillTypeSO[] EmptySkills = Array.Empty<SkillTypeSO>();
        // 전역 공격 시퀀스 번호
        private static int attackSequence;

        #endregion

        #region Serialized Fields

        [Header("Initial Skills")]
        // 시작 시 자동 등록 대상 스킬 목록
        [SerializeField] private List<SkillTypeSO> initialSkills = new();
        // 시작 시 우선 활성 스킬
        [SerializeField] private SkillTypeSO defaultActiveSkill;
        // 장착 무기 스킬 자동 동기화 옵션
        [SerializeField] private bool syncWithEquippedWeapon = true;

        [Header("Targeting")]
        // 스킬 대상 레이어 매핑 자원
        [SerializeField] private SkillTargetLayerMapSO skillTargetLayerMap;

        #endregion

        #region Character Components 

        private IStatHolder statHolder; // 캐릭터 능력치 참조
        private EquipmentHolder equipHolder; // 캐릭터 장착 장비 목록 참조
        
        #endregion

        #region Outer Services
        
        private IResourceLoader resourceLoader; // 리소스 로더 참조 (SO 에셋 로드 등에 사용)
        private ICombatSystem combatSystem; // 전투 시스템 참조 (대미지 처리)
        
        #endregion

        #region Internal Modules & Data (ScriptableObject)
        private ISkillProjectileExecutor projectileExecutor; // 투사체 발사 (+오브젝트 풀링 적용)
        private SkillTargetingEvaluator targetingEvaluator; // 스킬 타겟팅 레이어 구분 
        private SkillCategorySortProfileSO categorySortProfile; // 스킬 슬롯 정렬 프로필 데이터 SO

        private SkillBook skillBook; // 등록 스킬 캐시
        private SkillCaster skillCaster; // 스킬 사용 및 쿨다운 관리

        #endregion

        // 현재 장착 무기 스킬 목록
        private readonly List<SkillTypeSO> equippedWeaponSkills = new();

        // 스킬별 콤보 진행 컨텍스트 맵
        private readonly Dictionary<SkillTypeSO, ComboContext> comboContexts = new();
        // 제공자 우선순위 반영 최종 사용 가능 스킬 목록
        private readonly List<SkillTypeSO> effectiveAvailableSkills = new();
        // 최종 사용 가능 스킬 중복 판별 집합
        private readonly HashSet<SkillTypeSO> effectiveAvailableSkillSet = new();
        // 카테고리별 대표 스킬 선정 결과 맵
        private readonly Dictionary<SkillCategory, SkillTypeSO> categoryWinnerByType = new();
        // 카테고리 대표 스킬 원본 인덱스 캐시
        private readonly Dictionary<SkillCategory, int> categoryWinnerRawIndexByType = new();
        // 카테고리 대표 스킬 제공자 우선순위 캐시
        private readonly Dictionary<SkillCategory, int> categoryWinnerProviderPriorityByType = new();
        // 제공자 식별자별 우선순위 맵
        private readonly Dictionary<string, int> providerPriorityMap = new(StringComparer.Ordinal);
        // 정렬 전 사용 가능 스킬 목록
        private readonly List<SkillTypeSO> orderedAvailableSkills = new();
        // 이전 프레임 정렬 결과 스냅샷
        private readonly List<SkillTypeSO> orderedAvailableSkillsSnapshot = new();
        // 등록 순서 캐시
        private readonly Dictionary<SkillTypeSO, int> registeredSkillOrder = new();
        // 중복 제거 버퍼
        private readonly HashSet<SkillTypeSO> uniqueSkillBuffer = new();
        // 카테고리 우선순위 캐시
        private readonly Dictionary<SkillCategory, int> categoryPriorityMap = new();

        // 공격 히트 변조 데미지 임시 버퍼
        private readonly List<float> reusableModifiedHitDamages = new(8);
        // 보류 공격 1차 히트 데미지 버퍼
        private readonly List<float> primaryPendingHitDamages = new(8);

        // 활성 콤보 타임아웃 예약 상태
        private bool isActiveComboTimeoutPending;
        // 타임아웃 검증 대상 베이스 스킬
        private SkillTypeSO activeComboTimeoutBaseSkill;
        // 타임아웃 기대 다음 단계 인덱스
        private int activeComboTimeoutExpectedNextStepIndex;
        // 타임아웃 만료 시각
        private float activeComboTimeoutAt;

        // 현재 실행 고정 스킬
        private SkillTypeSO executingSkill;
        // 마지막 사용 스킬
        private SkillTypeSO lastUsedSkill;
        // 현재 해석 완료 스킬
        private SkillTypeSO resolvedSkill;
        // 현재 해석 완료 베이스 스킬
        private SkillTypeSO resolvedBaseSkill;
        // 현재 콤보 단계 인덱스
        private int currentComboStepIndex;
        // 현재 콤보 총 단계 수
        private int currentComboStepCount = 1;

        // 자동 재활성화 시 콤보 진행 유지 대상
        private SkillTypeSO preservedComboProgressSkill;
        // 외부 활성 해제 명령 무시 마커 대상
        private SkillTypeSO externalClearIgnoreComboMarkerSkill;

        // 보류 공격 존재 상태
        private bool hasPendingAttack;
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
        // 보류 공격 발행 프레임 번호
        private int pendingAttackIssuedFrame = -1;

        // 광역 대상 수집 버퍼
        private readonly List<Health> areaTargetsBuffer = new();
        // OverlapSphere 결과 버퍼
        private Collider[] overlapBuffer = new Collider[32];

        // 활성 스킬 변경 알림
        public event Action<SkillTypeSO> OnActiveSkillChanged;
        // 해석 스킬 변경 알림
        public event Action<SkillTypeSO> OnResolvedSkillChanged;
        // 스킬 소비 알림
        public event Action<SkillTypeSO> OnSkillConsumed;
        // 스킬 준비 완료 알림
        public event Action<SkillTypeSO> OnSkillReady;
        // 스킬북 변경 알림
        public event Action OnSkillBookChanged;
        // 사용 가능 스킬 목록 변경 알림
        public event Action OnAvailableSkillsChanged;
        // 슬롯 스킬 변경 알림
        public event Action<int, SkillTypeSO> OnSkillSlotChanged;
        // 콤보 단계 변경 알림
        public event Action<SkillTypeSO, int, int> OnComboStepChanged;
        // 슬롯 하이라이트 요청 알림
        public event Action<SkillTypeSO> OnSkillSlotHighlightRequested;

        // 활성 스킬 보유 여부
        public bool HasActiveSkill => skillBook != null && skillBook.ActiveSkill.IsNotNull();
        // 현재 활성 스킬 참조
        public SkillTypeSO ActiveSkill => skillBook?.ActiveSkill;
        // 해석 완료 스킬 보유 여부
        public bool HasResolvedSkill => resolvedSkill.IsNotNull();
        // 실행 고정 스킬 보유 여부
        public bool HasExecutingSkill => executingSkill.IsNotNull();
        // 현재 실행 고정 스킬 참조
        public SkillTypeSO ExecutingSkill => executingSkill;
        // 마지막 사용 스킬 보유 여부
        public bool HasLastUsedSkill => lastUsedSkill.IsNotNull();
        // 마지막 사용 스킬 참조
        public SkillTypeSO LastUsedSkill => lastUsedSkill;
        // 해석 완료 스킬 참조
        public SkillTypeSO ResolvedSkill => resolvedSkill;
        // 활성 스킬 즉시 사용 가능 여부
        public bool IsActiveSkillReady => HasActiveSkill && !hasPendingAttack && skillCaster.IsReady(skillBook.ActiveSkill);
        // 보류 공격 존재 여부
        public bool HasPendingAttack => hasPendingAttack;

        // 현재 프리뷰 스킬 기준 사거리
        public float ActiveSkillRange
        {
            get
            {
                // 프리뷰 기준 스킬 조회
                var previewSkill = GetPreviewSkill();
                // 음수 방지 사거리 반환
                return previewSkill.IsNotNull() ? Mathf.Max(0f, previewSkill.Range) : 0f;
            }
        }

        // 현재 콤보 단계 인덱스
        public int CurrentComboStepIndex => currentComboStepIndex;
        // 현재 콤보 총 단계 수
        public int CurrentComboStepCount => currentComboStepCount;
        // 등록 스킬 읽기 전용 목록
        public IReadOnlyList<SkillTypeSO> RegisteredSkills => skillBook?.Skills ?? EmptySkills;
        // 사용 가능 스킬 읽기 전용 목록
        public IReadOnlyList<SkillTypeSO> AvailableSkills => effectiveAvailableSkills;
        // 정렬 반영 사용 가능 스킬 읽기 전용 목록
        public IReadOnlyList<SkillTypeSO> OrderedAvailableSkills => orderedAvailableSkills;
        // 대상 레이어 맵 외부 노출 참조
        public SkillTargetLayerMapSO SkillTargetLayerMap => skillTargetLayerMap;

        // 컴포넌트 참조 캐시 및 초기 스킬 구성 진입점
        private void Awake()
        {
            // 필수 컴포넌트 참조 캐시
            TryGetComponent(out statHolder);
            TryGetComponent(out equipHolder);
            // 서비스 로더 참조 캐시
            resourceLoader = ServiceLocator.Get<IResourceLoader>();

            // 내부 상태 컨테이너 초기화
            skillBook = new SkillBook();
            skillCaster = new SkillCaster();
            targetingEvaluator = new SkillTargetingEvaluator(new SkillTargetLayerMaskResolver(skillTargetLayerMap));
            InitializeProviderPriorityMap();

            // 초기 스킬 자동 등록 루프
            for (int i = 0; i < initialSkills.Count; i++)
            {
                // 인덱스 기반 등록 요청
                RegisterSkill(initialSkills[i]);
            }


            // 프리뷰 기반 해석 결과 초기 동기화
            UpdateResolvedSkillFromPreview(forceNotify: HasActiveSkill);
            // 인스펙터 디버그 값 동기화
            SyncDebugValues();
        }

        // 시작 시 현재 장착 무기 스킬 동기화
        private void Start()
        {
            // 시작 시점 장착 무기 존재 검증
            if (syncWithEquippedWeapon && equipHolder is { IsEquippingWeapon: true, GetEquippedWeaponInfo: { } weapon })
            {
                HandleEquipWeapon(weapon);
            }
        }

        // 활성 구간 장착 이벤트 구독
        private void OnEnable()
        {
            // 장착 무기 동기화 구독 등록
            if (syncWithEquippedWeapon && equipHolder.IsNotNull())
            {
                equipHolder.OnEquipWeapon += HandleEquipWeapon;
            }

            // 리소스 레이블 로드 완료 구독 등록
            if (resourceLoader != null)
            {
                resourceLoader.OnLabelResourcesLoadedAll += HandleResourceLabelLoaded;
            }
        }

        // 비활성 구간 장착 이벤트 해제 및 임시 상태 정리
        private void OnDisable()
        {
            // 장착 무기 동기화 구독 해제
            if (syncWithEquippedWeapon && equipHolder.IsNotNull())
            {
                equipHolder.OnEquipWeapon -= HandleEquipWeapon;
            }

            // 리소스 레이블 로드 완료 구독 해제
            if (resourceLoader != null)
            {
                resourceLoader.OnLabelResourcesLoadedAll -= HandleResourceLabelLoaded;
            }

            // 보류 공격 상태 즉시 취소 분기
            if (hasPendingAttack)
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedByStateChange, refreshResolvedFromPreview: false);
            }

            // 활성 콤보 타임아웃 예약 정리
            CancelActiveComboTimeoutRoutine();
        }

        // 프레임 단위 상태 무결성 검사 및 준비 이벤트 처리
        private void Update()
        {
            // 초기화 이전 프레임 가드
            if (skillBook == null || skillCaster == null) return;

            // 보류 공격 재사용 불가 상태 취소 분기
            if (hasPendingAttack && !IsPendingAttackReusableState())
            {
                CancelPendingAttack(PendingCancelReason.InvalidatedByStateChange, refreshResolvedFromPreview: true);
            }

            // 활성 콤보 타임아웃 만료 처리 분기
            if (isActiveComboTimeoutPending && Time.time >= activeComboTimeoutAt)
            {
                // 타임아웃 예약 상태 해제
                isActiveComboTimeoutPending = false;

                // 타임아웃 예약 대상 스킬 일치 검증
                if (HasActiveSkill && skillBook.ActiveSkill == activeComboTimeoutBaseSkill)
                {
                    // 예약 대상 콤보 컨텍스트 조회
                    var context = GetOrCreateComboContext(activeComboTimeoutBaseSkill);
                    // 타임아웃 시점 기대 단계 유지 여부 검증
                    if (context.NextStepIndex == activeComboTimeoutExpectedNextStepIndex)
                    {
                        // 프리뷰 기반 해석 결과 강제 갱신
                        ResetComboProgress(activeComboTimeoutBaseSkill, ignoreComboPreserveMarker: true);
                        UpdateResolvedSkillFromPreview(forceNotify: true);
                    }
                }
            }

            // 쿨다운 준비 완료 콜백 처리
            skillCaster.PollReady(skillBook.Skills, skill =>
            {
                // 스킬 준비 완료 이벤트 전달
                OnSkillReady?.Invoke(skill);
            });

            // 디버그 필드 동기화
            SyncDebugValues();
        }

        // 무기 장착 시 스킬 등록 및 활성 보정 처리
        private void HandleEquipWeapon(WeaponTypeSO weapon)
        {
            // 필수 참조 유효성 가드
            if (weapon.IsNull() || skillBook == null || skillCaster == null) return;

            // 이전 무기 스킬 목록 스냅샷 보관
            var previousWeaponSkills = new List<SkillTypeSO>(equippedWeaponSkills);
            // 현재 장착 무기 스킬 캐시 갱신
            CacheEquippedWeaponSkills(weapon.DefaultSkills);

            // 장착 무기 스킬 등록 및 쿨다운 추적 등록
            bool skillBookChanged = false;
            bool availabilityChanged = false;
            for (int i = 0; i < equippedWeaponSkills.Count; i++)
            {
                // 현재 순회 스킬 참조
                SkillTypeSO skill = equippedWeaponSkills[i];
                // null 스킬 스킵 분기
                if (skill.IsNull()) continue;

                // 스킬북 등록 + 쿨다운 추적 동기화
                bool wasRegistered = skillBook.Contains(skill);
                bool changed = skillBook.Register(skill, equippedWeaponSkillProviderId, isAvailable: true);
                skillCaster.TrackSkill(skill);
                availabilityChanged |= changed;

                if (!wasRegistered && skillBook.Contains(skill))
                {
                    skillBookChanged = true;
                }
            }

            // 이전 무기 스킬 available 해제 루프
            for (int i = 0; i < previousWeaponSkills.Count; i++)
            {
                // 이전 무기 스킬 참조
                SkillTypeSO oldSkill = previousWeaponSkills[i];
                // null 스킬 스킵 분기
                if (oldSkill.IsNull()) continue;

                // available 플래그 해제 누적
                if (equippedWeaponSkills.Contains(oldSkill))
                {
                    continue;
                }

                bool wasRegistered = skillBook.Contains(oldSkill);
                bool changed = skillBook.Revoke(oldSkill, equippedWeaponSkillProviderId);
                availabilityChanged |= changed;

                if (wasRegistered && !skillBook.Contains(oldSkill))
                {
                    skillBookChanged = true;
                }
            }

            // 신규 등록 발생 시 스킬북 변경 알림
            if (skillBookChanged)
            {
                OnSkillBookChanged?.Invoke();
            }

            // available 목록 동기화 및 활성 스킬 보정
            SyncAvailableSkillSet(forceNotify: availabilityChanged);
        }

        // 무기 스킬 캐시 갱신 및 중복 제거
        private void CacheEquippedWeaponSkills(IReadOnlyList<SkillTypeSO> weaponSkills)
        {
            // 기존 캐시 초기화
            equippedWeaponSkills.Clear();
            // 입력 목록 null 가드
            if (weaponSkills == null) return;

            // null 및 중복 스킬 제거 필터링
            for (int i = 0; i < weaponSkills.Count; i++)
            {
                // 입력 목록 스킬 참조
                SkillTypeSO skill = weaponSkills[i];
                // null 또는 중복 스킬 스킵 분기
                if (skill.IsNull() || equippedWeaponSkills.Contains(skill))
                {
                    continue;
                }

                // 필터 통과 스킬 캐시 적재
                equippedWeaponSkills.Add(skill);
            }
        }
    }
}
