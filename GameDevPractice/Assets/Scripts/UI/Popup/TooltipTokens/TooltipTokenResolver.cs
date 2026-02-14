using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TH.Item;
using TH.Resource;
using UnityEngine;

namespace TH.UI
{
    public static class TooltipTokenResolver
    {
        private static readonly Regex TokenRegex = new(@"\{(?<key>[a-zA-Z0-9._-]+)(:(?<format>[^{}]+))?\}", RegexOptions.Compiled);
        private static readonly ItemTooltipBaseTokenProvider BaseTokenProvider = new();

        public static string ResolveDescription(string template, ItemTypeSO itemInfo, TooltipDetailLevel detailLevel, IGameItem runtimeItem = null)
        {
            if (string.IsNullOrEmpty(template) || itemInfo == null)
                return template ?? string.Empty;

            if (template.IndexOf('{') < 0)
                return template;

            var context = new TooltipTokenContext(itemInfo, runtimeItem, detailLevel);
            var tokens = CollectTokens(context);
            if (tokens.Count == 0)
                return template;

            return TokenRegex.Replace(template, match =>
            {
                var key = match.Groups["key"].Value;
                if (!tokens.TryGetValue(key, out var value))
                    return match.Value;

                var format = match.Groups["format"].Success ? match.Groups["format"].Value : null;
                return value.ToDisplayString(format);
            });
        }

        public static bool SetToken(IDictionary<string, TooltipTokenValue> tokens, string key, TooltipTokenValue value, bool overwrite = true)
        {
            if (tokens == null || string.IsNullOrWhiteSpace(key))
                return false;

            if (!overwrite && tokens.ContainsKey(key))
                return false;

            tokens[key] = value;
            return true;
        }

        private static Dictionary<string, TooltipTokenValue> CollectTokens(in TooltipTokenContext context)
        {
            var tokens = new Dictionary<string, TooltipTokenValue>(StringComparer.OrdinalIgnoreCase);

            SafeCollect(BaseTokenProvider, context, tokens);
            if (context.ItemInfo is ITooltipTokenProvider itemProvider)
                SafeCollect(itemProvider, context, tokens);

            var effects = context.ItemInfo?.itemUseEffects;
            if (effects == null)
                return tokens;

            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] is ITooltipTokenProvider effectProvider)
                    SafeCollect(effectProvider, context, tokens);
            }

            return tokens;
        }

        private static void SafeCollect(ITooltipTokenProvider provider, in TooltipTokenContext context, IDictionary<string, TooltipTokenValue> tokens)
        {
            if (provider == null)
                return;

            try
            {
                provider.CollectTokens(context, tokens);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[TooltipTokenResolver] Failed to collect tokens from {provider.GetType().Name}. Error={e.Message}");
            }
        }

        private sealed class ItemTooltipBaseTokenProvider : ITooltipTokenProvider
        {
            public void CollectTokens(in TooltipTokenContext context, IDictionary<string, TooltipTokenValue> tokens)
            {
                if (context.ItemInfo == null)
                    return;

                var itemInfo = context.ItemInfo;
                SetToken(tokens, "item.name", itemInfo.nameString ?? string.Empty);
                SetToken(tokens, "item.type", itemInfo.itemType.ToString());
                SetToken(tokens, "item.maxAmount", itemInfo.maxAmount);
                SetToken(tokens, "item.isUsable", itemInfo.isUsable);
            }
        }
    }
}
