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
        public readonly AttackSource AttackSource;
        public readonly Transform Origin;

        public SkillExecutionContext(IAttacker attacker, Health primaryTarget, SkillTypeSO skill, AttackSource attackSource)
        {
            Attacker = attacker;
            PrimaryTarget = primaryTarget;
            Skill = skill;
            AttackSource = attackSource;
            Origin = attacker is Component component ? component.transform : null;
        }
    }
}
