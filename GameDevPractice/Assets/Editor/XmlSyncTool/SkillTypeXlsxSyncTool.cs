#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using TH.Combat;
using TH.Resource;
using UnityEditor;
using UnityEngine;

public sealed class SkillTypeXlsxSyncWindow : EditorWindow
{
    private const string WindowTitle = "SkillType XLSX Sync";
    private const string MenuPath = "Tools/Skill/Data Sync/SkillTypeSO XLSX Sync";

    private const string PrefXlsxPath = "TH.SkillTypeXlsxSync.XlsxPath";
    private const string PrefCreateMissing = "TH.SkillTypeXlsxSync.CreateMissing";
    private const string PrefApplySerializedJson = "TH.SkillTypeXlsxSync.ApplySerializedJson";
    private const string PrefIncludeSerializedJsonOnExport = "TH.SkillTypeXlsxSync.IncludeSerializedJsonOnExport";
    private const string PrefImportPathMapAssetPath = "TH.SkillTypeXlsxSync.ImportPathMapAssetPath";

    [SerializeField] private string xlsxPath = "";
    [SerializeField] private bool createMissingAssets = true;
    [SerializeField] private bool applySerializedJsonOnImport = true;
    [SerializeField] private bool includeSerializedJsonOnExport = true;
    [SerializeField] private string importPathMapAssetPath = "";

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        var window = GetWindow<SkillTypeXlsxSyncWindow>(WindowTitle);
        window.minSize = new Vector2(560f, 240f);
        window.Show();
    }

    private void OnEnable()
    {
        xlsxPath = EditorPrefs.GetString(PrefXlsxPath, SkillTypeXlsxSyncTool.GetDefaultXlsxFilePath());
        if (string.IsNullOrWhiteSpace(xlsxPath))
        {
            xlsxPath = SkillTypeXlsxSyncTool.GetDefaultXlsxFilePath();
        }

        createMissingAssets = EditorPrefs.GetBool(PrefCreateMissing, true);
        applySerializedJsonOnImport = EditorPrefs.GetBool(PrefApplySerializedJson, true);
        includeSerializedJsonOnExport = EditorPrefs.GetBool(PrefIncludeSerializedJsonOnExport, true);

        importPathMapAssetPath = EditorPrefs.GetString(PrefImportPathMapAssetPath, SkillTypeXlsxSyncTool.GetDefaultImportPathMapAssetPath());
        if (string.IsNullOrWhiteSpace(importPathMapAssetPath))
        {
            importPathMapAssetPath = SkillTypeXlsxSyncTool.GetDefaultImportPathMapAssetPath();
        }
    }

    private void OnDisable()
    {
        EditorPrefs.SetString(PrefXlsxPath, xlsxPath ?? string.Empty);
        EditorPrefs.SetBool(PrefCreateMissing, createMissingAssets);
        EditorPrefs.SetBool(PrefApplySerializedJson, applySerializedJsonOnImport);
        EditorPrefs.SetBool(PrefIncludeSerializedJsonOnExport, includeSerializedJsonOnExport);
        EditorPrefs.SetString(PrefImportPathMapAssetPath, importPathMapAssetPath ?? string.Empty);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("SkillTypeSO XLSX Sync", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        EditorGUILayout.BeginHorizontal();
        xlsxPath = EditorGUILayout.TextField("XLSX Path", xlsxPath);

        if (GUILayout.Button("Browse", GUILayout.Width(76f)))
        {
            var selected = EditorUtility.OpenFilePanel("Select SkillType XLSX", ResolveDefaultDirectory(), "xlsx");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                xlsxPath = NormalizePath(selected);
                GUI.FocusControl(null);
            }
        }

        if (GUILayout.Button("Save As", GUILayout.Width(76f)))
        {
            var defaultDir = ResolveDefaultDirectory();
            var defaultName = Path.GetFileNameWithoutExtension(SkillTypeXlsxSyncTool.GetDefaultXlsxFilePath());
            var selected = EditorUtility.SaveFilePanel("Save SkillType XLSX", defaultDir, defaultName, "xlsx");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                xlsxPath = NormalizePath(selected);
                GUI.FocusControl(null);
            }
        }

        if (GUILayout.Button("Use Default", GUILayout.Width(96f)))
        {
            xlsxPath = SkillTypeXlsxSyncTool.GetDefaultXlsxFilePath();
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();

        createMissingAssets = EditorGUILayout.ToggleLeft("Create missing assets on import", createMissingAssets);
        applySerializedJsonOnImport = EditorGUILayout.ToggleLeft("Apply serialized JSON payload on import", applySerializedJsonOnImport);
        includeSerializedJsonOnExport = EditorGUILayout.ToggleLeft("Include serialized JSON payload on export", includeSerializedJsonOnExport);

        var currentImportPathMap = SkillTypeXlsxSyncTool.LoadImportPathMapAsset(importPathMapAssetPath);
        var selectedImportPathMap = (SkillTypeImportPathMapSO)EditorGUILayout.ObjectField(
            "Import Path Map",
            currentImportPathMap,
            typeof(SkillTypeImportPathMapSO),
            false);
        if (selectedImportPathMap != currentImportPathMap)
        {
            var selectedPath = selectedImportPathMap == null
                ? SkillTypeXlsxSyncTool.GetDefaultImportPathMapAssetPath()
                : AssetDatabase.GetAssetPath(selectedImportPathMap);
            importPathMapAssetPath = NormalizePath(selectedPath);
        }
        var mapForResolve = selectedImportPathMap != null ? selectedImportPathMap : currentImportPathMap;
        var resolvedAddressKey = SkillTypeXlsxSyncTool.ResolveSkillDataFolderAddressableKey(mapForResolve);

        var resolvedFolder = SkillTypeXlsxSyncTool.ResolveFolderByAddressKey(resolvedAddressKey, logOnError: false);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Resolved Skill Folder", resolvedFolder ?? "(unresolved)");
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Resolved Skill Folder Key", string.IsNullOrWhiteSpace(resolvedAddressKey) ? "(unresolved)" : resolvedAddressKey);
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Default XLSX Path", SkillTypeXlsxSyncTool.GetDefaultXlsxFilePath());
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Default Import Map Path", SkillTypeXlsxSyncTool.GetDefaultImportPathMapAssetPath());
        }

        EditorGUILayout.Space(10f);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(resolvedAddressKey) || string.IsNullOrWhiteSpace(xlsxPath)))
            {
                if (GUILayout.Button("Export SO -> XLSX", GUILayout.Height(28f)))
                {
                    SkillTypeXlsxSyncTool.ExportToXlsx(resolvedAddressKey, xlsxPath, includeSerializedJsonOnExport);
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(resolvedAddressKey) || string.IsNullOrWhiteSpace(xlsxPath)))
            {
                if (GUILayout.Button("Import XLSX -> SO", GUILayout.Height(28f)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Import XLSX -> SkillTypeSO",
                        "XLSX data to SkillTypeSO",
                        "Import",
                        "Cancel"))
                    {
                        SkillTypeXlsxSyncTool.ImportFromXlsx(
                            resolvedAddressKey,
                            xlsxPath,
                            createMissingAssets,
                            applySerializedJsonOnImport,
                            importPathMapAssetPath);
                    }
                }
            }
        }
    }

    private static string ResolveDefaultDirectory()
    {
        return SkillTypeXlsxSyncTool.GetDefaultXlsxFolderPath();
    }

    private static string NormalizePath(string rawPath)
    {
        return string.IsNullOrWhiteSpace(rawPath) ? string.Empty : rawPath.Replace("\\", "/");
    }
}

public static class SkillTypeXlsxSyncTool
{
    private const string DefaultAddressKey = "Skill Data Folder";

    private const string DefaultXlsxRelativeFolder = "Resources/Data Table";
    private const string DefaultXlsxFileName = "SkillTypeTable.xlsx";
    private const string DefaultSheetName = "SkillType";
    private const string DefaultImportPathMapAssetPath = "Assets/Editor/Scriptable Object/SkillTypeImportPathMap.asset";

    private static readonly string[] ColumnHeaders =
    {
        "skillId",
        "damageType",
        "damageTypeValue",
        "baseDamage",
        "hitCount",
        "attackCoefficient",
        "range",
        "cooldown",
        "targetPolicy",
        "targetPolicyValue",
        "assetAddressableKey",
        "assetPath",
        "dataTypeName",
        "attackSourceStatName",
        "comboSequenceName",
        "executionProfileName",
        "animatorOverrideName",
        "projectilePrefabName",
        "skillVfxPrefabName",
        "onHitVfxPrefabName",
        "castSfxName",
        "skillSlotImageName",
        "serializedJson"
    };

    private static readonly Type DamageTypeEnumType = typeof(SkillTypeSO)
        .GetField("damageType", BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType;

    private static readonly Type TargetPolicyGroupEnumType = typeof(SkillTypeSO)
        .GetField("targetPolicy", BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType
        .GetField("allowedGroups", BindingFlags.NonPublic | BindingFlags.Instance)?.FieldType;

    [MenuItem("Tools/Skill/Data Sync/Export SkillTypeSO XLSX (Default Key)")]
    private static void ExportDefault()
    {
        var importPathMap = LoadImportPathMapAsset(GetDefaultImportPathMapAssetPath());
        var addressKey = ResolveSkillDataFolderAddressableKey(importPathMap);
        ExportToXlsx(string.IsNullOrWhiteSpace(addressKey) ? DefaultAddressKey : addressKey, GetDefaultXlsxFilePath(), includeSerializedJson: true);
    }

    [MenuItem("Tools/Skill/Data Sync/Import SkillTypeSO XLSX (Default Key)")]
    private static void ImportDefault()
    {
        var inputPath = GetDefaultXlsxFilePath();
        if (!File.Exists(inputPath))
        {
            Debug.LogError($"[SkillTypeXlsxSyncTool] XLSX file not found: {inputPath}");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Import XLSX -> SkillTypeSO",
                "XLSX data to SkillTypeSO",
                "Import",
                "Cancel"))
        {
            return;
        }

        var importPathMap = LoadImportPathMapAsset(GetDefaultImportPathMapAssetPath());
        var addressKey = ResolveSkillDataFolderAddressableKey(importPathMap);

        ImportFromXlsx(
            string.IsNullOrWhiteSpace(addressKey) ? DefaultAddressKey : addressKey,
            inputPath,
            createMissingAssets: true,
            applySerializedJson: true,
            importPathMapAssetPath: GetDefaultImportPathMapAssetPath());
    }

    public static string ResolveSkillDataFolderAddressableKey(SkillTypeImportPathMapSO importPathMap)
    {
        if (importPathMap == null || string.IsNullOrWhiteSpace(importPathMap.skillDataFolderAddressableKey))
        {
            return DefaultAddressKey;
        }

        return importPathMap.skillDataFolderAddressableKey;
    }

    public static string GetDefaultXlsxFolderPath()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        var folderPath = Path.Combine(projectRoot, DefaultXlsxRelativeFolder).Replace("\\", "/");
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    public static string GetDefaultXlsxFilePath()
    {
        return Path.Combine(GetDefaultXlsxFolderPath(), DefaultXlsxFileName).Replace("\\", "/");
    }

    public static string GetDefaultImportPathMapAssetPath()
    {
        return DefaultImportPathMapAssetPath;
    }

    public static SkillTypeImportPathMapSO LoadImportPathMapAsset(string importPathMapAssetPath)
    {
        var targetPath = string.IsNullOrWhiteSpace(importPathMapAssetPath)
            ? GetDefaultImportPathMapAssetPath()
            : importPathMapAssetPath.Replace("\\", "/");

        return AssetDatabase.LoadAssetAtPath<SkillTypeImportPathMapSO>(targetPath);
    }

    public static string ResolveFolderByAddressKey(string addressKey, bool logOnError = true)
    {
        return AddressableSyncShared.ResolveFolderByAddressKey(addressKey, nameof(SkillTypeXlsxSyncTool), logOnError);
    }

    public static bool ExportToXlsx(string addressKey, string xlsxPath, bool includeSerializedJson)
    {
        var folderPath = ResolveFolderByAddressKey(addressKey);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        var guids = AssetDatabase.FindAssets("t:SkillTypeSO", new[] { folderPath });
        var rows = new List<SkillTypeXlsxRow>(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            var skill = AssetDatabase.LoadAssetAtPath<SkillTypeSO>(assetPath);
            if (skill == null)
            {
                continue;
            }

            rows.Add(BuildRow(skill, assetPath, includeSerializedJson));
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.assetPath, b.assetPath));

        try
        {
            var targetPath = string.IsNullOrWhiteSpace(xlsxPath) ? GetDefaultXlsxFilePath() : xlsxPath;
            var normalizedXlsxPath = AddressableSyncShared.NormalizeFilePath(targetPath);
            var outputDir = Path.GetDirectoryName(normalizedXlsxPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet(DefaultSheetName);
            XlsxSyncShared.WriteHeaderRow(sheet, ColumnHeaders);

            for (int i = 0; i < rows.Count; i++)
            {
                var source = rows[i];
                var row = sheet.CreateRow(i + 1);
                row.CreateCell(0, CellType.String).SetCellValue(source.skillId ?? string.Empty);
                row.CreateCell(1, CellType.String).SetCellValue(source.damageType ?? string.Empty);
                row.CreateCell(2, CellType.Numeric).SetCellValue(source.damageTypeValue);
                row.CreateCell(3, CellType.Numeric).SetCellValue(source.baseDamage);
                row.CreateCell(4, CellType.Numeric).SetCellValue(source.hitCount);
                row.CreateCell(5, CellType.Numeric).SetCellValue(source.attackCoefficient);
                row.CreateCell(6, CellType.Numeric).SetCellValue(source.range);
                row.CreateCell(7, CellType.Numeric).SetCellValue(source.cooldown);
                row.CreateCell(8, CellType.String).SetCellValue(source.targetPolicy ?? string.Empty);
                row.CreateCell(9, CellType.Numeric).SetCellValue(source.targetPolicyValue);
                row.CreateCell(10, CellType.String).SetCellValue(source.assetAddressableKey ?? string.Empty);
                row.CreateCell(11, CellType.String).SetCellValue(source.assetPath ?? string.Empty);
                row.CreateCell(12, CellType.String).SetCellValue(source.dataTypeName ?? string.Empty);
                row.CreateCell(13, CellType.String).SetCellValue(source.attackSourceStatName ?? string.Empty);
                row.CreateCell(14, CellType.String).SetCellValue(source.comboSequenceName ?? string.Empty);
                row.CreateCell(15, CellType.String).SetCellValue(source.executionProfileName ?? string.Empty);
                row.CreateCell(16, CellType.String).SetCellValue(source.animatorOverrideName ?? string.Empty);
                row.CreateCell(17, CellType.String).SetCellValue(source.projectilePrefabName ?? string.Empty);
                row.CreateCell(18, CellType.String).SetCellValue(source.skillVfxPrefabName ?? string.Empty);
                row.CreateCell(19, CellType.String).SetCellValue(source.onHitVfxPrefabName ?? string.Empty);
                row.CreateCell(20, CellType.String).SetCellValue(source.castSfxName ?? string.Empty);
                row.CreateCell(21, CellType.String).SetCellValue(source.skillSlotImageName ?? string.Empty);

                var serializedJson = source.serializedJson ?? string.Empty;
                if (serializedJson.Length > XlsxSyncShared.ExcelCellMaxTextLength)
                {
                    Debug.LogError($"[SkillTypeXlsxSyncTool] serializedJson exceeds Excel cell limit. Asset={source.assetPath}");
                    return false;
                }

                row.CreateCell(22, CellType.String).SetCellValue(serializedJson);
            }

            using var stream = new FileStream(normalizedXlsxPath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(stream);

            Debug.Log($"[SkillTypeXlsxSyncTool] Export completed. Count={rows.Count}, Folder={folderPath}, Xlsx={normalizedXlsxPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SkillTypeXlsxSyncTool] Export failed. Xlsx={xlsxPath}, Error={e}");
            return false;
        }
    }

    public static bool ImportFromXlsx(
        string addressKey,
        string xlsxPath,
        bool createMissingAssets,
        bool applySerializedJson,
        string importPathMapAssetPath = null)
    {
        var targetPath = string.IsNullOrWhiteSpace(xlsxPath) ? GetDefaultXlsxFilePath() : xlsxPath;
        var normalizedXlsxPath = AddressableSyncShared.NormalizeFilePath(targetPath);
        if (!File.Exists(normalizedXlsxPath))
        {
            Debug.LogError($"[SkillTypeXlsxSyncTool] XLSX file not found: {normalizedXlsxPath}");
            return false;
        }

        var folderPath = ResolveFolderByAddressKey(addressKey);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        List<SkillTypeXlsxRow> rows;
        try
        {
            using var stream = new FileStream(normalizedXlsxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheet(DefaultSheetName) ?? workbook.GetSheetAt(0);
            if (sheet == null)
            {
                Debug.LogError($"[SkillTypeXlsxSyncTool] XLSX has no sheet. Xlsx={normalizedXlsxPath}");
                return false;
            }

            if (!XlsxSyncShared.TryBuildHeaderMap(sheet, ColumnHeaders, out var headerMap))
            {
                Debug.LogError($"[SkillTypeXlsxSyncTool] Invalid XLSX header. Xlsx={normalizedXlsxPath}");
                return false;
            }

            rows = ReadRows(sheet, headerMap);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SkillTypeXlsxSyncTool] Failed to deserialize XLSX. Xlsx={normalizedXlsxPath}, Error={e}");
            return false;
        }

        if (rows == null || rows.Count == 0)
        {
            Debug.LogError($"[SkillTypeXlsxSyncTool] XLSX has no skill rows. Xlsx={normalizedXlsxPath}");
            return false;
        }

        var importPathMap = LoadImportPathMapAsset(importPathMapAssetPath);

        int created = 0;
        int updated = 0;
        int skipped = 0;

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row == null)
            {
                skipped++;
                continue;
            }

            var targetAssetPath = ResolveTargetAssetPath(row, folderPath, importPathMap);
            if (string.IsNullOrWhiteSpace(targetAssetPath))
            {
                skipped++;
                continue;
            }

            var skill = AssetDatabase.LoadAssetAtPath<SkillTypeSO>(targetAssetPath);
            var desiredType = ResolveSkillType(row.dataTypeName) ?? typeof(SkillTypeSO);

            if (skill == null)
            {
                if (!createMissingAssets)
                {
                    skipped++;
                    continue;
                }

                AddressableSyncShared.EnsureFolder(Path.GetDirectoryName(targetAssetPath)?.Replace("\\", "/"));
                skill = ScriptableObject.CreateInstance(desiredType) as SkillTypeSO;
                if (skill == null)
                {
                    Debug.LogError($"[SkillTypeXlsxSyncTool] Failed to create asset instance. Type={desiredType.FullName}, Path={targetAssetPath}");
                    skipped++;
                    continue;
                }

                AssetDatabase.CreateAsset(skill, targetAssetPath);
                created++;
            }
            else
            {
                updated++;
            }

            ApplyRow(skill, row, applySerializedJson);
            EditorUtility.SetDirty(skill);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[SkillTypeXlsxSyncTool] Import completed. Folder={folderPath}, Xlsx={normalizedXlsxPath}, " +
            $"Created={created}, Updated={updated}, Skipped={skipped}, Rows={rows.Count}");
        return true;
    }

    private static SkillTypeXlsxRow BuildRow(SkillTypeSO skill, string assetPath, bool includeSerializedJson)
    {
        return new SkillTypeXlsxRow
        {
            skillId = skill.SkillId,
            damageType = skill.DamageType.ToString(),
            damageTypeValue = (int)skill.DamageType,
            baseDamage = skill.BaseDamage,
            hitCount = skill.HitCount,
            attackCoefficient = skill.AttackCoefficient,
            range = skill.Range,
            cooldown = skill.Cooldown,
            targetPolicy = skill.TargetPolicy.AllowedGroups.ToString(),
            targetPolicyValue = (int)skill.TargetPolicy.AllowedGroups,
            assetAddressableKey = AddressableSyncShared.ResolveAddressableKeyByAssetPath(assetPath),
            assetPath = assetPath,
            dataTypeName = skill.GetType().AssemblyQualifiedName,
            attackSourceStatName = skill.AttackSourceStatSO != null ? skill.AttackSourceStatSO.name : string.Empty,
            comboSequenceName = skill.ComboSequence != null ? skill.ComboSequence.name : string.Empty,
            executionProfileName = skill.ExecutionProfile != null ? skill.ExecutionProfile.name : string.Empty,
            animatorOverrideName = skill.AnimatorOverride != null ? skill.AnimatorOverride.name : string.Empty,
            projectilePrefabName = skill.ProjectilePrefab != null ? skill.ProjectilePrefab.name : string.Empty,
            skillVfxPrefabName = ResolveFirstCuePrefabName(skill, SkillEffectTrigger.OnConsume),
            onHitVfxPrefabName = ResolveFirstCuePrefabName(skill, SkillEffectTrigger.OnHit),
            castSfxName = skill.CastSFX != null ? skill.CastSFX.name : string.Empty,
            skillSlotImageName = skill.SkillSlotImage != null ? skill.SkillSlotImage.name : string.Empty,
            serializedJson = includeSerializedJson ? EditorJsonUtility.ToJson(skill, false) : string.Empty
        };
    }

    private static string ResolveFirstCuePrefabName(SkillTypeSO skill, SkillEffectTrigger trigger)
    {
        if (skill == null || !skill.HasSkillVfxCues || skill.SkillVfxCues == null)
        {
            return string.Empty;
        }

        var cues = skill.SkillVfxCues;
        for (int i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            if (cue == null || !cue.IsValid || cue.Trigger != trigger || cue.EffectPrefab == null)
            {
                continue;
            }

            return cue.EffectPrefab.name;
        }

        return string.Empty;
    }

    private static void ApplyRow(SkillTypeSO skill, SkillTypeXlsxRow row, bool applySerializedJson)
    {
        if (skill == null || row == null)
        {
            return;
        }

        Undo.RecordObject(skill, "Import SkillType XLSX");

        if (applySerializedJson && !string.IsNullOrWhiteSpace(row.serializedJson))
        {
            try
            {
                EditorJsonUtility.FromJsonOverwrite(row.serializedJson, skill);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SkillTypeXlsxSyncTool] Failed to apply serialized JSON. Asset={skill.name}, Error={e.Message}");
            }
        }

        var serializedObject = new SerializedObject(skill);

        SetString(serializedObject, "skillId", row.skillId);
        SetFloat(serializedObject, "baseDamage", row.baseDamage);
        SetInt(serializedObject, "hitCount", row.hitCount);
        SetFloat(serializedObject, "attackCoefficient", row.attackCoefficient);
        SetFloat(serializedObject, "range", row.range);
        SetFloat(serializedObject, "cooldown", row.cooldown);

        if (TryParseEnumValue(DamageTypeEnumType, row.damageType, row.damageTypeValue, out var damageTypeValue))
        {
            SetInt(serializedObject, "damageType", damageTypeValue);
        }

        if (TryParseEnumValue(TargetPolicyGroupEnumType, row.targetPolicy, row.targetPolicyValue, out var targetPolicyValue))
        {
            SetInt(serializedObject, "targetPolicy.allowedGroups", targetPolicyValue);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void SetString(SerializedObject serializedObject, string fieldName, string value)
    {
        var prop = serializedObject.FindProperty(fieldName);
        if (prop != null)
        {
            prop.stringValue = value ?? string.Empty;
        }
    }

    private static void SetInt(SerializedObject serializedObject, string fieldName, int value)
    {
        var prop = serializedObject.FindProperty(fieldName);
        if (prop != null)
        {
            prop.intValue = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string fieldName, float value)
    {
        var prop = serializedObject.FindProperty(fieldName);
        if (prop != null)
        {
            prop.floatValue = value;
        }
    }

    private static bool TryParseEnumValue(Type enumType, string enumName, int fallbackValue, out int enumValue)
    {
        if (enumType != null && enumType.IsEnum)
        {
            if (!string.IsNullOrWhiteSpace(enumName))
            {
                try
                {
                    var parsed = Enum.Parse(enumType, enumName, true);
                    enumValue = (int)Convert.ChangeType(parsed, typeof(int), CultureInfo.InvariantCulture);
                    return true;
                }
                catch
                {
                }
            }

            if (Enum.IsDefined(enumType, fallbackValue))
            {
                enumValue = fallbackValue;
                return true;
            }
        }

        enumValue = default;
        return false;
    }

    private static List<SkillTypeXlsxRow> ReadRows(ISheet sheet, Dictionary<string, int> headerMap)
    {
        var rows = new List<SkillTypeXlsxRow>();
        var formatter = new DataFormatter(CultureInfo.InvariantCulture);

        for (int r = 1; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null)
            {
                continue;
            }

            var item = new SkillTypeXlsxRow
            {
                skillId = XlsxSyncShared.ReadCellString(row, headerMap, "skillId", formatter),
                damageType = XlsxSyncShared.ReadCellString(row, headerMap, "damageType", formatter),
                damageTypeValue = XlsxSyncShared.ReadCellInt(row, headerMap, "damageTypeValue", formatter),
                baseDamage = XlsxSyncShared.ReadCellFloat(row, headerMap, "baseDamage", formatter),
                hitCount = XlsxSyncShared.ReadCellInt(row, headerMap, "hitCount", formatter),
                attackCoefficient = XlsxSyncShared.ReadCellFloat(row, headerMap, "attackCoefficient", formatter),
                range = XlsxSyncShared.ReadCellFloat(row, headerMap, "range", formatter),
                cooldown = XlsxSyncShared.ReadCellFloat(row, headerMap, "cooldown", formatter),
                targetPolicy = XlsxSyncShared.ReadCellString(row, headerMap, "targetPolicy", formatter),
                targetPolicyValue = XlsxSyncShared.ReadCellInt(row, headerMap, "targetPolicyValue", formatter),
                assetAddressableKey = XlsxSyncShared.ReadCellString(row, headerMap, "assetAddressableKey", formatter),
                assetPath = XlsxSyncShared.ReadCellString(row, headerMap, "assetPath", formatter),
                dataTypeName = XlsxSyncShared.ReadCellString(row, headerMap, "dataTypeName", formatter),
                attackSourceStatName = XlsxSyncShared.ReadCellString(row, headerMap, "attackSourceStatName", formatter),
                comboSequenceName = XlsxSyncShared.ReadCellString(row, headerMap, "comboSequenceName", formatter),
                executionProfileName = XlsxSyncShared.ReadCellString(row, headerMap, "executionProfileName", formatter),
                animatorOverrideName = XlsxSyncShared.ReadCellString(row, headerMap, "animatorOverrideName", formatter),
                projectilePrefabName = XlsxSyncShared.ReadCellString(row, headerMap, "projectilePrefabName", formatter),
                skillVfxPrefabName = XlsxSyncShared.ReadCellString(row, headerMap, "skillVfxPrefabName", formatter),
                onHitVfxPrefabName = XlsxSyncShared.ReadCellString(row, headerMap, "onHitVfxPrefabName", formatter),
                castSfxName = XlsxSyncShared.ReadCellString(row, headerMap, "castSfxName", formatter),
                skillSlotImageName = XlsxSyncShared.ReadCellString(row, headerMap, "skillSlotImageName", formatter),
                serializedJson = XlsxSyncShared.ReadCellString(row, headerMap, "serializedJson", formatter)
            };

            if (IsRowEmpty(item))
            {
                continue;
            }

            rows.Add(item);
        }

        return rows;
    }

    private static bool IsRowEmpty(SkillTypeXlsxRow row)
    {
        return string.IsNullOrWhiteSpace(row.skillId)
               && string.IsNullOrWhiteSpace(row.damageType)
               && row.damageTypeValue == 0
               && Math.Abs(row.baseDamage) < float.Epsilon
               && row.hitCount == 0
               && Math.Abs(row.attackCoefficient) < float.Epsilon
               && Math.Abs(row.range) < float.Epsilon
               && Math.Abs(row.cooldown) < float.Epsilon
               && string.IsNullOrWhiteSpace(row.targetPolicy)
               && row.targetPolicyValue == 0
               && string.IsNullOrWhiteSpace(row.assetAddressableKey)
               && string.IsNullOrWhiteSpace(row.assetPath)
               && string.IsNullOrWhiteSpace(row.dataTypeName)
               && string.IsNullOrWhiteSpace(row.serializedJson);
    }

    private static string ResolveTargetAssetPath(SkillTypeXlsxRow row, string rootFolder, SkillTypeImportPathMapSO importPathMap)
    {
        if (row == null || string.IsNullOrWhiteSpace(rootFolder))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(row.assetAddressableKey))
        {
            var fromAddressable = AddressableSyncShared.ResolveAssetPathByAddressableKey(row.assetAddressableKey);
            if (!string.IsNullOrWhiteSpace(fromAddressable)
                && fromAddressable.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase)
                && fromAddressable.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return fromAddressable;
            }
        }

        if (!string.IsNullOrWhiteSpace(row.assetPath))
        {
            var normalized = row.assetPath.Replace("\\", "/");
            if (normalized.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase)
                && normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }
        }

        if (TryResolvePathByImportMap(row, rootFolder, importPathMap, out var mappedPath))
        {
            return mappedPath;
        }

        var fileName = BuildFallbackFileName(row);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        return $"{rootFolder}/{fileName}.asset";
    }

    private static bool TryResolvePathByImportMap(
        SkillTypeXlsxRow row,
        string rootFolder,
        SkillTypeImportPathMapSO importPathMap,
        out string targetAssetPath)
    {
        targetAssetPath = null;
        if (row == null || string.IsNullOrWhiteSpace(rootFolder) || importPathMap == null || importPathMap.entries == null)
        {
            return false;
        }

        for (int i = 0; i < importPathMap.entries.Count; i++)
        {
            var entry = importPathMap.entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.targetFolderAddressableKey))
            {
                continue;
            }

            if (entry.damageTypeValue != row.damageTypeValue || !IsDataTypeMatch(row.dataTypeName, entry.dataTypeName))
            {
                continue;
            }

            var mappedFolder = ResolveFolderByAddressKey(entry.targetFolderAddressableKey, logOnError: false);
            if (string.IsNullOrWhiteSpace(mappedFolder) || !mappedFolder.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fileName = BuildFallbackFileName(row);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            targetAssetPath = $"{mappedFolder}/{fileName}.asset";
            return true;
        }

        return false;
    }

    private static bool IsDataTypeMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(
            AddressableSyncShared.NormalizeDataTypeName(left),
            AddressableSyncShared.NormalizeDataTypeName(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFallbackFileName(SkillTypeXlsxRow row)
    {
        var candidate = AddressableSyncShared.SanitizeFileName(row?.skillId);
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        var normalizedTypeName = AddressableSyncShared.NormalizeDataTypeName(row?.dataTypeName);
        if (!string.IsNullOrWhiteSpace(normalizedTypeName))
        {
            var lastDot = normalizedTypeName.LastIndexOf('.');
            var shortTypeName = lastDot >= 0 ? normalizedTypeName.Substring(lastDot + 1) : normalizedTypeName;
            candidate = AddressableSyncShared.SanitizeFileName($"{shortTypeName}_{row.damageTypeValue}");
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Type ResolveSkillType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        var direct = Type.GetType(typeName, false);
        if (IsValidSkillType(direct))
        {
            return direct;
        }

        var normalized = AddressableSyncShared.NormalizeDataTypeName(typeName);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            var t = assemblies[i].GetType(normalized, false) ?? assemblies[i].GetType(typeName, false);
            if (IsValidSkillType(t))
            {
                return t;
            }
        }

        return null;
    }

    private static bool IsValidSkillType(Type type)
    {
        return type != null && typeof(SkillTypeSO).IsAssignableFrom(type) && !type.IsAbstract;
    }
}

[Serializable]
public sealed class SkillTypeXlsxRow
{
    public string skillId;
    public string damageType;
    public int damageTypeValue;
    public float baseDamage;
    public int hitCount;
    public float attackCoefficient;
    public float range;
    public float cooldown;
    public string targetPolicy;
    public int targetPolicyValue;

    public string assetAddressableKey;
    public string assetPath;
    public string dataTypeName;

    public string attackSourceStatName;
    public string comboSequenceName;
    public string executionProfileName;
    public string animatorOverrideName;
    public string projectilePrefabName;
    public string skillVfxPrefabName;
    public string onHitVfxPrefabName;
    public string castSfxName;
    public string skillSlotImageName;

    public string serializedJson;
}
#endif
