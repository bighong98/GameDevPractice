using System;
using System.Collections.Generic;
using System.Globalization;
using TH.Resource;

namespace TH.UI
{
    public static class SkillTooltipContentBuilder
    {
        public static IReadOnlyList<string> BuildDescriptionSections(SkillTypeSO skill, SkillTooltipLabelMapSO labelMap = null)
        {
            if (skill == null)
                return Array.Empty<string>();

            return new[] { BuildCoreSection(skill, labelMap) };
        }

        private static string BuildCoreSection(SkillTypeSO skill, SkillTooltipLabelMapSO labelMap)
        {
            return string.Join("\n", new[]
            {
                $"{ResolveLabel(labelMap, TooltipLabelKeys.SkillRange, "Range")}: {FormatFloat(skill.Range)}",
                $"{ResolveLabel(labelMap, TooltipLabelKeys.SkillCooldown, "Cooldown")}: {FormatFloat(skill.Cooldown)}s",
                $"{ResolveLabel(labelMap, TooltipLabelKeys.SkillHitCount, "Hit Count")}: {skill.HitCount}",
                $"{ResolveLabel(labelMap, TooltipLabelKeys.SkillAttackCoefficient, "Attack Coef")}: {FormatFloat(skill.AttackCoefficient)}"
            });
        }

        private static string ResolveLabel(SkillTooltipLabelMapSO labelMap, string key, string fallback)
        {
            return labelMap != null
                ? labelMap.GetLabel(key, fallback)
                : fallback;
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
