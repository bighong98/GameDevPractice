using TH.Attribute;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    public readonly struct SkillExecutionContext
    {
        public readonly IAttacker Attacker;
        public readonly Health PrimaryTarget;
        public readonly SkillTypeSO Skill;
        public readonly IGameSkill RuntimeSkill;
        public readonly AttackSource AttackSource;
        public readonly Transform Origin;
        public readonly float TimingScale;

        public SkillExecutionContext(
            IAttacker attacker,
            Health primaryTarget,
            SkillTypeSO skill,
            AttackSource attackSource,
            float timingScale = 1f,
            IGameSkill runtimeSkill = null)
        {
            Attacker = attacker;
            PrimaryTarget = primaryTarget;
            Skill = skill;
            RuntimeSkill = runtimeSkill;
            AttackSource = attackSource;
            Origin = attacker is Component component ? component.transform : null;
            TimingScale = Mathf.Max(0.01f, timingScale);
        }
    }
}
