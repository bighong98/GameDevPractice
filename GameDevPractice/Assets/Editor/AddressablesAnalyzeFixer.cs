#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class AddressablesAnalyzeFixer
{
    private const string AnalyzePath = "AddressablesAnalyze/AddressablesAnalyseResults.json";

    [MenuItem("Tools/Addressables/Fix Duplicate Dependencies (Analyze Export)")]
    public static void FixDuplicateDependencies()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[Addressables] Settings not found. Open Addressables Groups window once and try again.");
            return;
        }

        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), AnalyzePath);
        if (!File.Exists(fullPath))
        {
            Debug.LogError($"[Addressables] Analyze export not found: {fullPath}");
            return;
        }

        var json = File.ReadAllText(fullPath);
        var data = JsonUtility.FromJson<AnalyzeResults>(json);
        if (data?.m_RuleToResults == null)
        {
            Debug.LogError("[Addressables] Failed to parse analyze results.");
            return;
        }

        var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in data.m_RuleToResults)
        {
            if (!string.Equals(rule.RuleName, "Check Duplicate Bundle Dependencies", StringComparison.Ordinal))
                continue;

            if (rule.Results == null) continue;
            foreach (var r in rule.Results)
            {
                if (string.IsNullOrEmpty(r.m_ResultName)) continue;
                var parts = r.m_ResultName.Split(new[] { ':' }, 3);
                var assetPath = parts.Length == 3 ? parts[2] : r.m_ResultName;
                if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                    assets.Add(assetPath);
            }
        }

        if (assets.Count == 0)
        {
            Debug.Log("[Addressables] No duplicate dependencies found in analyze export.");
            return;
        }

        var sharedGroup = settings.FindGroup("Shared");
        if (sharedGroup == null)
        {
            sharedGroup = settings.CreateGroup("Shared", false, false, false, null);
            sharedGroup.AddSchema<BundledAssetGroupSchema>();
            sharedGroup.AddSchema<ContentUpdateGroupSchema>();
        }

        int created = 0;
        int moved = 0;
        int skipped = 0;

        foreach (var assetPath in assets)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                skipped++;
                continue;
            }

            var entry = settings.FindAssetEntry(guid);
            if (entry == null)
            {
                entry = settings.CreateOrMoveEntry(guid, sharedGroup, false, false);
                entry.address = assetPath;
                created++;
            }
            else if (entry.parentGroup != sharedGroup)
            {
                settings.MoveEntry(entry, sharedGroup);
                moved++;
            }
        }

        settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, sharedGroup, true, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Addressables] Shared group updated. Created: {created}, Moved: {moved}, Skipped: {skipped}.");
    }

    [Serializable]
    private class AnalyzeResults
    {
        public List<RuleResults> m_RuleToResults;
    }

    [Serializable]
    private class RuleResults
    {
        public string RuleName;
        public List<RuleResult> Results;
    }

    [Serializable]
    private class RuleResult
    {
        public string m_ResultName;
        public int m_Severity;
    }
}
#endif
