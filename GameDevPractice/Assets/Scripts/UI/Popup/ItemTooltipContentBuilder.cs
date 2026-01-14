using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TH.Attribute.Stat;
using TH.Item;
using TH.Resource;

namespace TH.UI
{
    public enum TooltipDetailLevel
    {
        Brief,
        Detailed,
    }

    public readonly struct ItemTooltipContent
    {
        public readonly string Name;
        public readonly string Description;

        public ItemTooltipContent(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    public static class ItemTooltipContentBuilder
    {
        private const int BriefStatLimit = 2;
        private const int BriefEffectLimit = 2;

        public static ItemTooltipContent Build(IGameItem item, TooltipDetailLevel detailLevel)
        {
            if (item?.GetItemInfo == null)
                return default;

            return Build(item.GetItemInfo, detailLevel);
        }

        public static ItemTooltipContent Build(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            if (itemInfo == null)
                return default;

            var description = BuildDescription(itemInfo, detailLevel);
            return new ItemTooltipContent(itemInfo.nameString, description);
        }

        private static string BuildDescription(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            var sections = BuildDescriptionSections(itemInfo, detailLevel);
            return string.Join("\n\n", sections);
        }

        public static void SplitSections(string text, out string description, out string stats, out string effects)
        {
            description = string.Empty;
            stats = string.Empty;
            effects = string.Empty;

            if (string.IsNullOrWhiteSpace(text)) return;

            var sections = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawSection in sections)
            {
                var section = rawSection.Trim();
                if (TryExtractSection(section, "Stats", out var statBody))
                {
                    stats = statBody;
                    continue;
                }
                if (TryExtractSection(section, "Effects", out var effectBody))
                {
                    effects = effectBody;
                    continue;
                }

                if (description.Length > 0)
                    description += "\n\n";
                description += section;
            }
        }

        private static bool TryExtractSection(string section, string header, out string body)
        {
            body = string.Empty;
            if (!section.StartsWith(header, StringComparison.OrdinalIgnoreCase)) return false;

            body = section.Length == header.Length
                ? string.Empty
                : section.Substring(header.Length).TrimStart();
            body = body.TrimStart('\n', '\r');
            return true;
        }



        private static List<string> BuildEquipmentStatLines(ItemTypeSO itemInfo)
        {
            if (itemInfo is not EquipmentTypeSO equipment || equipment.equipmentStats == null)
                return new List<string>();

            var lines = new List<string>();
            foreach (var stat in equipment.equipmentStats)
            {
                var line = FormatStatLine(stat);
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }

            return lines;
        }

        private static List<string> BuildEffectLines(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            if (itemInfo?.itemUseEffects == null || itemInfo.itemUseEffects.Count == 0)
                return new List<string>();

            var lines = new List<string>();
            foreach (var effect in itemInfo.itemUseEffects)
            {
                if (effect == null) continue;

                var text = ResolveEffectText(effect, detailLevel);
                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(text);
            }

            return lines;
        }

        private static string ResolveEffectText(ItemEffectBase effect, TooltipDetailLevel detailLevel)
        {
            if (effect is IItemEffectTooltipInfo info)
                return detailLevel == TooltipDetailLevel.Detailed
                    ? info.GetTooltipDetail()
                    : info.GetTooltipSummary();

            return effect.name;
        }

        private static string FormatStatLine(StatModifierData data)
        {
            if (Math.Abs(data.value) < 0.0001f)
                return string.Empty;

            var statName = data.type.ToString();
            var valueText = FormatStatValue(data.value, data.calculation);

            if (string.IsNullOrWhiteSpace(statName) || string.IsNullOrWhiteSpace(valueText))
                return string.Empty;

            return $"{statName} {valueText}";
        }

        private static string FormatStatValue(float value, StatModCalcType calculation)
        {
            bool isPercent = calculation != StatModCalcType.Add;
            float displayValue = isPercent ? value * 100f : value;
            string sign = displayValue >= 0 ? "+" : "-";
            string formatted = Math.Abs(displayValue).ToString("0.##", CultureInfo.InvariantCulture);
            return isPercent ? $"{sign}{formatted}%" : $"{sign}{formatted}";
        }

        private static List<string> LimitLines(List<string> lines, int limit)
        {
            if (limit <= 0 || lines.Count <= limit)
                return lines;

            return lines.GetRange(0, limit);
        }

        private static string FormatSection(string title, List<string> lines)
        {
            var builder = new StringBuilder();
            builder.Append(title).Append('\n');
            for (int i = 0; i < lines.Count; i++)
            {
                builder.Append("- ").Append(lines[i]);
                if (i < lines.Count - 1)
                    builder.Append('\n');
            }

            return builder.ToString();
        }
    

        public static IReadOnlyList<string> BuildDescriptionSections(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            if (itemInfo == null)
                return Array.Empty<string>();

            var sections = new List<string>();

            if (!string.IsNullOrWhiteSpace(itemInfo.desc))
                sections.Add(itemInfo.desc.Trim());

            var statLines = BuildEquipmentStatLines(itemInfo);
            var effectLines = BuildEffectLines(itemInfo, detailLevel);

            if (detailLevel == TooltipDetailLevel.Brief)
            {
                statLines = LimitLines(statLines, BriefStatLimit);
                effectLines = LimitLines(effectLines, BriefEffectLimit);
            }

            if (statLines.Count > 0)
                sections.Add(FormatSection("Stats", statLines));
            if (effectLines.Count > 0)
                sections.Add(FormatSection("Effects", effectLines));

            return sections;
        }
}
}
