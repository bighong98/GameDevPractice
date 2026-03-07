using System;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using TH.Attribute.Data;
using TH.Attribute.Stat;
using TH.Combat;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Control
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterAnimationController : MonoBehaviour, ISkillTimingScaleProvider
    {
        private const int AttackSpeedLegacyId = 203;
        private const string DefaultAttackSpeedMultiplierParameter = "AttackSpeedMultiplier";
        private const float DefaultBaseAttackSpeedStatValue = 100f;
        private const float LegacyCurveTargetNormalizedAttackSpeed = 4f;
        private static readonly int AttackStateShortNameHash = Animator.StringToHash("Attack");
        private const float LegacyCurveTargetMultiplier = 2f;

        [Header("Default")]
        [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
        [SerializeField] private Animator animator;

        [Header("Attack Animation Speed")]
        [SerializeField] private string attackSpeedMultiplierParameter = DefaultAttackSpeedMultiplierParameter;

        [SerializeField] private AssetReferenceGameStatSO attackSpeedStatReference;
        [SerializeField] private GameStatSO attackSpeedStat;
        [SerializeField, Min(0.01f)] private float baseAttackSpeedStatValue = DefaultBaseAttackSpeedStatValue;
        [SerializeField] private AnimationCurve attackSpeedToAnimationCurve =
            new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1f), new Keyframe(2f, 1.5f), new Keyframe(4f, 2f));
        [SerializeField, Min(0.01f)] private float minAnimationSpeed = 0.1f;
        [SerializeField, Min(0.01f)] private float maxAnimationSpeed = 3f;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private SkillTypeSO activeSkillDebug;
        [SerializeField] private SkillTypeSO resolvedSkillDebug;
        [SerializeField, Min(1)] private int resolvedHitCountDebug = 1;
        [SerializeField, Min(0f)] private float resolvedAttackCoefficientDebug = 1f;
        [SerializeField, Min(0f)] private float attackSpeedStatValueDebug = 100f;
        [SerializeField, Min(0.01f)] private float attackAnimationSpeedDebug = 1f;
#endif

        private ISkillController skillController;
        private IStatHolder statHolder;
        private RuntimeAnimatorController baseAnimatorController;

        private GameStatSO resolvedAttackSpeedStat;
        private float currentAttackSpeedStatValue = 100f;
        private int attackSpeedMultiplierParameterHash;
        private bool hasAttackSpeedMultiplierParameter;

        private bool isResolvingAttackSpeedStatReference;
        private bool isAttackSpeedBound;
        private SkillTypeSO lastEffectiveSkill;
        public float SkillTimingScale { get; private set; } = 1f;

        private void Awake()
        {
            if (animator.IsNull())
                TryGetComponent(out animator);

            TryGetComponent(out skillController);
            TryGetComponent(out statHolder);

            currentAttackSpeedStatValue = ResolveBaseAttackSpeedStatValue();
            CacheBaseAnimatorController();

            EnsureAttackSpeedStatAsync().Forget();
            CacheAttackSpeedParameter();
        }

        private void Start()
        {
            ApplySkillAnimator(ResolveEffectiveSkill());
        }

        private void OnEnable()
        {
            if (skillController != null)
            {
                skillController.OnActiveSkillChanged += HandleActiveSkillChanged;
                skillController.OnResolvedSkillChanged += HandleResolvedSkillChanged;
            }


            EnsureAttackSpeedStatAsync().Forget();
            BindAttackSpeedStat();
            RefreshAttackAnimationSpeed();
        }

        private void OnDisable()
        {
            if (skillController != null)
            {
                skillController.OnActiveSkillChanged -= HandleActiveSkillChanged;
                skillController.OnResolvedSkillChanged -= HandleResolvedSkillChanged;
            }

            UnbindAttackSpeedStat();
        }

        private void Update()
        {
            // Initialization order can delay stat registration; keep trying until bound.
            if (!isAttackSpeedBound)
            {
                BindAttackSpeedStat();
                EnsureAttackSpeedStatAsync().Forget();
            }

            // Ensure runtime skill/override changes are reflected even if an event is missed.
            var effectiveSkill = ResolveEffectiveSkill();
            if (!ReferenceEquals(lastEffectiveSkill, effectiveSkill))
            {
                lastEffectiveSkill = effectiveSkill;
                RefreshAttackAnimationSpeed();
            }

            SyncAttackSpeedFromStat();
        }

        private void HandleActiveSkillChanged(SkillTypeSO skill)
        {
            _ = skill;
            ApplySkillAnimator(ResolveEffectiveSkill());
        }

        private void HandleResolvedSkillChanged(SkillTypeSO skill)
        {
            _ = skill;
            ApplySkillAnimator(ResolveEffectiveSkill());
        }

        private void HandleAttackSpeedChanged(float value)
        {
            currentAttackSpeedStatValue = Mathf.Max(0f, value);
            RefreshAttackAnimationSpeed();
        }

        public void ApplySkillAnimator(SkillTypeSO skill)
        {
#if UNITY_EDITOR
            SetDebugActiveSkill(skill);
#endif
            if (animator.IsNull()) return;

            if (skill.IsNotNull() && skill.AnimatorOverride.IsNotNull())
            {
                animator.runtimeAnimatorController = skill.AnimatorOverride;
            }
            else
            {
                RestoreBaseAnimator();
            }

            lastEffectiveSkill = ResolveEffectiveSkill();
            LogAnimatorSelection(skill);
            CacheAttackSpeedParameter();
            RefreshAttackAnimationSpeed();
        }

        private void BindAttackSpeedStat()
        {
            if (isAttackSpeedBound || statHolder == null)
                return;

            resolvedAttackSpeedStat = ResolveAttackSpeedStat();
            if (resolvedAttackSpeedStat.IsNull())
                return;

            if (!statHolder.TryGetStat(resolvedAttackSpeedStat, out var attackSpeedRuntimeStat))
                return;

            statHolder.BindStatChanged(resolvedAttackSpeedStat, HandleAttackSpeedChanged, pending: true);
            currentAttackSpeedStatValue = Mathf.Max(0f, attackSpeedRuntimeStat.Value);
            isAttackSpeedBound = true;
            RefreshAttackAnimationSpeed();
        }

        private void UnbindAttackSpeedStat()
        {
            if (statHolder != null && resolvedAttackSpeedStat.IsNotNull())
            {
                statHolder.UnbindStatChanged(resolvedAttackSpeedStat, HandleAttackSpeedChanged);
            }

            resolvedAttackSpeedStat = null;
            isAttackSpeedBound = false;
        }

        private async UniTaskVoid EnsureAttackSpeedStatAsync()
        {
            if (attackSpeedStat.IsNotNull())
            {
                return;
            }

            if (isResolvingAttackSpeedStatReference)
            {
                return;
            }

            if (attackSpeedStatReference == null || !attackSpeedStatReference.RuntimeKeyIsValid())
            {
                return;
            }

            isResolvingAttackSpeedStatReference = true;
            try
            {
                var loadedStat = await ResourceManager.Instance.ExtractAssetRefAsync<GameStatSO>(
                    attackSpeedStatReference,
                    this.GetCancellationTokenOnDestroy());

                if (loadedStat.IsNull())
                {
                    return;
                }

                attackSpeedStat = loadedStat;
                if (isActiveAndEnabled && !isAttackSpeedBound)
                {
                    BindAttackSpeedStat();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                isResolvingAttackSpeedStatReference = false;
            }
        }

        private GameStatSO ResolveAttackSpeedStat()
        {
            if (attackSpeedStat.IsNotNull())
            {
                return attackSpeedStat;
            }

            if (GameStats.TryGetByLegacyId(AttackSpeedLegacyId, out var cachedAttackSpeedStat) &&
                cachedAttackSpeedStat.IsNotNull())
            {
                return cachedAttackSpeedStat;
            }

            // ResolveAttackSpeedStat() 호출 시점에 StatHolder 내부 초기화가 되지 않은 경우 fallback 
            // todo: 초기화 순서에 의한 레이스 컨디션 조정 후 fallback 제거
            if (statHolder is StatHolder concreteHolder)
            {
                foreach (var pair in concreteHolder.Stats)
                {
                    var statKey = pair.Key;
                    if (statKey != null && statKey.LegacyId == AttackSpeedLegacyId)
                    {
                        return statKey;
                    }
                }
            }

            return null;
        }

        private void SyncAttackSpeedFromStat()
        {
            if (!isAttackSpeedBound || statHolder == null || resolvedAttackSpeedStat.IsNull())
                return;

            if (!statHolder.TryGetStat(resolvedAttackSpeedStat, out var attackSpeedRuntimeStat) || attackSpeedRuntimeStat == null)
                return;

            float nextValue = Mathf.Max(0f, attackSpeedRuntimeStat.Value);
            if (Mathf.Approximately(nextValue, currentAttackSpeedStatValue))
                return;

            currentAttackSpeedStatValue = nextValue;
            RefreshAttackAnimationSpeed();
        }

        private void RefreshAttackAnimationSpeed()
        {
            SkillTypeSO skill = ResolveEffectiveSkill();
            float skillMultiplier = skill.IsNotNull() ? skill.AnimationSpeedMultiplier : 1f;

            float attackSpeedMultiplier = 1f;
            if (skill.IsNotNull() && skill.AffectedByAttackSpeed)
            {
                float normalizedAttackSpeed = Mathf.Max(0f, currentAttackSpeedStatValue) /
                                              ResolveBaseAttackSpeedStatValue();
                attackSpeedMultiplier = EvaluateAttackSpeedMultiplier(normalizedAttackSpeed);
            }

            float finalMultiplier = Mathf.Clamp(
                skillMultiplier * Mathf.Max(0.01f, attackSpeedMultiplier),
                Mathf.Min(minAnimationSpeed, maxAnimationSpeed),
                Mathf.Max(minAnimationSpeed, maxAnimationSpeed));

            SkillTimingScale = finalMultiplier;
            if (!animator.IsNull() && hasAttackSpeedMultiplierParameter)
            {
                animator.SetFloat(attackSpeedMultiplierParameterHash, finalMultiplier);
            }

#if UNITY_EDITOR
            attackSpeedStatValueDebug = currentAttackSpeedStatValue;
            attackAnimationSpeedDebug = finalMultiplier;
#endif
        }

        private SkillTypeSO ResolveEffectiveSkill()
        {
            if (skillController != null && skillController.HasExecutingSkill)
            {
                return skillController.ExecutingSkill;
            }

            // Keep the currently playing attack clip stable until the state exits Attack.
            if (IsAttackStateActive() && lastEffectiveSkill.IsNotNull())
            {
                return lastEffectiveSkill;
            }

            if (skillController != null && skillController.HasResolvedSkill)
            {
                return skillController.ResolvedSkill;
            }

            if (skillController != null && skillController.HasActiveSkill)
            {
                return skillController.ActiveSkill;
            }

            return null;
        }

        private bool IsAttackStateActive()
        {
            if (animator.IsNull())
            {
                return false;
            }

            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == AttackStateShortNameHash)
            {
                return true;
            }

            if (!animator.IsInTransition(0))
            {
                return false;
            }

            var next = animator.GetNextAnimatorStateInfo(0);
            return next.shortNameHash == AttackStateShortNameHash;
        }

        private void CacheAttackSpeedParameter()
        {
            hasAttackSpeedMultiplierParameter = false;
            string resolvedParameter = ResolveAttackSpeedMultiplierParameter();

            if (animator.IsNull() || string.IsNullOrWhiteSpace(resolvedParameter))
                return;

            attackSpeedMultiplierParameterHash = Animator.StringToHash(resolvedParameter);
            var parameters = animator.parameters;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type != AnimatorControllerParameterType.Float)
                    continue;

                if (parameters[i].nameHash != attackSpeedMultiplierParameterHash)
                    continue;

                hasAttackSpeedMultiplierParameter = true;
                break;
            }
        }

        private float ResolveBaseAttackSpeedStatValue()
        {
            return baseAttackSpeedStatValue > 0.01f
                ? baseAttackSpeedStatValue
                : DefaultBaseAttackSpeedStatValue;
        }

        private float EvaluateAttackSpeedMultiplier(float normalizedAttackSpeed)
        {
            float clampedNormalized = Mathf.Max(0f, normalizedAttackSpeed);
            if (attackSpeedToAnimationCurve != null && attackSpeedToAnimationCurve.length > 0)
            {
                var keys = attackSpeedToAnimationCurve.keys;
                float firstTime = keys[0].time;
                float lastTime = keys[keys.Length - 1].time;

                // Legacy data can still have a 3-key curve ending at x=2.
                // Keep that data working while preserving the intended x=4 => 2.0 behavior.
                if (lastTime < LegacyCurveTargetNormalizedAttackSpeed)
                {
                    float safeNormalized = Mathf.Max(firstTime, clampedNormalized);
                    float lastValue = attackSpeedToAnimationCurve.Evaluate(lastTime);

                    if (safeNormalized <= lastTime)
                    {
                        return attackSpeedToAnimationCurve.Evaluate(safeNormalized);
                    }

                    if (safeNormalized >= LegacyCurveTargetNormalizedAttackSpeed)
                    {
                        return Mathf.Max(lastValue, LegacyCurveTargetMultiplier);
                    }

                    float t = Mathf.InverseLerp(lastTime, LegacyCurveTargetNormalizedAttackSpeed, safeNormalized);
                    return Mathf.Lerp(lastValue, LegacyCurveTargetMultiplier, t);
                }

                float safeClamped = Mathf.Clamp(clampedNormalized, firstTime, lastTime);
                return attackSpeedToAnimationCurve.Evaluate(safeClamped);
            }

            return clampedNormalized;
        }

        private string ResolveAttackSpeedMultiplierParameter()
        {
            return string.IsNullOrWhiteSpace(attackSpeedMultiplierParameter)
                ? DefaultAttackSpeedMultiplierParameter
                : attackSpeedMultiplierParameter;
        }

        private void CacheBaseAnimatorController()
        {
            if (defaultAnimatorController.IsNotNull())
            {
                baseAnimatorController = defaultAnimatorController;
                return;
            }

            if (animator.IsNull()) return;

            if (animator.runtimeAnimatorController is AnimatorOverrideController overrideController)
            {
                baseAnimatorController = overrideController.runtimeAnimatorController;
                return;
            }

            baseAnimatorController = animator.runtimeAnimatorController;
        }

        private void RestoreBaseAnimator()
        {
            if (animator.IsNull()) return;

            if (baseAnimatorController.IsNotNull())
            {
                animator.runtimeAnimatorController = baseAnimatorController;
                return;
            }

            if (animator.runtimeAnimatorController is AnimatorOverrideController overrideController)
            {
                animator.runtimeAnimatorController = overrideController.runtimeAnimatorController;
            }
        }

#if UNITY_EDITOR
        private void SetDebugActiveSkill(SkillTypeSO skill)
        {
            activeSkillDebug = skill;
            resolvedSkillDebug = skillController != null ? skillController.ResolvedSkill : null;

            var debugSkill = resolvedSkillDebug.IsNotNull() ? resolvedSkillDebug : skill;
            resolvedHitCountDebug = debugSkill.IsNotNull() ? debugSkill.HitCount : 1;
            resolvedAttackCoefficientDebug = debugSkill.IsNotNull() ? debugSkill.AttackCoefficient : 1f;
        }
#endif

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private void LogAnimatorSelection(SkillTypeSO selectedSkill)
        {
            string selectedName = selectedSkill.IsNotNull() ? selectedSkill.name : "null";
            string selectedOverride = selectedSkill.IsNotNull() && selectedSkill.AnimatorOverride.IsNotNull()
                ? selectedSkill.AnimatorOverride.name
                : "base";
            string activeName = skillController != null && skillController.HasActiveSkill && skillController.ActiveSkill.IsNotNull()
                ? skillController.ActiveSkill.name
                : "null";
            string resolvedName = skillController != null && skillController.HasResolvedSkill && skillController.ResolvedSkill.IsNotNull()
                ? skillController.ResolvedSkill.name
                : "null";
            string executingName = skillController != null && skillController.HasExecutingSkill && skillController.ExecutingSkill.IsNotNull()
                ? skillController.ExecutingSkill.name
                : "null";
            string runtimeControllerName = animator != null && animator.runtimeAnimatorController != null
                ? animator.runtimeAnimatorController.name
                : "null";

            Logg.Log(
                $"[{nameof(CharacterAnimationController)}.{nameof(ApplySkillAnimator)}] " +
                $"owner={gameObject.name}, frame={Time.frameCount}, time={Time.time:0.000}, " +
                $"selected={selectedName}, selectedOverride={selectedOverride}, runtimeController={runtimeControllerName}, " +
                $"executing={executingName}, resolved={resolvedName}, active={activeName}",
                Logg.LoggingMode.Completed);
        }
    }
}
