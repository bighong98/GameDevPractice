#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TH.Attribute.Stat;
using TH.Item;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class ArmorTypeTestDataGenerator
{
    private const string ArmorOutputDataAddressKey = "Armor Output Data";
    private const string OutfitDataAddressKey = "Outfit Data";
    private const string OutputIconsAddressKey = "Outfit Output Icons";
    private const string ItemDataGroupName = "Item Data";

    [MenuItem("Tools/Outfit/Generate Test ArmorTypeSO Data")]
    private static void GenerateTestArmorTypeData()
    {
        var armorOutputFolderPath = ResolvePathByAddressKey(ArmorOutputDataAddressKey);
        var outfitDataFolderPath = ResolvePathByAddressKey(OutfitDataAddressKey);
        var outputIconsFolderPath = ResolvePathByAddressKey(OutputIconsAddressKey);

        if (string.IsNullOrEmpty(armorOutputFolderPath) || string.IsNullOrEmpty(outfitDataFolderPath))
            return;

        EnsureFolder(armorOutputFolderPath);

        var statLookup = BuildGameStatLookup();
        var outfitKeys = LoadOutfitKeys(outfitDataFolderPath);
        if (outfitKeys.Count == 0)
        {
            Debug.LogWarning("[ArmorTypeTestDataGenerator] No OutfitKeySO found in Outfit Data folder.");
            return;
        }

        int created = 0;
        int updated = 0;
        int skipped = 0;

        var generatedAssets = new List<ArmorTypeSO>(outfitKeys.Count);
        var processedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < outfitKeys.Count; i++)
        {
            var outfitKey = outfitKeys[i];
            if (outfitKey == null || string.IsNullOrWhiteSpace(outfitKey.id))
            {
                skipped++;
                continue;
            }

            var slot = NormalizeSlot(outfitKey.partType != null
                ? outfitKey.partType.legacyEquipSlot
                : Enums.EquippedItemSlotType.Max);
            if (slot == Enums.EquippedItemSlotType.Max)
            {
                skipped++;
                continue;
            }

            if (!OutfitAutoGenerationRules.IsAutoGeneratableOutfitKey(outfitKey))
            {
                skipped++;
                continue;
            }

            var outputId = SanitizeFileName(outfitKey.id);
            if (string.IsNullOrWhiteSpace(outputId) || !processedIds.Add(outputId))
            {
                skipped++;
                continue;
            }

            var assetName = $"Armor_{outputId}";
            var assetPath = $"{armorOutputFolderPath}/{assetName}.asset";

            var armor = AssetDatabase.LoadAssetAtPath<ArmorTypeSO>(assetPath);
            bool isNew = armor == null;
            if (isNew)
            {
                armor = ScriptableObject.CreateInstance<ArmorTypeSO>();
                AssetDatabase.CreateAsset(armor, assetPath);
                created++;
            }
            else
            {
                updated++;
            }

            PopulateArmorData(armor, outfitKey, slot, outputIconsFolderPath, statLookup);
            EditorUtility.SetDirty(armor);
            generatedAssets.Add(armor);
        }

        EnsureAddressablesEntries(generatedAssets);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ArmorTypeTestDataGenerator] Completed. Generated={generatedAssets.Count}, Created={created}, Updated={updated}, Skipped={skipped}, OutputFolder={armorOutputFolderPath}");
    }

    private static void PopulateArmorData(
        ArmorTypeSO armor,
        OutfitKeySO outfitKey,
        Enums.EquippedItemSlotType slot,
        string outputIconsFolderPath,
        Dictionary<string, GameStatSO> statLookup)
    {
        if (armor == null || outfitKey == null)
            return;

        var seededValue = GetStableHash(outfitKey.id);
        var partName = ResolvePartName(slot, outfitKey.id);
        var themeName = ResolveThemeName(outfitKey.id);
        var materialName = ResolveMaterialName(slot, seededValue);

        armor.nameString = BuildNaturalName(themeName, materialName, partName);
        armor.desc = BuildDescription(slot, themeName, partName, outfitKey.id);

        armor.itemType = Enums.ItemType.Equipment;
        armor.maxAmount = 1;
        armor.equipmentType = Enums.EquipmentType.Armor;
        armor.slotType = slot;

        armor.itemUseEffects ??= new List<ItemEffectBase>();
        armor.equipmentStats = BuildStatModifiers(slot, seededValue, statLookup);

        var iconSprite = LoadOutfitIconSprite(outputIconsFolderPath, outfitKey.id);
        if (iconSprite != null)
            armor.sprite = iconSprite;

        AssignPrivateArmorFields(armor, outfitKey);
    }

    private static void AssignPrivateArmorFields(ArmorTypeSO armor, OutfitKeySO outfitKey)
    {
        var so = new SerializedObject(armor);

        var outfitKeyProp = so.FindProperty("outfitKeySO");
        if (outfitKeyProp != null)
            outfitKeyProp.objectReferenceValue = outfitKey;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static List<StatModifierData> BuildStatModifiers(
        Enums.EquippedItemSlotType slot,
        int seededValue,
        Dictionary<string, GameStatSO> statLookup)
    {
        var modifiers = new List<StatModifierData>(6);

        AddStat(modifiers, statLookup, 5f + (seededValue % 3), "Armor");
        AddStat(modifiers, statLookup, 2f + (seededValue % 2), "MagicResistance", "Magic Resist", "MR");

        switch (slot)
        {
            case Enums.EquippedItemSlotType.Head:
                AddStat(modifiers, statLookup, 2f + (seededValue % 2), "Intelligence", "Int");
                AddStat(modifiers, statLookup, 4f + (seededValue % 4), "Mana", "MP");
                break;

            case Enums.EquippedItemSlotType.Body:
                AddStat(modifiers, statLookup, 14f + (seededValue % 7), "Health", "HP");
                AddStat(modifiers, statLookup, 2f + (seededValue % 3), "Strength", "Str");
                break;

            case Enums.EquippedItemSlotType.Foot:
                AddStat(modifiers, statLookup, 0.35f + ((seededValue % 3) * 0.1f), "MoveSpeed", "MovementSpeed");
                AddStat(modifiers, statLookup, 2f + (seededValue % 3), "Agility", "Agi");
                break;

            case Enums.EquippedItemSlotType.Hand:
                AddStat(modifiers, statLookup, 2f + (seededValue % 2), "Agility", "Agi");
                AddStat(modifiers, statLookup, 2f + (seededValue % 3), "AD", "Attack");
                break;
        }

        return modifiers;
    }

    private static void AddStat(List<StatModifierData> target, Dictionary<string, GameStatSO> statLookup, float value, params string[] names)
    {
        var stat = FindStat(statLookup, names);
        if (stat == null)
            return;

        target.Add(new StatModifierData
        {
            type = stat,
            calculation = StatModCalcType.Add,
            value = value
        });
    }

    private static GameStatSO FindStat(Dictionary<string, GameStatSO> statLookup, params string[] names)
    {
        if (statLookup == null || names == null)
            return null;

        for (int i = 0; i < names.Length; i++)
        {
            var name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (statLookup.TryGetValue(name, out var stat) && stat != null)
                return stat;
        }

        return null;
    }

    private static Dictionary<string, GameStatSO> BuildGameStatLookup()
    {
        var lookup = new Dictionary<string, GameStatSO>(StringComparer.OrdinalIgnoreCase);
        var guids = AssetDatabase.FindAssets("t:GameStatSO", new[] { "Assets/Game/Attributes" });

        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var stat = AssetDatabase.LoadAssetAtPath<GameStatSO>(path);
            if (stat == null)
                continue;

            AddStatLookupKey(lookup, stat.name, stat);
            AddStatLookupKey(lookup, stat.DisplayName, stat);
            AddStatLookupKey(lookup, Path.GetFileNameWithoutExtension(path), stat);
        }

        return lookup;
    }

    private static void AddStatLookupKey(Dictionary<string, GameStatSO> lookup, string key, GameStatSO stat)
    {
        if (lookup == null || stat == null || string.IsNullOrWhiteSpace(key))
            return;

        lookup[key] = stat;
    }

    private static List<OutfitKeySO> LoadOutfitKeys(string outfitDataFolderPath)
    {
        var result = new List<OutfitKeySO>();
        var guids = AssetDatabase.FindAssets("t:OutfitKeySO", new[] { outfitDataFolderPath });

        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var key = AssetDatabase.LoadAssetAtPath<OutfitKeySO>(path);
            if (key != null)
                result.Add(key);
        }

        result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
        return result;
    }

    private static Enums.EquippedItemSlotType NormalizeSlot(Enums.EquippedItemSlotType slot)
    {
        return slot switch
        {
            Enums.EquippedItemSlotType.Head => Enums.EquippedItemSlotType.Head,
            Enums.EquippedItemSlotType.Body => Enums.EquippedItemSlotType.Body,
            Enums.EquippedItemSlotType.Hand => Enums.EquippedItemSlotType.Hand,
            Enums.EquippedItemSlotType.Foot => Enums.EquippedItemSlotType.Foot,
            _ => Enums.EquippedItemSlotType.Max
        };
    }

    private static string BuildNaturalName(string themeName, string materialName, string partName)
    {
        var possessiveTheme = MakePossessive(themeName);
        return $"{possessiveTheme} {materialName} {partName}";
    }

    private static string BuildDescription(Enums.EquippedItemSlotType slot, string themeName, string partName, string outfitId)
    {
        var baseSentence = $"A test {partName.ToLowerInvariant()} tuned for the {themeName} outfit line (key: {outfitId}).";

        return slot switch
        {
            Enums.EquippedItemSlotType.Foot => baseSentence + " Adds Armor and MagicResistance while improving MoveSpeed and Agility.",
            Enums.EquippedItemSlotType.Body => baseSentence + " Focuses on Armor and MagicResistance with extra Health and Strength.",
            Enums.EquippedItemSlotType.Head => baseSentence + " Improves Armor and MagicResistance with mana-oriented support stats.",
            Enums.EquippedItemSlotType.Hand => baseSentence + " Balances Armor and MagicResistance with light offensive utility.",
            _ => baseSentence + " Provides balanced defensive bonuses."
        };
    }

    private static string ResolvePartName(Enums.EquippedItemSlotType slot, string outfitId)
    {
        if (string.IsNullOrWhiteSpace(outfitId))
            return slot switch
            {
                Enums.EquippedItemSlotType.Head => "Headgear",
                Enums.EquippedItemSlotType.Body => "Cuirass",
                Enums.EquippedItemSlotType.Hand => "Gauntlets",
                Enums.EquippedItemSlotType.Foot => "Boots",
                _ => "Armor"
            };

        var lower = outfitId.ToLowerInvariant();

        if (slot == Enums.EquippedItemSlotType.Head)
        {
            if (lower.Contains("eyepatch")) return "Eyepatch";
            if (lower.Contains("headband")) return "Headband";
            if (lower.Contains("hood")) return "Hood";
            if (lower.Contains("helm")) return "Helm";
            if (lower.Contains("hat")) return "Hat";
            return "Headgear";
        }

        if (slot == Enums.EquippedItemSlotType.Body)
        {
            if (lower.Contains("mage") || lower.Contains("sorcerer") || lower.Contains("witch") || lower.Contains("warlock"))
                return "Robe";

            return "Cuirass";
        }

        if (slot == Enums.EquippedItemSlotType.Foot)
        {
            return lower.Contains("bottom") ? "Greaves" : "Boots";
        }

        if (slot == Enums.EquippedItemSlotType.Hand)
            return "Gauntlets";

        return "Armor";
    }

    private static string ResolveThemeName(string outfitId)
    {
        if (string.IsNullOrWhiteSpace(outfitId))
            return "Adventurer";

        var tokens = outfitId.Split(new[] { '_', '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (string.Equals(token, "M", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "F", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Top", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(token, "Bottom", StringComparison.OrdinalIgnoreCase))
                continue;

            return token;
        }

        return "Adventurer";
    }

    private static string ResolveMaterialName(Enums.EquippedItemSlotType slot, int seededValue)
    {
        var materials = slot switch
        {
            Enums.EquippedItemSlotType.Head => new[] { "Iron", "Steel", "Leather", "Runed" },
            Enums.EquippedItemSlotType.Body => new[] { "Plated", "Steel", "Runed", "Reinforced" },
            Enums.EquippedItemSlotType.Hand => new[] { "Leather", "Steel", "Guarded", "Runed" },
            Enums.EquippedItemSlotType.Foot => new[] { "Leather", "Swift", "Reinforced", "Traveler's" },
            _ => new[] { "Basic" }
        };

        return materials[seededValue % materials.Length];
    }

    private static string MakePossessive(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Adventurer's";

        return name.EndsWith("s", StringComparison.OrdinalIgnoreCase)
            ? $"{name}'"
            : $"{name}'s";
    }

    private static Sprite LoadOutfitIconSprite(string outputIconsFolderPath, string outfitId)
    {
        if (string.IsNullOrEmpty(outputIconsFolderPath) || string.IsNullOrWhiteSpace(outfitId))
            return null;

        var sanitized = SanitizeFileName(outfitId);
        var directPath = $"{outputIconsFolderPath}/{sanitized}.png";

        var direct = AssetDatabase.LoadAssetAtPath<Sprite>(directPath);
        if (direct != null)
            return direct;

        var guids = AssetDatabase.FindAssets($"t:Sprite {sanitized}", new[] { outputIconsFolderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            var spritePath = AssetDatabase.GUIDToAssetPath(guids[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null)
                return sprite;
        }

        return null;
    }

    private static void EnsureAddressablesEntries(IReadOnlyList<ArmorTypeSO> generatedAssets)
    {
        if (generatedAssets == null || generatedAssets.Count == 0)
            return;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[ArmorTypeTestDataGenerator] Addressable settings not found.");
            return;
        }

        var targetGroup = settings.FindGroup(ItemDataGroupName) ?? settings.DefaultGroup;
        if (targetGroup == null)
        {
            Debug.LogError("[ArmorTypeTestDataGenerator] Unable to resolve target Addressables group.");
            return;
        }

        for (int i = 0; i < generatedAssets.Count; i++)
        {
            var armor = generatedAssets[i];
            if (armor == null)
                continue;

            var path = AssetDatabase.GetAssetPath(armor);
            if (string.IsNullOrEmpty(path))
                continue;

            var guid = AssetDatabase.AssetPathToGUID(path);
            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, targetGroup, false, true);
            }
            else if (entry.parentGroup != targetGroup)
            {
                settings.MoveEntry(entry, targetGroup, false, true);
            }

            if (entry == null)
                continue;

            entry.address = Path.GetFileNameWithoutExtension(path);
            entry.SetLabel("PreLoad_DataSO", true);
            entry.SetLabel("Scriptable Object", true);
        }
    }

    private static string ResolvePathByAddressKey(string addressKey)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
            return null;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError($"[ArmorTypeTestDataGenerator] Addressable settings not found. key={addressKey}");
            return null;
        }

        for (int g = 0; g < settings.groups.Count; g++)
        {
            var group = settings.groups[g];
            if (group == null)
                continue;

            foreach (var entry in group.entries)
            {
                if (entry == null)
                    continue;

                if (!string.Equals(entry.address, addressKey, StringComparison.Ordinal))
                    continue;

                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.IsNullOrEmpty(path))
                    return path;
            }
        }

        Debug.LogError($"[ArmorTypeTestDataGenerator] Addressable key not found: {addressKey}");
        return null;
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var chars = raw.ToCharArray();
        var invalid = Path.GetInvalidFileNameChars();

        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }

        return new string(chars).Trim();
    }

    private static int GetStableHash(string text)
    {
        unchecked
        {
            int hash = 17;
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i++)
                {
                    hash = hash * 31 + char.ToUpperInvariant(text[i]);
                }
            }

            return hash & 0x7fffffff;
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
