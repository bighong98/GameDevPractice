using System;
using System.Collections.Generic;
using System.Globalization;
using TH.Resource;
using UnityEngine;

namespace TH.UI
{
    public static class SkillTooltipContentBuilder
    {
        public static IReadOnlyList<string> BuildDescriptionSections(SkillTypeSO skill, float sourceDamage, SkillTooltipLabelMapSO labelMap = null)
        {
            if (skill == null)
                return Array.Empty<string>();

            return new[] { BuildCoreSection(skill, sourceDamage, labelMap) };
        }

        private static string BuildCoreSection(SkillTypeSO skill, float sourceDamage, SkillTooltipLabelMapSO labelMap)
        {
            float perHitDamage = Mathf.Max(0f, sourceDamage * skill.AttackCoefficient);
            return string.Join("\n", new[]
            {
                $"{ResolveLabel(labelMap, TooltipLabelKeys.SkillRange, "Range")}: {FormatFloat(skill.Range)}",
                $"{ResolveLabel(labelMap, TooltipLabelKeys.SkillCooldown, "Cooldown")}: {FormatFloat(skill.Cooldown)}s",
                $"{ResolveLabel(labelMap, TooltipLabelKeys.SkillHitCount, "Hit Count")}: {skill.HitCount}",
                $"{ResolveLabel(labelMap, TooltipLabelKeys.SkillDamagePerHit, "Damage/Hit")}: {FormatFloat(perHitDamage)}"
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
