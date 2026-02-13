#if UNITY_EDITOR
using System;
using TH.Resource;

public static class OutfitAutoGenerationRules
{
    private static readonly char[] TokenSeparators = { '_', '-', ' ' };
    private static readonly string[] ExcludedTokens =
    {
        "hair",
        "eyebrow",
        "brow",
        "beard",
        "mustache",
        "moustache"
    };

    public static bool TryGetAutoOutfitSlotByPartName(string partName, out Enums.EquippedItemSlotType slot)
    {
        slot = Enums.EquippedItemSlotType.Max;
        if (string.IsNullOrWhiteSpace(partName))
            return false;

        if (IsExcludedOutfitIdentifier(partName))
            return false;

        if (partName.EndsWith("_Top", StringComparison.OrdinalIgnoreCase))
        {
            slot = Enums.EquippedItemSlotType.Body;
            return true;
        }

        if (partName.EndsWith("_Bottom", StringComparison.OrdinalIgnoreCase))
        {
            slot = Enums.EquippedItemSlotType.Foot;
            return true;
        }

        return false;
    }

    public static bool IsAllowedOutfitIdentifier(string identifier)
    {
        return !IsExcludedOutfitIdentifier(identifier);
    }

    public static bool IsExcludedOutfitIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return true;

        var tokens = identifier.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            for (int t = 0; t < ExcludedTokens.Length; t++)
            {
                if (string.Equals(token, ExcludedTokens[t], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    public static bool IsAutoGeneratableOutfitKey(OutfitKeySO outfitKey)
    {
        if (outfitKey == null || string.IsNullOrWhiteSpace(outfitKey.id))
            return false;

        if (!IsAllowedOutfitIdentifier(outfitKey.id))
            return false;

        return outfitKey.partType != null;
    }
}
#endif
