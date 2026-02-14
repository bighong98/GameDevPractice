namespace TH.UI
{
    public static class TooltipLabelKeys
    {
        public const string ItemSectionStats = "item.section.stats";
        public const string ItemSectionEffects = "item.section.effects";
        public const string ItemTypeCountableUsable = "item.type.Countable.Usable";
        public const string ItemTypeCountableResource = "item.type.Countable.Resource";

        public const string SkillType = "skill.type";
        public const string SkillRange = "skill.range";
        public const string SkillCooldown = "skill.cooldown";
        public const string SkillHitCount = "skill.hit_count";
        public const string SkillAttackCoefficient = "skill.attack_coef";
        public const string SkillDamagePerHit = "skill.damage_per_hit";

        public static string ItemType(Enums.ItemType itemType)
        {
            return $"item.type.{itemType}";
        }

        public static string ItemType(Enums.ItemType itemType, bool isUsable)
        {
            if (itemType == Enums.ItemType.Countable)
                return isUsable ? ItemTypeCountableUsable : ItemTypeCountableResource;

            return ItemType(itemType);
        }

        public static string ItemEffect(string effectTypeName)
        {
            if (string.IsNullOrWhiteSpace(effectTypeName))
                return "item.effect.unknown";

            return $"item.effect.{effectTypeName}";
        }

        public static string ItemEffect(string effectTypeName, TooltipDetailLevel detailLevel)
        {
            string suffix = detailLevel == TooltipDetailLevel.Detailed
                ? "detail"
                : "summary";

            return $"{ItemEffect(effectTypeName)}.{suffix}";
        }
    }
}
