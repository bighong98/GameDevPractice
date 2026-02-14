#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using TH.Item;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

public sealed class ItemTypeXmlSyncWindow : EditorWindow
{
    private const string WindowTitle = "ItemType XML Sync";
    private const string MenuPath = "Tools/Item/Data Sync/ItemTypeSO XML Sync";

    
    private const string PrefAddressableKeyMapAssetPath = "TH.ItemTypeXmlSync.AddressableKeyMapAssetPath";
    private const string PrefXmlPath = "TH.ItemTypeXmlSync.XmlPath";
    private const string PrefCreateMissing = "TH.ItemTypeXmlSync.CreateMissing";
    private const string PrefApplySerializedJson = "TH.ItemTypeXmlSync.ApplySerializedJson";
    private const string PrefIncludeSerializedJsonOnExport = "TH.ItemTypeXmlSync.IncludeSerializedJsonOnExport";
    private const string PrefImportPathMapAssetPath = "TH.ItemTypeXmlSync.ImportPathMapAssetPath";

    [SerializeField] private string addressableKeyMapAssetPath = "";
    [SerializeField] private string xmlPath = "";
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
        xmlPath = EditorPrefs.GetString(PrefXmlPath, ItemTypeXmlSyncTool.GetDefaultXmlFilePath());
        if (string.IsNullOrWhiteSpace(xmlPath))
        {
            xmlPath = ItemTypeXmlSyncTool.GetDefaultXmlFilePath();
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
        EditorPrefs.SetString(PrefXmlPath, xmlPath ?? string.Empty);
        EditorPrefs.SetString(PrefImportPathMapAssetPath, importPathMapAssetPath ?? string.Empty);
        EditorPrefs.SetBool(PrefCreateMissing, createMissingAssets);
        EditorPrefs.SetBool(PrefApplySerializedJson, applySerializedJsonOnImport);
        EditorPrefs.SetBool(PrefIncludeSerializedJsonOnExport, includeSerializedJsonOnExport);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ItemTypeSO XML Sync", EditorStyles.boldLabel);
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
        xmlPath = EditorGUILayout.TextField("XML Path", xmlPath);

        if (GUILayout.Button("Browse", GUILayout.Width(76f)))
        {
            var selected = EditorUtility.OpenFilePanel("Select ItemType XML", ResolveDefaultDirectory(), "xml");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                xmlPath = NormalizePath(selected);
                GUI.FocusControl(null);
            }
        }

        if (GUILayout.Button("Save As", GUILayout.Width(76f)))
        {
            var defaultDir = ResolveDefaultDirectory();
            var defaultName = Path.GetFileNameWithoutExtension(ItemTypeXmlSyncTool.GetDefaultXmlFilePath());
            var selected = EditorUtility.SaveFilePanel("Save ItemType XML", defaultDir, defaultName, "xml");
            if (!string.IsNullOrWhiteSpace(selected))
            {
                xmlPath = NormalizePath(selected);
                GUI.FocusControl(null);
            }
        }

        if (GUILayout.Button("Use Default", GUILayout.Width(96f)))
        {
            xmlPath = ItemTypeXmlSyncTool.GetDefaultXmlFilePath();
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
            EditorGUILayout.TextField("Default XML Path", ItemTypeXmlSyncTool.GetDefaultXmlFilePath());
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
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(resolvedAddressKey) || string.IsNullOrWhiteSpace(xmlPath)))
            {
                if (GUILayout.Button("Export SO -> XML", GUILayout.Height(28f)))
                {
                    ItemTypeXmlSyncTool.ExportToXml(resolvedAddressKey, xmlPath, includeSerializedJsonOnExport);
                }
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(resolvedAddressKey) || string.IsNullOrWhiteSpace(xmlPath)))
            {
                if (GUILayout.Button("Import XML -> SO", GUILayout.Height(28f)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Import XML -> ItemTypeSO",
                        "XML 데이터를 ItemTypeSO 에셋으로 반영합니다. 계속하시겠습니까?",
                        "Import",
                        "Cancel"))
                    {
                        ItemTypeXmlSyncTool.ImportFromXml(
                            resolvedAddressKey,
                            xmlPath,
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
    private const string DefaultXmlFileName = "ItemTypeTable.xml";
    private const string DefaultImportPathMapAssetPath = "Assets/Editor/DataSync/ItemTypeImportPathMap.asset";

    [MenuItem("Tools/Item/Data Sync/Export ItemTypeSO XML (Default Key)")]
    private static void ExportDefault()
    {
        var outputPath = GetDefaultXmlFilePath();
        var keyMap = LoadAddressableKeyMapAsset(GetDefaultAddressableKeyMapAssetPath());
        var addressKey = ResolveItemDataFolderAddressableKey(keyMap);
        ExportToXml(string.IsNullOrWhiteSpace(addressKey) ? DefaultAddressKey : addressKey, outputPath, includeSerializedJson: true);
    }

    [MenuItem("Tools/Item/Data Sync/Import ItemTypeSO XML (Default Key)")]
    private static void ImportDefault()
    {
        var inputPath = GetDefaultXmlFilePath();
        if (!File.Exists(inputPath))
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] XML file not found: {inputPath}");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Import XML -> ItemTypeSO",
                "XML 데이터를 ItemTypeSO 에셋으로 반영합니다. 계속하시겠습니까?",
                "Import",
                "Cancel"))
        {
            return;
        }

        var keyMap = LoadAddressableKeyMapAsset(GetDefaultAddressableKeyMapAssetPath());
        var addressKey = ResolveItemDataFolderAddressableKey(keyMap);

        ImportFromXml(
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

    public static string GetDefaultXmlFilePath()
    {
        var folderPath = GetDefaultXmlFolderPath();
        return Path.Combine(folderPath, DefaultXmlFileName).Replace("\\", "/");
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



    public static bool ExportToXml(string addressKey, string xmlPath, bool includeSerializedJson)
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

        var table = new ItemTypeXmlTable
        {
            items = rows
        };

        try
        {
            var targetPath = string.IsNullOrWhiteSpace(xmlPath) ? GetDefaultXmlFilePath() : xmlPath;
            var normalizedXmlPath = NormalizeFilePath(targetPath);
            var outputDir = Path.GetDirectoryName(normalizedXmlPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            var serializer = new XmlSerializer(typeof(ItemTypeXmlTable));
            using var stream = new FileStream(normalizedXmlPath, FileMode.Create, FileAccess.Write, FileShare.None);
            serializer.Serialize(stream, table);

            Debug.Log($"[ItemTypeXmlSyncTool] Export completed. Count={rows.Count}, Folder={folderPath}, Xml={normalizedXmlPath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] Export failed. Xml={xmlPath}, Error={e}");
            return false;
        }
    }

    public static bool ImportFromXml(
        string addressKey,
        string xmlPath,
        bool createMissingAssets,
        bool applySerializedJson,
        string importPathMapAssetPath = null)
    {
        var targetPath = string.IsNullOrWhiteSpace(xmlPath) ? GetDefaultXmlFilePath() : xmlPath;
        var normalizedXmlPath = NormalizeFilePath(targetPath);
        if (!File.Exists(normalizedXmlPath))
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] XML file not found: {normalizedXmlPath}");
            return false;
        }

        var folderPath = ResolveFolderByAddressKey(addressKey);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        ItemTypeXmlTable table;
        try
        {
            var serializer = new XmlSerializer(typeof(ItemTypeXmlTable));
            using var stream = new FileStream(normalizedXmlPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            table = serializer.Deserialize(stream) as ItemTypeXmlTable;
        }
        catch (Exception e)
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] Failed to deserialize XML. Xml={normalizedXmlPath}, Error={e}");
            return false;
        }

        if (table == null || table.items == null)
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] XML has no item rows. Xml={normalizedXmlPath}");
            return false;
        }

        var importPathMap = LoadImportPathMapAsset(importPathMapAssetPath);

        int created = 0;
        int updated = 0;
        int skipped = 0;

        for (int i = 0; i < table.items.Count; i++)
        {
            var row = table.items[i];
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
            $"[ItemTypeXmlSyncTool] Import completed. Folder={folderPath}, Xml={normalizedXmlPath}, " +
            $"Created={created}, Updated={updated}, Skipped={skipped}, Rows={table.items.Count}");
        return true;
    }

    public static string ResolveFolderByAddressKey(string addressKey, bool logOnError = true)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
        {
            if (logOnError)
            {
                Debug.LogError("[ItemTypeXmlSyncTool] Address key is empty.");
            }

            return null;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            if (logOnError)
            {
                Debug.LogError("[ItemTypeXmlSyncTool] Addressable settings not found.");
            }

            return null;
        }

        for (int i = 0; i < settings.groups.Count; i++)
        {
            var group = settings.groups[i];
            if (group == null)
            {
                continue;
            }

            foreach (var entry in group.entries)
            {
                if (entry == null || !string.Equals(entry.address, addressKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    return path;
                }

                var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
                if (!string.IsNullOrWhiteSpace(parent) && AssetDatabase.IsValidFolder(parent))
                {
                    return parent;
                }
            }
        }

        if (logOnError)
        {
            Debug.LogError($"[ItemTypeXmlSyncTool] Address key not resolved: {addressKey}");
        }

        return null;
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
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return string.Empty;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return string.Empty;
        }

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrWhiteSpace(guid))
        {
            return string.Empty;
        }

        var entry = settings.FindAssetEntry(guid);
        return entry?.address ?? string.Empty;
    }

private static string ResolveAssetPathByAddressableKey(string addressableKey)
    {
        if (string.IsNullOrWhiteSpace(addressableKey))
        {
            return null;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return null;
        }

        for (int i = 0; i < settings.groups.Count; i++)
        {
            var group = settings.groups[i];
            if (group == null)
            {
                continue;
            }

            foreach (var entry in group.entries)
            {
                if (entry == null || !string.Equals(entry.address, addressableKey, StringComparison.Ordinal))
                {
                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }
        }

        return null;
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
        if (string.IsNullOrWhiteSpace(dataTypeName))
        {
            return string.Empty;
        }

        var trimmed = dataTypeName.Trim();
        var commaIndex = trimmed.IndexOf(',');
        if (commaIndex >= 0)
        {
            trimmed = trimmed.Substring(0, commaIndex).Trim();
        }

        return trimmed;
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
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var chars = raw.ToCharArray();
        var invalid = Path.GetInvalidFileNameChars();

        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars).Trim();
    }

    private static void EnsureFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var normalized = folderPath.Replace("\\", "/");
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        var parent = Path.GetDirectoryName(normalized)?.Replace("\\", "/");
        var folderName = Path.GetFileName(normalized);

        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(folderName))
        {
            return;
        }

        EnsureFolder(parent);

        if (!AssetDatabase.IsValidFolder(normalized))
        {
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

[Serializable]
public sealed class ItemTypeXmlTable
{
    public List<ItemTypeXmlRow> items = new();
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
