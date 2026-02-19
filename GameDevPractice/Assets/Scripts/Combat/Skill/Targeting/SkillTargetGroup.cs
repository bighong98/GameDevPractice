using System;

namespace TH.Combat
{
    [Flags]
    public enum SkillTargetGroup
    {
        None = 0,
        Ally = 1 << 0,
        Enemy = 1 << 1,
        Neutral = 1 << 2,
        Object = 1 << 3,
        All = Ally | Enemy | Neutral | Object,
    }
}
