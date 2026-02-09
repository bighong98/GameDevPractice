using System;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Combat;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace TH.Resource
{
    [CreateAssetMenu(fileName = "SkillTypeSO", menuName = "Scriptable Objects/Type/Skill/SkillTypeSO")]
    public class SkillTypeSO : ScriptableObject
    {
        [Header("Skill Data")]
        [SerializeField] private string skillId;
        [SerializeField] private GameStatSO attackSourceStatSO;
        // 콤보 단계 스킬은 각 SkillTypeSO에서 데미지/사거리 값을 개별 설정.
        [SerializeField] private float baseDamage = 1f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField, Min(0f)] private float range = 2f;
        // 베이스 스킬 공용 쿨다운. comboTimeout 초과 시 콤보 연계 단절 가능.
        [SerializeField, Min(0f)] private float cooldown = 1f;
        // 콤보 사용 시 ComboSequenceSO 에셋 연결. null이면 단일 스킬 동작.
        [SerializeField] private ComboSequenceSO comboSequence;

        [Header("VFX")]
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private GameObject impactParticlePrefab;

        [Header("Animation")]
        // 콤보 단계별 애니메이션 오버라이드 개별 설정.
        [SerializeField] private AnimatorOverrideController animatorOverride;

        [Header("SFX")]
        // 콤보 단계별 캐스트 SFX 개별 설정.
        [SerializeField] private AudioClip castSfx;

        public string SkillId => string.IsNullOrWhiteSpace(skillId) ? name : skillId;
        public GameStatSO AttackSourceStatSO => attackSourceStatSO;
        public float BaseDamage => baseDamage;
        public DamageType DamageType => damageType;
        public float Range => range;
        public float Cooldown => cooldown;
        public ComboSequenceSO ComboSequence => comboSequence;
        public AudioClip CastSFX => castSfx;
        public AnimatorOverrideController AnimatorOverride => animatorOverride;
        public bool HasProjectile => projectilePrefab != null;
        public GameObject ProjectilePrefab => projectilePrefab;
        public bool HasImpactEffect => impactParticlePrefab != null;
        public GameObject ImpactParticlePrefab => impactParticlePrefab;
#if UNITY_EDITOR
        [ContextMenu("Validate Skill (Editor)")]
        private void ValidateSkillInEditor()
        {
            ValidateAndLogInEditor();
        }

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

            if (comboSequence != null && !comboSequence.HasSteps)
            {
                errors.Add("comboSequence is assigned but has no combo steps.");
            }

            ValidateAnimatorOverride(errors, warnings);
            return errors.Count == 0;
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

        private static bool ContainsAttackWord(AnimationClip clip)
        {
            return clip != null && clip.name.IndexOf("Attack", StringComparison.OrdinalIgnoreCase) >= 0;
        }
#endif

    }
}
