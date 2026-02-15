using System;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Combat;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TH.Resource
{
    // 스킬 1개의 전투 데이터/연출 데이터/에디터 검증 규칙을 보관하는 SO
    [CreateAssetMenu(fileName = "SkillTypeSO", menuName = "Scriptable Objects/Type/Skill/SkillTypeSO")]
    public class SkillTypeSO : ScriptableObject
    {
        [Header("Skill Data")]
        // 런타임 식별자 -> 비어 있으면 에셋 이름으로 대체
        [SerializeField] private string skillId;
        // 데미지 계산 시 기준이 되는 스탯 SO (-> 없으면 baseDamage 사용)
        [SerializeField] private GameStatSO attackSourceStatSO;
        // 콤보 단계 스킬은 각 SkillTypeSO에서 데미지/사거리 값을 개별 설정
        [SerializeField] private float baseDamage = 1f;
        // 타격 횟수(최소 1).
        [SerializeField, Min(1)] private int hitCount = 1;
        // 공격 계수(최소 0). 최종 데미지 = sourceDamage * attackCoefficient
        [SerializeField, Min(0f)] private float attackCoefficient = 1f;
        // 데미지 속성 타입
        [SerializeField] private DamageType damageType = DamageType.Physical;
        // 스킬 유효 사거리
        [SerializeField, Min(0f)] private float range = 2f;
        // 베이스 스킬 공용 쿨다운. comboTimeout 초과 시 콤보 연계 단절 가능
        [SerializeField, Min(0f)] private float cooldown = 1f;
        // 콤보 사용 시 ComboSequenceSO 에셋 연결. null이면 단일 스킬 동작
        [SerializeField] private ComboSequenceSO comboSequence;
        [SerializeField] private SkillExecutionProfileSO executionProfile;

        [Header("Targeting")]
        [SerializeField] private SkillTargetPolicy targetPolicy = SkillTargetPolicy.EnemyOnlyDefault;

        [Header("Animation Speed")]
        [SerializeField, Min(0.01f)] private float animationSpeedMultiplier = 1f;
        [SerializeField] private bool affectedByAttackSpeed = true;

        [Header("Animation")]
        // 콤보 단계별 애니메이션 오버라이드 개별 설정
        [SerializeField] private AnimatorOverrideController animatorOverride;

        [Header("Projectile")]
        [SerializeField] private GameObject projectilePrefab; // 원거리 스킬의 발사체 프리팹

        [Header("VFX")]
        [SerializeField] private GameObject skillVFXPrefab; // 스킬 사용 시 재생할 이펙트 프리팹
        [SerializeField] private GameObject onHitVFXPrefab; // 적중 시 재생할 이펙트 프리팹

        [Header("SFX")]
        // Cast sound for this skill
        [SerializeField] private AudioClip castSfx;

        [Header("UI")]
        [SerializeField] private SkillCategory skillCategory = SkillCategory.AdditiveSkill;
        [SerializeField] private Sprite skillSlotImage;

        // 유효한 스킬 ID(없으면 에셋 이름 대체)
        public string SkillId => string.IsNullOrWhiteSpace(skillId) ? name : skillId;
        // 공격 소스 스탯 SO
        public GameStatSO AttackSourceStatSO => attackSourceStatSO;
        // 기본 데미지
        public float BaseDamage => baseDamage;
        // 보정된 히트 수(최소 1)
        public int HitCount => Mathf.Max(1, hitCount);
        // 보정된 공격 계수(최소 0)
        public float AttackCoefficient => Mathf.Max(0f, attackCoefficient);
        // 데미지 타입
        public DamageType DamageType => damageType;
        // 스킬 사거리
        public float Range => range;
        // 스킬 쿨다운
        public float Cooldown => cooldown;
        // 콤보 시퀀스 참조
        public ComboSequenceSO ComboSequence => comboSequence;
        public SkillExecutionProfileSO ExecutionProfile => executionProfile;
        public SkillTargetPolicy TargetPolicy => targetPolicy;
        // Skill animation speed multiplier
        public float AnimationSpeedMultiplier => Mathf.Max(0.01f, animationSpeedMultiplier);
        // Whether to apply attack-speed stat scaling
        public bool AffectedByAttackSpeed => affectedByAttackSpeed;
        // Cast sound clip
        public AudioClip CastSFX => castSfx;
        public SkillCategory SkillCategory => skillCategory;
        public Sprite SkillSlotImage => skillSlotImage;
        // 공격 애니메이션 오버라이드
        public AnimatorOverrideController AnimatorOverride => animatorOverride;
        // 발사체 사용 여부
        public bool HasProjectile => projectilePrefab != null;
        // 발사체 프리팹
        public GameObject ProjectilePrefab => projectilePrefab;
        // 적중 이펙트 사용 여부
        public bool HasSkillEffect => skillVFXPrefab != null;
        public GameObject SkillEffectPrefab => skillVFXPrefab;
        public bool HasOnHitEffect => onHitVFXPrefab != null;
        public GameObject OnHitEffectPrefab => onHitVFXPrefab;
        public bool HasImpactEffect => HasOnHitEffect;
        // 적중 이펙트 프리팹
        public GameObject ImpactParticlePrefab => onHitVFXPrefab;

        #region Debug (Editor Only)
#if UNITY_EDITOR
        // 인스펙터 컨텍스트 메뉴에서 수동 검증을 실행
        [ContextMenu("Validate Skill (Editor)")]
        private void ValidateSkillInEditor()
        {
            ValidateAndLogInEditor();
        }

        // 에디터 검증 수행 후 로그를 출력
        public bool ValidateAndLogInEditor(string logPrefix = null)
        {
            var isValid = ValidateInEditor(out var errors, out var warnings);
            var prefix = string.IsNullOrWhiteSpace(logPrefix)
                ? $"[SkillTypeSO:{name}]"
                : $"[{logPrefix}][SkillTypeSO:{name}]";

            if (errors.Count == 0 && warnings.Count == 0)
            {
                Debug.Log($"{prefix} Validation passed.", this);
                return true;
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                Debug.LogWarning($"{prefix} Warning: {warnings[i]}", this);
            }

            for (int i = 0; i < errors.Count; i++)
            {
                Debug.LogError($"{prefix} Error: {errors[i]}", this);
            }

            return isValid;
        }

        // 스킬 데이터의 정합성을 점검하고 에러/경고 목록을 반환
        public bool ValidateInEditor(out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(skillId))
            {
                warnings.Add("skillId is empty. Asset name fallback will be used at runtime.");
            }

            if (float.IsNaN(baseDamage) || float.IsInfinity(baseDamage))
            {
                errors.Add("baseDamage must be a finite number.");
            }

            if (hitCount < 1)
            {
                errors.Add("hitCount must be >= 1.");
            }

            if (float.IsNaN(attackCoefficient) || float.IsInfinity(attackCoefficient) || attackCoefficient < 0f)
            {
                errors.Add("attackCoefficient must be a finite value >= 0.");
            }

            if (float.IsNaN(range) || float.IsInfinity(range) || range < 0f)
            {
                errors.Add("range must be a finite value >= 0.");
            }

            if (float.IsNaN(cooldown) || float.IsInfinity(cooldown) || cooldown < 0f)
            {
                errors.Add("cooldown must be a finite value >= 0.");
            }

            if (attackSourceStatSO == null && baseDamage <= 0f)
            {
                warnings.Add("attackSourceStatSO is null and baseDamage <= 0. Actual damage may become 0.");
            }

            if (attackCoefficient <= 0f)
            {
                warnings.Add("attackCoefficient is 0. Actual damage may become 0.");
            }

            if (comboSequence != null && !comboSequence.HasSteps)
            {
                errors.Add("comboSequence is assigned but has no combo steps.");
            }

            if (executionProfile != null && !executionProfile.HasActions)
            {
                errors.Add("executionProfile is assigned but has no actions.");
            }

            ValidateEffectPrefab(skillVFXPrefab, nameof(skillVFXPrefab), warnings);
            ValidateEffectPrefab(onHitVFXPrefab, nameof(onHitVFXPrefab), warnings);
            ValidateAnimatorOverride(errors, warnings);
            return errors.Count == 0;
        }

        // 애니메이션 오버라이드와 Hit 이벤트 설정을 검증
        private static void ValidateEffectPrefab(GameObject prefab, string fieldName, List<string> warnings)
        {
            if (prefab == null)
            {
                return;
            }

            if (prefab.GetComponent<SimplePooledParticlePlayer>() == null)
            {
                warnings.Add($"{fieldName} does not contain SimplePooledParticlePlayer. Pool playback can fail at runtime.");
            }
        }

        private void ValidateAnimatorOverride(List<string> errors, List<string> warnings)
        {
            if (animatorOverride == null)
            {
                warnings.Add("animatorOverride is null. Attack will use base controller clip.");
                return;
            }

            var overridePairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            animatorOverride.GetOverrides(overridePairs);

            if (overridePairs.Count == 0)
            {
                warnings.Add("animatorOverride has no override entries.");
                return;
            }

            var attackClips = new HashSet<AnimationClip>();
            var fallbackClips = new HashSet<AnimationClip>();

            // Attack 명명 규칙을 우선 적용하고, 없으면 전체 클립을 폴백으로 사용
            for (int i = 0; i < overridePairs.Count; i++)
            {
                var original = overridePairs[i].Key;
                var resolved = overridePairs[i].Value != null ? overridePairs[i].Value : overridePairs[i].Key;
                if (resolved == null)
                {
                    continue;
                }

                fallbackClips.Add(resolved);

                bool attackNamed = ContainsAttackWord(original) || ContainsAttackWord(resolved);
                if (attackNamed)
                {
                    attackClips.Add(resolved);
                }
            }

            if (attackClips.Count == 0)
            {
                warnings.Add("No Attack-named clip slot found in animatorOverride. Hit event scan will use all resolved clips.");
                attackClips = fallbackClips;
            }

            bool hasAnyHit = false;
            foreach (var clip in attackClips)
            {
                if (clip == null)
                {
                    continue;
                }

                var events = AnimationUtility.GetAnimationEvents(clip);
                int hitCount = 0;
                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    if (string.Equals(evt.functionName, "Hit", StringComparison.Ordinal))
                    {
                        hitCount++;
                        hasAnyHit = true;

                        if (evt.time <= 0f || evt.time >= clip.length)
                        {
                            warnings.Add($"Hit event timing is near clip boundary. clip={clip.name}, time={evt.time:0.###}, length={clip.length:0.###}");
                        }
                    }
                    else if (string.Equals(evt.functionName, "hit", StringComparison.OrdinalIgnoreCase))
                    {
                        warnings.Add($"Animation event function name uses wrong case. Expected 'Hit'. clip={clip.name}, function={evt.functionName}");
                    }
                }

                if (hitCount == 0)
                {
                    errors.Add($"Missing Hit event in attack clip. clip={clip.name}");
                }
                else if (hitCount > 1)
                {
                    warnings.Add($"Multiple Hit events found in clip. clip={clip.name}, count={hitCount}");
                }
            }

            if (!hasAnyHit)
            {
                errors.Add("No valid 'Hit' event found in animatorOverride attack clips.");
            }
        }

        // 클립 이름에 Attack 키워드가 포함되는지 확인 (임시 사용)
        private static bool ContainsAttackWord(AnimationClip clip)
        {
            return clip != null && clip.name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0;
        }
#endif
        #endregion
    
    }
}
