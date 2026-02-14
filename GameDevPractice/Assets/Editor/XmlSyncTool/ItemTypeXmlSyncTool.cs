#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using TH.Item;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

public sealed class ItemTypeXmlSyncWindow : EditorWindow
{
    private const string WindowTitle = "ItemType XLSX Sync";
    private const string MenuPath = "Tools/Item/Data Sync/ItemTypeSO XLSX Sync";

    
    private const string PrefAddressableKeyMapAssetPath = "TH.ItemTypeXmlSync.AddressableKeyMapAssetPath";
    private const string PrefXlsxPath = "TH.ItemTypeXmlSync.XlsxPath";
    private const string PrefCreateMissing = "TH.ItemTypeXmlSync.CreateMissing";
    private const string PrefApplySerializedJson = "TH.ItemTypeXmlSync.ApplySerializedJson";
    private const string PrefIncludeSerializedJsonOnExport = "TH.ItemTypeXmlSync.IncludeSerializedJsonOnExport";
    private const string PrefImportPathMapAssetPath = "TH.ItemTypeXmlSync.ImportPathMapAssetPath";

    [SerializeField] private string addressableKeyMapAssetPath = "";
    [SerializeField] private string xlsxPath = "";
    [SerializeField] private string importPathMapAssetPath = "";
    [SerializeField] private bool createMissingAssets = true;
    [SerializeField] private bool applySerializedJsonOnImport = true;
    [SerializeField] private bool includeSerializedJsonOnExport = true;

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        var window = GetWindow<ItemTypeXmlSyncWindow>(WindowTitle);
        window.minSize = new Vector2(540f, 250f);
        window.Show();
    }

    private void OnEnable()
    {
        addressableKeyMapAssetPath = EditorPrefs.GetString(
            PrefAddressableKeyMapAssetPath,
            ItemTypeXmlSyncTool.GetDefaultAddressableKeyMapAssetPath());
        if (string.IsNullOrWhiteSpace(addressableKeyMapAssetPath))
        {
            addressableKeyMapAssetPath = ItemTypeXmlSyncTool.GetDefaultAddressableKeyMapAssetPath();
        }
        xlsxPath = EditorPrefs.GetString(PrefXlsxPath, ItemTypeXmlSyncTool.GetDefaultXlsxFilePath());
        if (string.IsNullOrWhiteSpace(xlsxPath))
        {
            xlsxPath = ItemTypeXmlSyncTool.GetDefaultXlsxFilePath();
        }
        importPathMapAssetPath = EditorPrefs.GetString(PrefImportPathMapAssetPath, ItemTypeXmlSyncTool.GetDefaultImportPathMapAssetPath());
        if (string.IsNullOrWhiteSpace(importPathMapAssetPath))
        {
            importPathMapAssetPath = ItemTypeXmlSyncTool.GetDefaultImportPathMapAssetPath();
        }

        createMissingAssets = EditorPrefs.GetBool(PrefCreateMissing, true);
        applySerializedJsonOnImport = EditorPrefs.GetBool(PrefApplySerializedJson, true);
        includeSerializedJsonOnExport = EditorPrefs.GetBool(PrefIncludeSerializedJsonOnExport, true);
    }

    private void OnDisable()
    {
        EditorPrefs.SetString(PrefAddressableKeyMapAssetPath, addressableKeyMapAssetPath ?? string.Empty);
        EditorPrefs.SetString(PrefXlsxPath, xlsxPath ?? string.Empty);
        EditorPrefs.SetString(PrefImportPathMapAssetPath, importPathMapAssetPath ?? string.Empty);
        EditorPrefs.SetBool(PrefCreateMissing, createMissingAssets);
        EditorPrefs.SetBool(PrefApplySerializedJson, applySerializedJsonOnImport);
        EditorPrefs.SetBool(PrefIncludeSerializedJsonOnExport, includeSerializedJsonOnExport);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ItemTypeSO XLSX Sync", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        var currentAddressableKeyMap = ItemTypeXmlSyncTool.LoadAddressableKeyMapAsset(addressableKeyMapAssetPath);
        var selectedAddressableKeyMap = (ItemTypeAddressableKeyMapSO)EditorGUILayout.ObjectField(
            "Addressable Key Map",
            currentAddressableKeyMap,
            typeof(ItemTypeAddressableKeyMapSO),
            false);
        if (selectedAddressableKeyMap != currentAddressableKeyMap)
        {
            var selectedPath = selectedAddressableKeyMap == null
                ? ItemTypeXmlSyncTool.GetDefaultAddressableKeyMapAssetPath()
                : AssetDatabase.GetAssetPath(selectedAddressableKeyMap);
            addressableKeyMapAssetPath = NormalizePath(selectedPath);
        }
        var mapForResolve = selectedAddressableKeyMap != null ? selectedAddressableKeyMap : currentAddressableKeyMap;
        var resolvedAddressKey = ItemTypeXmlSyncTool.ResolveItemDataFolderAddressableKey(mapForResolve);

        EditorGUILayout.BeginHorizontal();
        xlsxPath = EditorGUILayout.TextField("XLSX Path", xlsxPath);

        if (GUILayout.Button("Browse", GUILayout.Width(76f)))
        {
            var selected = EditorUtility.OpenFilePanel("Select ItemType XLSX", ResolveDefaultDirectory(), "xlsx");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                xlsxPath = NormalizePath(selected);
                GUI.FocusControl(null);
            }
        }

        if (GUILayout.Button("Save As", GUILayout.Width(76f)))
        {
            var defaultDir = ResolveDefaultDirectory();
            var defaultName = Path.GetFileNameWithoutExtension(ItemTypeXmlSyncTool.GetDefaultXlsxFilePath());
            var selected = EditorUtility.SaveFilePanel("Save ItemType XLSX", defaultDir, defaultName, "xlsx");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                xlsxPath = NormalizePath(selected);
                GUI.FocusControl(null);
            }
        }

        if (GUILayout.Button("Use Default", GUILayout.Width(96f)))
        {
            xlsxPath = ItemTypeXmlSyncTool.GetDefaultXlsxFilePath();
            GUI.FocusControl(null);
        }

        EditorGUILayout.EndHorizontal();

        createMissingAssets = EditorGUILayout.ToggleLeft("Create missing assets on import", createMissingAssets);
        applySerializedJsonOnImport = EditorGUILayout.ToggleLeft("Apply serialized JSON payload on import", applySerializedJsonOnImport);
        includeSerializedJsonOnExport = EditorGUILayout.ToggleLeft("Include serialized JSON payload on export", includeSerializedJsonOnExport);

        var currentImportPathMap = ItemTypeXmlSyncTool.LoadImportPathMapAsset(importPathMapAssetPath);
        var selectedImportPathMap = (ItemTypeImportPathMapSO)EditorGUILayout.ObjectField(
            "Import Path Map",
            currentImportPathMap,
            typeof(ItemTypeImportPathMapSO),
            false);
        if (selectedImportPathMap != currentImportPathMap)
        {
            var selectedPath = selectedImportPathMap == null
                ? ItemTypeXmlSyncTool.GetDefaultImportPathMapAssetPath()
                : AssetDatabase.GetAssetPath(selectedImportPathMap);
            importPathMapAssetPath = NormalizePath(selectedPath);
        }

        EditorGUILayout.Space(8f);

        var resolvedFolder = ItemTypeXmlSyncTool.ResolveFolderByAddressKey(resolvedAddressKey, logOnError: false);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Resolved Item Folder", resolvedFolder ?? "(unresolved)");
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Resolved Item Folder Key", string.IsNullOrWhiteSpace(resolvedAddressKey) ? "(unresolved)" : resolvedAddressKey);
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Default XLSX Path", ItemTypeXmlSyncTool.GetDefaultXlsxFilePath());
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Default Addressable Key Map Path", ItemTypeXmlSyncTool.GetDefaultAddressableKeyMapAssetPath());
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Default Import Map Path", ItemTypeXmlSyncTool.GetDefaultImportPathMapAssetPath());
        }

        EditorGUILayout.Space(10f);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(resolvedAddressKey) || string.IsNullOrWhiteSpace(xlsxPath)))
            {
                if (GUILayout.Button("Export SO -> XLSX", GUILayout.Height(28f)))
                {
                    ItemTypeXmlSyncTool.ExportToXlsx(resolvedAddressKey, xlsxPath, includeSerializedJsonOnExport);
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(resolvedAddressKey) || string.IsNullOrWhiteSpace(xlsxPath)))
            {
                if (GUILayout.Button("Import XLSX -> SO", GUILayout.Height(28f)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Import XLSX -> ItemTypeSO",
                        "XML to ItemTypeSO",
                        "Import",
                        "Cancel"))
                    {
                        ItemTypeXmlSyncTool.ImportFromXlsx(
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
        return ItemTypeXmlSyncTool.GetDefaultXmlFolderPath();
    }

    private static string NormalizePath(string rawPath)
    {
        return string.IsNullOrWhiteSpace(rawPath) ? string.Empty : rawPath.Replace("\\", "/");
    }
}

public static class ItemTypeXmlSyncTool
{
    private const string DefaultAddressKey = "Item Data Folder";
    private const string DefaultAddressableKeyMapAssetPath = "Assets/Editor/Scriptable Object/ItemTypeAddressableKeyMap.asset";
    private const string DefaultXmlRelativeFolder = "Resources/Data Table";
    private const string DefaultXlsxFileName = "ItemTypeTable.xlsx";
    private const string DefaultSheetName = "ItemType";
    private const string DefaultImportPathMapAssetPath = "Assets/Editor/Scriptable Object/ItemTypeImportPathMap.asset";
    private static readonly string[] ColumnHeaders =
    {
        "nameString",
        "itemType",
        "itemTypeValue",
        "maxAmount",
        "description",
        "assetAddressableKey",
        "assetPath",
        "dataTypeName",
        "prefabName",
        "spriteName",
        "useEffectName",
        "serializedJson"
    };

    [MenuItem("Tools/Item/Data Sync/Export ItemTypeSO XLSX (Default Key)")]
    private static void ExportDefault()
    {
        var outputPath = GetDefaultXlsxFilePath();
        var keyMap = LoadAddressableKeyMapAsset(GetDefaultAddressableKeyMapAssetPath());
        var addressKey = ResolveItemDataFolderAddressableKey(keyMap);
        ExportToXlsx(string.IsNullOrWhiteSpace(addressKey) ? DefaultAddressKey : addressKey, outputPath, includeSerializedJson: true);
    }

    [MenuItem("Tools/Item/Data Sync/Import ItemTypeSO XLSX (Default Key)")]
    private static void ImportDefault()
    {
        var inputPath = GetDefaultXlsxFilePath();
        if (!File.Exists(inputPath))
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] XLSX file not found: {inputPath}");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Import XLSX -> ItemTypeSO",
                "XML to ItemTypeSO", // todo: 한글 인코딩 깨져서 임시로 변경함 재작성 필요
                "Import",
                "Cancel"))
        {
            return;
        }

        var keyMap = LoadAddressableKeyMapAsset(GetDefaultAddressableKeyMapAssetPath());
        var addressKey = ResolveItemDataFolderAddressableKey(keyMap);

        ImportFromXlsx(
            string.IsNullOrWhiteSpace(addressKey) ? DefaultAddressKey : addressKey,
            inputPath,
            createMissingAssets: true,
            applySerializedJson: true,
            importPathMapAssetPath: GetDefaultImportPathMapAssetPath());
    }

    public static string GetDefaultAddressableKeyMapAssetPath()
    {
        return DefaultAddressableKeyMapAssetPath;
    }

    public static ItemTypeAddressableKeyMapSO LoadAddressableKeyMapAsset(string addressableKeyMapAssetPath)
    {
        var targetPath = string.IsNullOrWhiteSpace(addressableKeyMapAssetPath)
            ? GetDefaultAddressableKeyMapAssetPath()
            : addressableKeyMapAssetPath.Replace("\\", "/");

        return AssetDatabase.LoadAssetAtPath<ItemTypeAddressableKeyMapSO>(targetPath);
    }

    public static string ResolveItemDataFolderAddressableKey(ItemTypeAddressableKeyMapSO keyMap)
    {
        if (keyMap == null || string.IsNullOrWhiteSpace(keyMap.itemDataFolderAddressableKey))
        {
            return DefaultAddressKey;
        }

        return keyMap.itemDataFolderAddressableKey;
    }

    public static string GetDefaultXmlFolderPath()
    {
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        var folderPath = Path.Combine(projectRoot, DefaultXmlRelativeFolder).Replace("\\", "/");
        Directory.CreateDirectory(folderPath);
        return folderPath;
    }

    public static string GetDefaultXlsxFilePath()
    {
        var folderPath = GetDefaultXmlFolderPath();
        return Path.Combine(folderPath, DefaultXlsxFileName).Replace("\\", "/");
    }

    public static string GetDefaultImportPathMapAssetPath()
    {
        return DefaultImportPathMapAssetPath;
    }

    public static ItemTypeImportPathMapSO LoadImportPathMapAsset(string importPathMapAssetPath)
    {
        var targetPath = string.IsNullOrWhiteSpace(importPathMapAssetPath)
            ? GetDefaultImportPathMapAssetPath()
            : importPathMapAssetPath.Replace("\\", "/");

        return AssetDatabase.LoadAssetAtPath<ItemTypeImportPathMapSO>(targetPath);
    }



    public static bool ExportToXlsx(string addressKey, string xlsxPath, bool includeSerializedJson)
    {
        var folderPath = ResolveFolderByAddressKey(addressKey);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        var guids = AssetDatabase.FindAssets("t:ItemTypeSO", new[] { folderPath });
        var rows = new List<ItemTypeXmlRow>(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                continue;
            }

            var item = AssetDatabase.LoadAssetAtPath<ItemTypeSO>(assetPath);
            if (item == null)
            {
                continue;
            }

            rows.Add(BuildRow(item, assetPath, includeSerializedJson));
        }

        rows.Sort((a, b) => string.CompareOrdinal(a.assetPath, b.assetPath));

        try
        {
            var targetPath = string.IsNullOrWhiteSpace(xlsxPath) ? GetDefaultXlsxFilePath() : xlsxPath;
            var normalizedXlsxPath = NormalizeFilePath(targetPath);
            var outputDir = Path.GetDirectoryName(normalizedXlsxPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using var workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet(DefaultSheetName);
            WriteHeaderRow(sheet);

            for (int i = 0; i < rows.Count; i++)
            {
                var source = rows[i];
                var row = sheet.CreateRow(i + 1);
                row.CreateCell(0, CellType.String).SetCellValue(source.nameString ?? string.Empty);
                row.CreateCell(1, CellType.String).SetCellValue(source.itemType ?? string.Empty);
                row.CreateCell(2, CellType.Numeric).SetCellValue(source.itemTypeValue);
                row.CreateCell(3, CellType.Numeric).SetCellValue(source.maxAmount);
                row.CreateCell(4, CellType.String).SetCellValue(source.description ?? string.Empty);
                row.CreateCell(5, CellType.String).SetCellValue(source.assetAddressableKey ?? string.Empty);
                row.CreateCell(6, CellType.String).SetCellValue(source.assetPath ?? string.Empty);
                row.CreateCell(7, CellType.String).SetCellValue(source.dataTypeName ?? string.Empty);
                row.CreateCell(8, CellType.String).SetCellValue(source.prefabName ?? string.Empty);
                row.CreateCell(9, CellType.String).SetCellValue(source.spriteName ?? string.Empty);
                row.CreateCell(10, CellType.String).SetCellValue(source.useEffectName ?? string.Empty);

                var serializedJson = source.serializedJson ?? string.Empty;
                if (serializedJson.Length > XlsxSyncShared.ExcelCellMaxTextLength)
                {
                    Debug.LogError($"[ItemTypeXmlSyncTool] serializedJson exceeds Excel cell limit. Asset={source.assetPath}");
                    return false;
                }

                row.CreateCell(11, CellType.String).SetCellValue(serializedJson);
            }

            using var stream = new FileStream(normalizedXlsxPath, FileMode.Create, FileAccess.Write, FileShare.None);
            workbook.Write(stream);

            Debug.Log($"[ItemTypeXmlSyncTool] Export completed. Count={rows.Count}, Folder={folderPath}, Xlsx={normalizedXlsxPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] Export failed. Xlsx={xlsxPath}, Error={e}");
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
        var normalizedXlsxPath = NormalizeFilePath(targetPath);
        if (!File.Exists(normalizedXlsxPath))
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] XLSX file not found: {normalizedXlsxPath}");
            return false;
        }

        var folderPath = ResolveFolderByAddressKey(addressKey);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        List<ItemTypeXmlRow> rows;
        try
        {
            using var stream = new FileStream(normalizedXlsxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var workbook = new XSSFWorkbook(stream);
            var sheet = workbook.GetSheet(DefaultSheetName) ?? workbook.GetSheetAt(0);
            if (sheet == null)
            {
                Debug.LogError($"[ItemTypeXmlSyncTool] XLSX has no sheet. Xlsx={normalizedXlsxPath}");
                return false;
            }

            if (!TryBuildHeaderMap(sheet, out var headerMap))
            {
                Debug.LogError($"[ItemTypeXmlSyncTool] Invalid XLSX header. Xlsx={normalizedXlsxPath}");
                return false;
            }

            rows = ReadRows(sheet, headerMap);
        }
        catch (Exception e)
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] Failed to deserialize XLSX. Xlsx={normalizedXlsxPath}, Error={e}");
            return false;
        }

        if (rows == null || rows.Count == 0)
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] XLSX has no item rows. Xlsx={normalizedXlsxPath}");
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

            var item = AssetDatabase.LoadAssetAtPath<ItemTypeSO>(targetAssetPath);
            var desiredType = ResolveItemType(row.dataTypeName) ?? typeof(ItemTypeSO);

            if (item == null)
            {
                if (!createMissingAssets)
                {
                    skipped++;
                    continue;
                }

                EnsureFolder(Path.GetDirectoryName(targetAssetPath)?.Replace("\\", "/"));
                item = ScriptableObject.CreateInstance(desiredType) as ItemTypeSO;
                if (item == null)
                {
                    Debug.LogError($"[ItemTypeXmlSyncTool] Failed to create asset instance. Type={desiredType.FullName}, Path={targetAssetPath}");
                    skipped++;
                    continue;
                }

                AssetDatabase.CreateAsset(item, targetAssetPath);
                created++;
            }
            else
            {
                updated++;
            }

            ApplyRow(item, row, applySerializedJson);
            EditorUtility.SetDirty(item);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[ItemTypeXmlSyncTool] Import completed. Folder={folderPath}, Xlsx={normalizedXlsxPath}, " +
            $"Created={created}, Updated={updated}, Skipped={skipped}, Rows={rows.Count}");
        return true;
    }

private static void WriteHeaderRow(ISheet sheet)
    {
        XlsxSyncShared.WriteHeaderRow(sheet, ColumnHeaders);
    }

private static bool TryBuildHeaderMap(ISheet sheet, out Dictionary<string, int> headerMap)
    {
        return XlsxSyncShared.TryBuildHeaderMap(sheet, ColumnHeaders, out headerMap);
    }

    private static List<ItemTypeXmlRow> ReadRows(ISheet sheet, Dictionary<string, int> headerMap)
    {
        var rows = new List<ItemTypeXmlRow>();
        var formatter = new DataFormatter(CultureInfo.InvariantCulture);

        for (int r = 1; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null)
            {
                continue;
            }

            var item = new ItemTypeXmlRow
            {
                nameString = ReadCellString(row, headerMap, "nameString", formatter),
                itemType = ReadCellString(row, headerMap, "itemType", formatter),
                itemTypeValue = ReadCellInt(row, headerMap, "itemTypeValue", formatter),
                maxAmount = ReadCellInt(row, headerMap, "maxAmount", formatter),
                description = ReadCellString(row, headerMap, "description", formatter),
                assetAddressableKey = ReadCellString(row, headerMap, "assetAddressableKey", formatter),
                assetPath = ReadCellString(row, headerMap, "assetPath", formatter),
                dataTypeName = ReadCellString(row, headerMap, "dataTypeName", formatter),
                prefabName = ReadCellString(row, headerMap, "prefabName", formatter),
                spriteName = ReadCellString(row, headerMap, "spriteName", formatter),
                useEffectName = ReadCellString(row, headerMap, "useEffectName", formatter),
                serializedJson = ReadCellString(row, headerMap, "serializedJson", formatter)
            };

            if (IsRowEmpty(item))
            {
                continue;
            }

            rows.Add(item);
        }

        return rows;
    }

private static string ReadCellString(IRow row, Dictionary<string, int> headerMap, string key, DataFormatter formatter)
    {
        return XlsxSyncShared.ReadCellString(row, headerMap, key, formatter);
    }

private static int ReadCellInt(IRow row, Dictionary<string, int> headerMap, string key, DataFormatter formatter)
    {
        return XlsxSyncShared.ReadCellInt(row, headerMap, key, formatter);
    }

    private static bool IsRowEmpty(ItemTypeXmlRow row)
    {
        return string.IsNullOrWhiteSpace(row.nameString)
               && string.IsNullOrWhiteSpace(row.itemType)
               && row.itemTypeValue == 0
               && row.maxAmount == 0
               && string.IsNullOrWhiteSpace(row.description)
               && string.IsNullOrWhiteSpace(row.assetAddressableKey)
               && string.IsNullOrWhiteSpace(row.assetPath)
               && string.IsNullOrWhiteSpace(row.dataTypeName)
               && string.IsNullOrWhiteSpace(row.serializedJson);
    }

public static string ResolveFolderByAddressKey(string addressKey, bool logOnError = true)
    {
        return AddressableSyncShared.ResolveFolderByAddressKey(addressKey, nameof(ItemTypeXmlSyncTool), logOnError);
    }

private static ItemTypeXmlRow BuildRow(ItemTypeSO item, string assetPath, bool includeSerializedJson)
    {
        var row = new ItemTypeXmlRow
        {
            itemType = item.itemType.ToString(),
            nameString = item.nameString,
            maxAmount = item.maxAmount,
            description = item.desc,

            assetAddressableKey = ResolveAddressableKeyByAssetPath(assetPath),
            assetPath = assetPath,
            dataTypeName = item.GetType().AssemblyQualifiedName,

            prefabName = item.prefab != null ? item.prefab.name : string.Empty,
            spriteName = item.sprite != null ? item.sprite.name : string.Empty,
            useEffectName = SerializeItemUseEffectNames(item.itemUseEffects),
            itemTypeValue = (int)item.itemType,
            serializedJson = includeSerializedJson ? EditorJsonUtility.ToJson(item, false) : string.Empty
        };

        return row;
    }

    private static void ApplyRow(ItemTypeSO item, ItemTypeXmlRow row, bool applySerializedJson)
    {
        if (item == null || row == null)
        {
            return;
        }

        Undo.RecordObject(item, "Import ItemType XML");

        if (applySerializedJson && !string.IsNullOrWhiteSpace(row.serializedJson))
        {
            try
            {
                EditorJsonUtility.FromJsonOverwrite(row.serializedJson, item);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ItemTypeXmlSyncTool] Failed to apply serialized JSON. Asset={item.name}, Error={e.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(row.nameString) || row.nameString == string.Empty)
        {
            item.nameString = row.nameString ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(row.description) || row.description == string.Empty)
        {
            item.desc = row.description ?? string.Empty;
        }

        if (TryParseItemType(row, out var parsedItemType))
        {
            item.itemType = parsedItemType;
        }

        item.maxAmount = row.maxAmount;
    }

    private static bool TryParseItemType(ItemTypeXmlRow row, out Enums.ItemType itemType)
    {
        if (!string.IsNullOrWhiteSpace(row.itemType) && Enum.TryParse(row.itemType, true, out itemType))
        {
            return true;
        }

        if (Enum.IsDefined(typeof(Enums.ItemType), row.itemTypeValue))
        {
            itemType = (Enums.ItemType)row.itemTypeValue;
            return true;
        }

        itemType = default;
        return false;
    }

    private static string SerializeItemUseEffectNames(List<ItemEffectBase> effects)
    {
        if (effects == null || effects.Count == 0)
        {
            return string.Empty;
        }

        var names = new List<string>(effects.Count);
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(effect.name))
            {
                names.Add(effect.name);
            }
        }

        return string.Join("|", names);
    }

private static string ResolveAddressableKeyByAssetPath(string assetPath)
    {
        return AddressableSyncShared.ResolveAddressableKeyByAssetPath(assetPath);
    }

private static string ResolveAssetPathByAddressableKey(string addressableKey)
    {
        return AddressableSyncShared.ResolveAssetPathByAddressableKey(addressableKey);
    }



private static string ResolveTargetAssetPath(ItemTypeXmlRow row, string rootFolder, ItemTypeImportPathMapSO importPathMap)
    {
        if (row == null || string.IsNullOrWhiteSpace(rootFolder))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(row.assetAddressableKey))
        {
            var fromAddressable = ResolveAssetPathByAddressableKey(row.assetAddressableKey);
            if (!string.IsNullOrWhiteSpace(fromAddressable) &&
                fromAddressable.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase) &&
                fromAddressable.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return fromAddressable;
            }
        }

        if (!string.IsNullOrWhiteSpace(row.assetPath))
        {
            var normalized = row.assetPath.Replace("\\", "/");
            if (normalized.StartsWith(rootFolder, StringComparison.OrdinalIgnoreCase) && normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }
        }

        if (TryResolvePathByImportMap(row, rootFolder, importPathMap, out var mappedPath))
        {
            return mappedPath;
        }

        return null;
    }

    private static bool TryResolvePathByImportMap(
        ItemTypeXmlRow row,
        string rootFolder,
        ItemTypeImportPathMapSO importPathMap,
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

            if (entry.itemTypeValue != row.itemTypeValue || !IsDataTypeMatch(row.dataTypeName, entry.dataTypeName))
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

    private static string BuildFallbackFileName(ItemTypeXmlRow row)
    {
        var candidate = SanitizeFileName(row?.nameString);
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        var normalizedTypeName = NormalizeDataTypeName(row?.dataTypeName);
        if (!string.IsNullOrWhiteSpace(normalizedTypeName))
        {
            var lastDot = normalizedTypeName.LastIndexOf('.');
            var shortTypeName = lastDot >= 0 ? normalizedTypeName.Substring(lastDot + 1) : normalizedTypeName;
            candidate = SanitizeFileName($"{shortTypeName}_{row.itemTypeValue}");
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return null;
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
            NormalizeDataTypeName(left),
            NormalizeDataTypeName(right),
            StringComparison.OrdinalIgnoreCase);
    }

private static string NormalizeDataTypeName(string dataTypeName)
    {
        return AddressableSyncShared.NormalizeDataTypeName(dataTypeName);
    }

    private static Type ResolveItemType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        var direct = Type.GetType(typeName, false);
        if (IsValidItemType(direct))
        {
            return direct;
        }

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            var t = assemblies[i].GetType(typeName, false);
            if (IsValidItemType(t))
            {
                return t;
            }
        }

        return null;
    }

    private static bool IsValidItemType(Type type)
    {
        return type != null && typeof(ItemTypeSO).IsAssignableFrom(type) && !type.IsAbstract;
    }

private static string NormalizeFilePath(string path)
    {
        return AddressableSyncShared.NormalizeFilePath(path);
    }

private static string SanitizeFileName(string raw)
    {
        return AddressableSyncShared.SanitizeFileName(raw);
    }

private static void EnsureFolder(string folderPath)
    {
        AddressableSyncShared.EnsureFolder(folderPath);
    }
}

[Serializable]
public sealed class ItemTypeXmlRow
{
    public string nameString;
    public string itemType;
    public int itemTypeValue;
    public int maxAmount;
    public string description;

    public string assetAddressableKey;
    public string assetPath;
    public string dataTypeName;

    public string prefabName;
    public string spriteName;
    public string useEffectName;

    public string serializedJson;
}
#endif

