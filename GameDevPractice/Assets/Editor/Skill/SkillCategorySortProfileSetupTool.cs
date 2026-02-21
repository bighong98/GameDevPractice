#if UNITY_EDITOR
using System;
using System.IO;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class SkillCategorySortProfileSetupTool
{
    private const string ProfileAssetPath = "Assets/Game/Scriptable Objects/UI/SkillCategorySortProfileSO.asset";
    private const string AddressKey = "SkillCategorySortProfileSO";
    private const string TargetGroupName = "Data Catalog";
    private const string WeaponSkillFolderPath = "Assets/Game/Item/Weapon/SkillTypeSO";

    [MenuItem("Tools/Skill/Setup/Configure Skill Category Sort Profile")]
    private static void Configure()
    {
        var profile = EnsureProfileAsset(out var createdProfile, out var initializedRules);
        int updatedSkillCount = ApplyWeaponDefaultCategoryToWeaponSkills();
        bool linkedAddressable = EnsureAddressableEntry(profile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[SkillCategorySortProfileSetupTool] Completed. " +
            $"CreatedProfile={createdProfile}, InitializedRules={initializedRules}, UpdatedWeaponSkills={updatedSkillCount}, AddressableLinked={linkedAddressable}, Address={AddressKey}");
    }

    private static SkillCategorySortProfileSO EnsureProfileAsset(out bool createdProfile, out bool initializedRules)
    {
        createdProfile = false;
        initializedRules = false;

        var profile = AssetDatabase.LoadAssetAtPath<SkillCategorySortProfileSO>(ProfileAssetPath);
        if (profile == null)
        {
            EnsureFolder(Path.GetDirectoryName(ProfileAssetPath)?.Replace("\\", "/"));
            profile = ScriptableObject.CreateInstance<SkillCategorySortProfileSO>();
            AssetDatabase.CreateAsset(profile, ProfileAssetPath);
            createdProfile = true;
        }

        if (profile == null)
            return null;

        var serializedObject = new SerializedObject(profile);
        var rulesProp = serializedObject.FindProperty("rules");
        if (rulesProp == null)
            return profile;

        if (rulesProp.arraySize <= 0)
        {
            rulesProp.arraySize = 3;
            SetRule(rulesProp, 0, SkillCategory.BasicSkill, 300);
            SetRule(rulesProp, 1, SkillCategory.AdditiveSkill, 200);
            SetRule(rulesProp, 2, SkillCategory.UltimateSkill, 100);

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            initializedRules = true;
        }

        return profile;
    }

    private static void SetRule(SerializedProperty rulesProp, int index, SkillCategory category, int priority)
    {
        var element = rulesProp.GetArrayElementAtIndex(index);
        var categoryProp = element.FindPropertyRelative("category");
        var priorityProp = element.FindPropertyRelative("priority");

        if (categoryProp != null)
            categoryProp.enumValueIndex = (int)category;
        if (priorityProp != null)
            priorityProp.intValue = priority;
    }

    private static int ApplyWeaponDefaultCategoryToWeaponSkills()
    {
        var guids = AssetDatabase.FindAssets("t:SkillTypeSO", new[] { WeaponSkillFolderPath });
        int updatedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var skill = AssetDatabase.LoadAssetAtPath<SkillTypeSO>(path);
            if (skill == null)
                continue;

            var serializedObject = new SerializedObject(skill);
            var categoryProp = serializedObject.FindProperty("skillCategory");
            if (categoryProp == null)
                continue;

            if (categoryProp.enumValueIndex == (int)SkillCategory.BasicSkill)
                continue;

            categoryProp.enumValueIndex = (int)SkillCategory.BasicSkill;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);
            updatedCount++;
        }

        return updatedCount;
    }

    private static bool EnsureAddressableEntry(SkillCategorySortProfileSO profile)
    {
        if (profile == null)
            return false;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[SkillCategorySortProfileSetupTool] Addressable settings not found.");
            return false;
        }

        settings.AddLabel("PreLoad_DataSO", false);
        settings.AddLabel("Scriptable Object", false);

        var targetGroup = settings.FindGroup(TargetGroupName) ?? settings.DefaultGroup;
        if (targetGroup == null)
        {
            Debug.LogError("[SkillCategorySortProfileSetupTool] Unable to resolve target Addressables group.");
            return false;
        }

        var guid = AssetDatabase.AssetPathToGUID(ProfileAssetPath);
        if (string.IsNullOrWhiteSpace(guid))
            return false;

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
            return false;

        entry.address = AddressKey;
        entry.SetLabel("PreLoad_DataSO", true);
        entry.SetLabel("Scriptable Object", true);

        EditorUtility.SetDirty(settings);
        return true;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
