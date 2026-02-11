using System;
using UnityEngine;

namespace TH.Combat
{
    [Serializable]
    public struct SkillTargetPolicy
    {
        [SerializeField] private SkillTargetGroup allowedGroups;
        [SerializeField] private bool allowSelf;
        [SerializeField] private bool includeDeadTargets;

        public SkillTargetGroup AllowedGroups => allowedGroups == SkillTargetGroup.None
            ? SkillTargetGroup.Enemy
            : allowedGroups;

        public bool AllowSelf => allowSelf;
        public bool IncludeDeadTargets => includeDeadTargets;

        public bool Allows(SkillTargetGroup group)
        {
            return (AllowedGroups & group) != 0;
        }

        public static SkillTargetPolicy EnemyOnlyDefault => new SkillTargetPolicy
        {
            allowedGroups = SkillTargetGroup.Enemy,
            allowSelf = false,
            includeDeadTargets = false,
        };
    }
}
