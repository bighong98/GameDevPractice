using System;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Item;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    public interface ISkillController
    {
        event Action<SkillTypeSO> OnActiveSkillChanged;
        event Action<SkillTypeSO> OnResolvedSkillChanged;
        event Action<SkillTypeSO> OnSkillReady;
        event Action<SkillTypeSO, int, int> OnComboStepChanged;

        bool HasActiveSkill { get; }
        SkillTypeSO ActiveSkill { get; }
        bool HasResolvedSkill { get; }
        SkillTypeSO ResolvedSkill { get; }
        bool IsActiveSkillReady { get; }
        float ActiveSkillRange { get; }
        AudioClip ActiveSkillSFX { get; }
        AudioClip ResolvedSkillSFX { get; }
        int CurrentComboStepIndex { get; }
        int CurrentComboStepCount { get; }

        bool RegisterSkill(SkillTypeSO skill, bool setActive = false);
        bool SetActiveSkill(SkillTypeSO skill);
        float GetRemainingCooldown(SkillTypeSO skill);
        bool TryConsumeActiveSkill(IAttacker attacker, out AttackSource attackSource);
        bool TryBuildPreviewAttackSource(IAttacker attacker, out AttackSource attackSource);
    }

    public sealed class SkillController : MonoBehaviour, ISkillController
    {
        [Header("Initial Skills")]
        [SerializeField] private List<SkillTypeSO> initialSkills = new();
        [SerializeField] private SkillTypeSO defaultActiveSkill;
        [SerializeField] private bool syncWithEquippedWeapon = true;

        [Header("Debug")]
        [SerializeField] private SkillTypeSO activeSkillDebug;
        [SerializeField] private SkillTypeSO resolvedSkillDebug;
        [SerializeField] private int comboStepIndexDebug;
        [SerializeField] private int comboStepCountDebug;
        [SerializeField] private float activeSkillRemainCooldownDebug;

        private IStatHolder statHolder;
        private EquipmentHolder equipHolder;

        private SkillBook skillBook;
        private SkillCaster skillCaster;
        private readonly Dictionary<SkillTypeSO, ComboContext> comboContexts = new();

        private SkillTypeSO resolvedSkill;
        private SkillTypeSO resolvedBaseSkill;
        private int currentComboStepIndex;
        private int currentComboStepCount = 1;

        public event Action<SkillTypeSO> OnActiveSkillChanged;
        public event Action<SkillTypeSO> OnResolvedSkillChanged;
        public event Action<SkillTypeSO> OnSkillConsumed;
        public event Action<SkillTypeSO> OnSkillReady;
        public event Action<SkillTypeSO, int, int> OnComboStepChanged;

        public bool HasActiveSkill => skillBook != null && skillBook.ActiveSkill.IsNotNull();
        public SkillTypeSO ActiveSkill => skillBook?.ActiveSkill;
        public bool HasResolvedSkill => resolvedSkill.IsNotNull();
        public SkillTypeSO ResolvedSkill => resolvedSkill;
        public bool IsActiveSkillReady => HasActiveSkill && skillCaster.IsReady(skillBook.ActiveSkill);
        public float ActiveSkillRange
        {
            get
            {
                var previewSkill = GetPreviewSkill();
                return previewSkill.IsNotNull() ? Mathf.Max(0f, previewSkill.Range) : 0f;
            }
        }

        public AudioClip ActiveSkillSFX
        {
            get
            {
                var previewSkill = GetPreviewSkill();
                return previewSkill.IsNotNull() ? previewSkill.CastSFX : null;
            }
        }

        public AudioClip ResolvedSkillSFX => HasResolvedSkill ? resolvedSkill.CastSFX : null;
        public int CurrentComboStepIndex => currentComboStepIndex;
        public int CurrentComboStepCount => currentComboStepCount;

        private void Awake()
        {
            TryGetComponent(out statHolder);
            TryGetComponent(out equipHolder);

            skillBook = new SkillBook();
            skillCaster = new SkillCaster();

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

        private void Start()
        {
            if (syncWithEquippedWeapon && equipHolder is { IsEquippingWeapon: true, GetEquippedWeaponInfo: { } weapon })
            {
                HandleEquipWeapon(weapon);
            }
        }

        private void OnEnable()
        {
            if (syncWithEquippedWeapon && equipHolder.IsNotNull())
            {
                equipHolder.OnEquipWeapon += HandleEquipWeapon;
            }
        }

        private void OnDisable()
        {
            if (syncWithEquippedWeapon && equipHolder.IsNotNull())
            {
                equipHolder.OnEquipWeapon -= HandleEquipWeapon;
            }
        }

        private void Update()
        {
            if (skillBook == null || skillCaster == null) return;

            skillCaster.PollReady(skillBook.Skills, skill =>
            {
                OnSkillReady?.Invoke(skill);
            });

            SyncDebugValues();
        }

        private void HandleEquipWeapon(WeaponTypeSO weapon)
        {
            if (weapon.IsNull() || weapon.DefaultSkill.IsNull()) return;

            RegisterSkill(weapon.DefaultSkill, setActive: true);
        }

        public bool RegisterSkill(SkillTypeSO skill, bool setActive = false)
        {
            if (skill.IsNull() || skillBook == null || skillCaster == null) return false;

            bool added = skillBook.Register(skill);
            skillCaster.TrackSkill(skill);

            if (setActive)
            {
                SetActiveSkill(skill);
            }

            return added;
        }

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
                ResetComboProgress(skill);
                OnActiveSkillChanged?.Invoke(skill);
            }

            UpdateResolvedSkillFromPreview(forceNotify: changed || !HasResolvedSkill);
            SyncDebugValues();
            return changed;
        }

        public float GetRemainingCooldown(SkillTypeSO skill)
        {
            if (skill.IsNull() || skillCaster == null) return 0f;
            return skillCaster.GetRemainingCooldown(skill);
        }

        public bool TryConsumeActiveSkill(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;

            if (!HasActiveSkill || attacker.IsNull()) return false;

            var baseSkill = skillBook.ActiveSkill;
            if (!skillCaster.IsReady(baseSkill)) return false;
            if (!TryResolveSkillPreview(baseSkill, out var resolved, out var stepIndex, out var stepCount)) return false;
            if (!TryBuildAttackSource(attacker, resolved, out attackSource)) return false;

            if (!skillCaster.Consume(baseSkill, baseSkill.Cooldown))
            {
                attackSource = default;
                return false;
            }

            CommitComboProgress(baseSkill, stepIndex, stepCount);
            SetResolvedSkill(resolved, baseSkill, stepIndex, stepCount, forceNotify: true);
            OnSkillConsumed?.Invoke(resolved);
            SyncDebugValues();
            return true;
        }

        public bool TryBuildPreviewAttackSource(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;
            if (!HasActiveSkill || attacker.IsNull()) return false;

            var previewSkill = GetPreviewSkill();
            if (previewSkill.IsNull()) return false;

            return TryBuildAttackSource(attacker, previewSkill, out attackSource);
        }

        private SkillTypeSO GetPreviewSkill()
        {
            if (!HasActiveSkill) return null;

            if (TryResolveSkillPreview(skillBook.ActiveSkill, out var previewSkill, out _, out _))
                return previewSkill;

            return skillBook.ActiveSkill;
        }

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

        private static bool ShouldResetCombo(float timeout, ComboContext context)
        {
            if (context.LastConsumeTime < 0f) return true;
            if (timeout <= 0f) return true;

            return Time.time > context.LastConsumeTime + timeout;
        }

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

        private void ResetComboProgress(SkillTypeSO baseSkill)
        {
            if (baseSkill.IsNull()) return;

            var context = GetOrCreateComboContext(baseSkill);
            context.NextStepIndex = 0;
            context.LastConsumeTime = -1f;
        }

        private ComboContext GetOrCreateComboContext(SkillTypeSO baseSkill)
        {
            if (!comboContexts.TryGetValue(baseSkill, out var context))
            {
                context = new ComboContext();
                comboContexts[baseSkill] = context;
            }

            return context;
        }

        private void UpdateResolvedSkillFromPreview(bool forceNotify)
        {
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

        private bool TryBuildAttackSource(IAttacker attacker, SkillTypeSO skill, out AttackSource attackSource)
        {
            attackSource = default;

            if (skill.IsNull() || attacker.IsNull()) return false;

            bool hasAttackSourceStat = TryResolveAttackSourceStat(skill, out var attackSourceStat);
            float sourceDamage = hasAttackSourceStat ? attackSourceStat.Value : skill.BaseDamage;
            float perHitDamage = Mathf.Max(0f, sourceDamage * skill.AttackCoefficient);
            int hitCount = Mathf.Max(1, skill.HitCount);

            if (hitCount <= 1)
            {
                if (hasAttackSourceStat && Mathf.Approximately(skill.AttackCoefficient, 1f))
                {
                    attackSource = new AttackSource(attacker, attackSourceStat, skill.DamageType);
                    return true;
                }

                attackSource = new AttackSource(attacker, perHitDamage, skill.DamageType);
                return true;
            }

            var hitDamages = BuildHitDamages(perHitDamage, hitCount);
            attackSource = new AttackSource(attacker, null, perHitDamage, skill.DamageType, 0, hitDamages);
            return true;
        }

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

        private static List<float> BuildHitDamages(float perHitDamage, int hitCount)
        {
            int resolvedHitCount = Mathf.Max(1, hitCount);
            var hitDamages = new List<float>(resolvedHitCount);
            for (int i = 0; i < resolvedHitCount; i++)
            {
                hitDamages.Add(perHitDamage);
            }

            return hitDamages;
        }

        private void SyncDebugValues()
        {
            activeSkillDebug = ActiveSkill;
            resolvedSkillDebug = ResolvedSkill;
            comboStepIndexDebug = CurrentComboStepIndex;
            comboStepCountDebug = CurrentComboStepCount;
            activeSkillRemainCooldownDebug = HasActiveSkill
                ? skillCaster.GetRemainingCooldown(skillBook.ActiveSkill)
                : 0f;
        }

        [Serializable]
        private sealed class SkillBook
        {
            private readonly List<SkillTypeSO> skills = new();

            public IReadOnlyList<SkillTypeSO> Skills => skills;
            public SkillTypeSO ActiveSkill { get; private set; }

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

            public bool Contains(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                return skills.Contains(skill);
            }

            public bool SetActive(SkillTypeSO skill)
            {
                if (!Contains(skill)) return false;
                if (ActiveSkill == skill) return false;

                ActiveSkill = skill;
                return true;
            }

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

        [Serializable]
        private sealed class SkillCaster
        {
            private readonly Dictionary<SkillTypeSO, float> nextReadyAt = new();
            private readonly Dictionary<SkillTypeSO, bool> cachedReadyState = new();

            public void TrackSkill(SkillTypeSO skill)
            {
                if (skill.IsNull()) return;

                if (!nextReadyAt.ContainsKey(skill))
                {
                    nextReadyAt[skill] = 0f;
                }

                cachedReadyState[skill] = true;
            }

            public bool IsReady(SkillTypeSO skill)
            {
                if (skill.IsNull()) return false;
                if (!nextReadyAt.TryGetValue(skill, out var readyTime)) return true;

                return Time.time >= readyTime;
            }

            public bool Consume(SkillTypeSO skill, float cooldown)
            {
                if (skill.IsNull() || !IsReady(skill)) return false;

                float appliedCooldown = Mathf.Max(0f, cooldown);
                nextReadyAt[skill] = appliedCooldown > 0f ? Time.time + appliedCooldown : Time.time;
                cachedReadyState[skill] = appliedCooldown <= 0f;
                return true;
            }

            public float GetRemainingCooldown(SkillTypeSO skill)
            {
                if (skill.IsNull()) return 0f;
                if (!nextReadyAt.TryGetValue(skill, out var readyTime)) return 0f;

                return Mathf.Max(0f, readyTime - Time.time);
            }

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

        [Serializable]
        private sealed class ComboContext
        {
            public int NextStepIndex;
            public float LastConsumeTime = -1f;
        }
    }
}
