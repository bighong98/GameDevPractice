using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TH.Attribute.Stat;
using TH.Item;
using TH.Resource;

namespace TH.UI
{
    /// <summary>
    /// 툴팁 상세 수준 정의.
    /// Brief: 간략 정보, Detailed: 상세 정보.
    /// </summary>
    
public enum TooltipDetailLevel
    {
        /// <summary>간략 정보 (제한된 스탯/효과 표시)</summary>
        
Brief,
        /// <summary>상세 정보 (모든 스탯/효과 표시)</summary>
        
Detailed,
    }

    /// <summary>
    /// 툴팁 콘텐츠 데이터 구조체.
    /// 이름과 설명을 함께 전달.
    /// </summary>
    
public readonly struct ItemTooltipContent
    {
        /// <summary>아이템 이름</summary>
        
public readonly string Name;
        /// <summary>아이템 설명 (스탯, 효과 포함)</summary>
        
public readonly string Description;

        public ItemTooltipContent(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    /// <summary>
    /// 아이템 툴팁 콘텐츠 빌더 유틸리티 클래스.
    /// ItemTypeSO에서 설명, 스탯, 효과 정보를 추출하여 포맷팅.
    /// </summary>
    
public static class ItemTooltipContentBuilder
    {
        /// <summary>Brief 모드에서 표시할 최대 스탯 수</summary>
        
private const int BriefStatLimit = 2;
        /// <summary>Brief 모드에서 표시할 최대 효과 수</summary>
        
private const int BriefEffectLimit = 2;

        /// <summary>
        /// IGameItem에서 툴팁 콘텐츠 생성.
        /// </summary>
        /// <param name="item">대상 아이템</param>
        /// <param name="detailLevel">상세 수준</param>
        /// <returns>툴팁 콘텐츠 데이터</returns>
        
public static ItemTooltipContent Build(IGameItem item, TooltipDetailLevel detailLevel)
        {
            if (item?.GetItemInfo == null)
                return default;

            return Build(item.GetItemInfo, detailLevel);
        }

        /// <summary>
        /// ItemTypeSO에서 툴팁 콘텐츠 생성.
        /// </summary>
        /// <param name="itemInfo">아이템 정보 SO</param>
        /// <param name="detailLevel">상세 수준</param>
        /// <returns>툴팁 콘텐츠 데이터</returns>
        
public static ItemTooltipContent Build(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            if (itemInfo == null)
                return default;

            var description = BuildDescription(itemInfo, detailLevel);
            return new ItemTooltipContent(itemInfo.nameString, description);
        }

        /// <summary>
        /// 설명 문자열 생성.
        /// 섹션들을 줄바꿈으로 결합.
        /// </summary>
        /// <param name="itemInfo">아이템 정보 SO</param>
        /// <param name="detailLevel">상세 수준</param>
        /// <returns>포맷팅된 설명 문자열</returns>
        
private static string BuildDescription(ItemTypeSO itemInfo, TooltipDetailLevel detailLevel)
        {
            var sections = BuildDescriptionSections(itemInfo, detailLevel);
            return string.Join("\n\n", sections);
        }

        /// <summary>
        /// 텍스트를 섹션별로 분리.
        /// description, stats, effects로 구분.
        /// </summary>
        /// <param name="text">원본 텍스트</param>
        /// <param name="description">설명 출력</param>
        /// <param name="stats">스탯 출력</param>
        /// <param name="effects">효과 출력</param>
        
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

        /// <summary>
        /// 섹션 헤더를 기준으로 본문 추출 시도.
        /// </summary>
        /// <param name="section">섹션 텍스트</param>
        /// <param name="header">헤더 문자열 (Stats, Effects 등)</param>
        /// <param name="body">추출된 본문 출력</param>
        /// <returns>추출 성공 여부</returns>
        
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



        /// <summary>
        /// 장비 스탯 라인 목록 생성.
        /// EquipmentTypeSO의 equipmentStats에서 추출.
        /// </summary>
        /// <param name="itemInfo">아이템 정보 SO</param>
        /// <returns>스탯 라인 목록</returns>
        
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

        /// <summary>
        /// 아이템 효과 라인 목록 생성.
        /// itemUseEffects에서 추출.
        /// </summary>
        /// <param name="itemInfo">아이템 정보 SO</param>
        /// <param name="detailLevel">상세 수준</param>
        /// <returns>효과 라인 목록</returns>
        
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

        /// <summary>
        /// 효과 텍스트 결정.
        /// IItemEffectTooltipInfo 인터페이스 구현 시 해당 텍스트 사용.
        /// </summary>
        /// <param name="effect">효과 객체</param>
        /// <param name="detailLevel">상세 수준</param>
        /// <returns>효과 텍스트</returns>
        
private static string ResolveEffectText(ItemEffectBase effect, TooltipDetailLevel detailLevel)
        {
            if (effect is IItemEffectTooltipInfo info)
                return detailLevel == TooltipDetailLevel.Detailed
                    ? info.GetTooltipDetail()
                    : info.GetTooltipSummary();

            return effect.name;
        }

        /// <summary>
        /// 스탯 라인 포맷팅.
        /// 스탯 이름과 값을 결합.
        /// </summary>
        /// <param name="data">스탯 수정자 데이터</param>
        /// <returns>포맷팅된 스탯 라인</returns>
        
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

        /// <summary>
        /// 스탯 값 포맷팅.
        /// 퍼센트/절대값 표시 및 부호 처리.
        /// </summary>
        /// <param name="value">스탯 값</param>
        /// <param name="calculation">계산 타입 (Add/Multiply 등)</param>
        /// <returns>포맷팅된 값 문자열</returns>
        
private static string FormatStatValue(float value, StatModCalcType calculation)
        {
            bool isPercent = calculation != StatModCalcType.Add;
            float displayValue = isPercent ? value * 100f : value;
            string sign = displayValue >= 0 ? "+" : "-";
            string formatted = Math.Abs(displayValue).ToString("0.##", CultureInfo.InvariantCulture);
            return isPercent ? $"{sign}{formatted}%" : $"{sign}{formatted}";
        }

        /// <summary>
        /// 라인 목록을 지정 개수로 제한.
        /// </summary>
        /// <param name="lines">원본 라인 목록</param>
        /// <param name="limit">최대 개수</param>
        /// <returns>제한된 라인 목록</returns>
        
private static List<string> LimitLines(List<string> lines, int limit)
        {
            if (limit <= 0 || lines.Count <= limit)
                return lines;

            return lines.GetRange(0, limit);
        }

        /// <summary>
        /// 섹션 포맷팅.
        /// 타이틀과 라인들을 결합.
        /// </summary>
        /// <param name="title">섹션 타이틀</param>
        /// <param name="lines">라인 목록</param>
        /// <returns>포맷팅된 섹션 문자열</returns>
        
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
    

        /// <summary>
        /// 설명 섹션 목록 생성.
        /// 기본 설명, 스탯, 효과 섹션을 각각 구성.
        /// </summary>
        /// <param name="itemInfo">아이템 정보 SO</param>
        /// <param name="detailLevel">상세 수준</param>
        /// <returns>섹션 문자열 목록 (desc, stats, effects)</returns>
        
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
