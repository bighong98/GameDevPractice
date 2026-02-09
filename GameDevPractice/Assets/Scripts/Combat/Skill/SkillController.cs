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
        event Action<SkillTypeSO> OnSkillReady;

        bool HasActiveSkill { get; }
        SkillTypeSO ActiveSkill { get; }
        bool IsActiveSkillReady { get; }
        float ActiveSkillRange { get; }
        AudioClip ActiveSkillSFX { get; }

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
        [SerializeField] private float activeSkillRemainCooldownDebug;

        private IStatHolder statHolder;
        private EquipmentHolder equipHolder;

        private SkillBook skillBook;
        private SkillCaster skillCaster;

        public event Action<SkillTypeSO> OnActiveSkillChanged;
        public event Action<SkillTypeSO> OnSkillConsumed;
        public event Action<SkillTypeSO> OnSkillReady;

        public bool HasActiveSkill => skillBook != null && skillBook.ActiveSkill.IsNotNull();
        public SkillTypeSO ActiveSkill => skillBook?.ActiveSkill;
        public bool IsActiveSkillReady => HasActiveSkill && skillCaster.IsReady(skillBook.ActiveSkill);
        public float ActiveSkillRange => HasActiveSkill ? Mathf.Max(0f, skillBook.ActiveSkill.Range) : 0f;
        public AudioClip ActiveSkillSFX => HasActiveSkill ? skillBook.ActiveSkill.CastSFX : null;

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
                activeSkillDebug = skill;
                OnActiveSkillChanged?.Invoke(skill);
            }

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

            var skill = skillBook.ActiveSkill;
            if (!skillCaster.IsReady(skill)) return false;
            if (!TryBuildAttackSource(attacker, skill, out attackSource)) return false;

            if (!skillCaster.Consume(skill))
            {
                attackSource = default;
                return false;
            }

            OnSkillConsumed?.Invoke(skill);
            SyncDebugValues();
            return true;
        }

        public bool TryBuildPreviewAttackSource(IAttacker attacker, out AttackSource attackSource)
        {
            attackSource = default;
            if (!HasActiveSkill || attacker.IsNull()) return false;

            return TryBuildAttackSource(attacker, skillBook.ActiveSkill, out attackSource);
        }

        private bool TryBuildAttackSource(IAttacker attacker, SkillTypeSO skill, out AttackSource attackSource)
        {
            attackSource = default;

            if (skill.IsNull() || attacker.IsNull()) return false;

            if (statHolder.IsNotNull() &&
                skill.AttackSourceStatSO.IsNotNull() &&
                statHolder.TryGetStat(skill.AttackSourceStatSO, out var attackSourceStat))
            {
                attackSource = new AttackSource(attacker, attackSourceStat, skill.DamageType);
                return true;
            }

            attackSource = new AttackSource(attacker, skill.BaseDamage, skill.DamageType);
            return true;
        }

        private void SyncDebugValues()
        {
            activeSkillDebug = ActiveSkill;
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

            public bool Consume(SkillTypeSO skill)
            {
                if (skill.IsNull() || !IsReady(skill)) return false;

                float cooldown = Mathf.Max(0f, skill.Cooldown);
                nextReadyAt[skill] = cooldown > 0f ? Time.time + cooldown : Time.time;
                cachedReadyState[skill] = cooldown <= 0f;
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
    }
}
