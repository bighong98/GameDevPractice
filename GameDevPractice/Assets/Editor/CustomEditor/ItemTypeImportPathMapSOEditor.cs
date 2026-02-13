#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[CustomEditor(typeof(ItemTypeImportPathMapSO))]
public sealed class ItemTypeImportPathMapSOEditor : Editor
{
    private const string EditorGroupName = "Editor";
    private static readonly GUIContent DataTypeLabel = new("Data Type");
    private static readonly GUIContent ItemTypeLabel = new("Item Type");
    private static readonly GUIContent AddressKeyLabel = new("Target Folder Addressable Key");

    private string[] dataTypeOptions = Array.Empty<string>();
    private string[] addressKeyOptions = Array.Empty<string>();

    private void OnEnable()
    {
        RefreshOptions();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawToolbar();
        EditorGUILayout.Space(6f);

        var entriesProp = serializedObject.FindProperty("entries");
        if (entriesProp == null)
        {
            EditorGUILayout.HelpBox("entries property not found.", MessageType.Error);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var entryProp = entriesProp.GetArrayElementAtIndex(i);
            DrawEntry(entryProp, i, entriesProp);
            EditorGUILayout.Space(8f);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Entry"))
            {
                entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
                var newEntry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
                InitializeEntry(newEntry);
            }

            if (GUILayout.Button("Clear All"))
            {
                if (EditorUtility.DisplayDialog("Clear All Entries", "Delete all entries?", "Clear", "Cancel"))
                {
                    entriesProp.ClearArray();
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Import Path Mapping", EditorStyles.boldLabel);

            if (GUILayout.Button("Refresh Options", GUILayout.Width(120f)))
            {
                RefreshOptions();
            }
        }

        EditorGUILayout.HelpBox(
            "Use dropdowns to map (dataTypeName + itemTypeValue) -> targetFolderAddressableKey.\n" +
            "Addressable key must already exist. This tool does not register entries automatically.\n" +
            "Address key options are limited to folder keys inside Addressables group 'Editor'.",
            MessageType.Info);
    }

    private void DrawEntry(SerializedProperty entryProp, int index, SerializedProperty entriesProp)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Entry {index}", EditorStyles.boldLabel);

                if (GUILayout.Button("Delete", GUILayout.Width(64f)))
                {
                    entriesProp.DeleteArrayElementAtIndex(index);
                    return;
                }
            }

            var dataTypeNameProp = entryProp.FindPropertyRelative("dataTypeName");
            var itemTypeValueProp = entryProp.FindPropertyRelative("itemTypeValue");
            var targetAddressKeyProp = entryProp.FindPropertyRelative("targetFolderAddressableKey");

            DrawDataTypePopup(dataTypeNameProp);
            DrawItemTypePopup(itemTypeValueProp);
            DrawAddressKeyPopup(targetAddressKeyProp);
        }
    }

    private void DrawDataTypePopup(SerializedProperty dataTypeNameProp)
    {
        if (dataTypeNameProp == null)
        {
            return;
        }

        var current = dataTypeNameProp.stringValue ?? string.Empty;
        var currentIndex = FindIndexOrCustom(dataTypeOptions, current, out var options);
        var selectedIndex = EditorGUILayout.Popup(DataTypeLabel, currentIndex, options);

        if (selectedIndex != currentIndex)
        {
            dataTypeNameProp.stringValue = selectedIndex == options.Length - 1
                ? current
                : options[selectedIndex];
        }

        if (selectedIndex == options.Length - 1)
        {
            dataTypeNameProp.stringValue = EditorGUILayout.TextField("Data Type (Manual)", dataTypeNameProp.stringValue);
        }
    }

    private static void DrawItemTypePopup(SerializedProperty itemTypeValueProp)
    {
        if (itemTypeValueProp == null)
        {
            return;
        }

        var value = itemTypeValueProp.intValue;
        var itemType = Enum.IsDefined(typeof(Enums.ItemType), value)
            ? (Enums.ItemType)value
            : Enums.ItemType.Default;

        var selected = (Enums.ItemType)EditorGUILayout.EnumPopup(ItemTypeLabel, itemType);
        itemTypeValueProp.intValue = (int)selected;
    }

    private void DrawAddressKeyPopup(SerializedProperty targetAddressKeyProp)
    {
        if (targetAddressKeyProp == null)
        {
            return;
        }

        var current = targetAddressKeyProp.stringValue ?? string.Empty;
        var currentIndex = FindIndexOrCustom(addressKeyOptions, current, out var options);
        var selectedIndex = EditorGUILayout.Popup(AddressKeyLabel, currentIndex, options);

        if (selectedIndex != currentIndex)
        {
            targetAddressKeyProp.stringValue = selectedIndex == options.Length - 1
                ? current
                : options[selectedIndex];
        }

        if (selectedIndex == options.Length - 1)
        {
            targetAddressKeyProp.stringValue = EditorGUILayout.TextField("Address Key (Manual)", targetAddressKeyProp.stringValue);
        }
    }

    private void RefreshOptions()
    {
        dataTypeOptions = BuildDataTypeOptions();
        addressKeyOptions = BuildAddressKeyOptions();
    }

    private static string[] BuildDataTypeOptions()
    {
        var results = new List<string>();

        if (!typeof(ItemTypeSO).IsAbstract)
        {
            results.Add(typeof(ItemTypeSO).FullName);
        }

        foreach (var type in TypeCache.GetTypesDerivedFrom<ItemTypeSO>())
        {
            if (type == null || type.IsAbstract || string.IsNullOrWhiteSpace(type.FullName))
            {
                continue;
            }

            results.Add(type.FullName);
        }

        return results
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] BuildAddressKeyOptions()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            return Array.Empty<string>();
        }

        var keySet = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < settings.groups.Count; i++)
        {
            var group = settings.groups[i];
            if (group == null)
            {
                continue;
            }

            if (!string.Equals(group.Name, EditorGroupName, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var entry in group.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.address))
                {
                    continue;
                }

                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrWhiteSpace(path) || !AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                keySet.Add(entry.address);
            }
        }

        return keySet.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static int FindIndexOrCustom(string[] baseOptions, string value, out string[] options)
    {
        var list = new List<string>(baseOptions ?? Array.Empty<string>());
        const string manual = "<Manual Input>";

        if (!list.Contains(manual, StringComparer.Ordinal))
        {
            list.Add(manual);
        }

        options = list.ToArray();
        var index = Array.IndexOf(options, value);
        return index >= 0 ? index : options.Length - 1;
    }

    private static void InitializeEntry(SerializedProperty entryProp)
    {
        if (entryProp == null)
        {
            return;
        }

        entryProp.FindPropertyRelative("dataTypeName").stringValue = typeof(ItemTypeSO).FullName;
        entryProp.FindPropertyRelative("itemTypeValue").intValue = (int)Enums.ItemType.Default;
        entryProp.FindPropertyRelative("targetFolderAddressableKey").stringValue = string.Empty;
    }
}
#endif
