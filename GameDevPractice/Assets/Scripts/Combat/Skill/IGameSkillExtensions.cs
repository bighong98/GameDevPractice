namespace TH.Combat
{
    public static class IGameSkillExtensions
    {
        public static bool IsNull(this IGameSkill skill)
        {
            return skill == null || skill.Definition == null;
        }

        public static bool IsNotNull(this IGameSkill skill)
        {
            return skill != null && skill.Definition != null;
        }
    }
}
