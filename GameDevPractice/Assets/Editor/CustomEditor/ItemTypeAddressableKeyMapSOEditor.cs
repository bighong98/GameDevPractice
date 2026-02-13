#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[CustomEditor(typeof(ItemTypeAddressableKeyMapSO))]
public sealed class ItemTypeAddressableKeyMapSOEditor : Editor
{
    private static readonly GUIContent ItemDataFolderKeyLabel = new("Item Data Folder Key");
    private string[] folderAddressKeyOptions = Array.Empty<string>();

    private void OnEnable()
    {
        RefreshOptions();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Addressable Key Mapping", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh Options", GUILayout.Width(120f)))
            {
                RefreshOptions();
            }
        }

        EditorGUILayout.HelpBox(
            "Select a folder addressable key used by ItemType XML sync root resolution.",
            MessageType.Info);

        var keyProp = serializedObject.FindProperty("itemDataFolderAddressableKey");
        DrawFolderKeyPopup(keyProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawFolderKeyPopup(SerializedProperty keyProp)
    {
        if (keyProp == null)
        {
            return;
        }

        var current = keyProp.stringValue ?? string.Empty;
        var currentIndex = FindIndexOrCustom(folderAddressKeyOptions, current, out var options);
        var selectedIndex = EditorGUILayout.Popup(ItemDataFolderKeyLabel, currentIndex, options);

        if (selectedIndex != currentIndex)
        {
            keyProp.stringValue = selectedIndex == options.Length - 1
                ? current
                : options[selectedIndex];
        }

        if (selectedIndex == options.Length - 1)
        {
            keyProp.stringValue = EditorGUILayout.TextField("Address Key (Manual)", keyProp.stringValue);
        }
    }

    private void RefreshOptions()
    {
        folderAddressKeyOptions = BuildFolderAddressKeyOptions();
    }

    private static string[] BuildFolderAddressKeyOptions()
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
}
#endif