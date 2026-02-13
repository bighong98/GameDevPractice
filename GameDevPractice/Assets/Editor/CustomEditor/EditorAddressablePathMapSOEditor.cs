#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[CustomEditor(typeof(EditorAddressablePathMapSO))]
public sealed class EditorAddressablePathMapSOEditor : Editor
{
    private const string EditorGroupName = "Editor";
    private string[] addressKeyOptions = Array.Empty<string>();
    private readonly List<string> optionsBuffer = new();

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        RefreshAddressKeyOptions();

        EditorGUILayout.HelpBox(
            "Map ID -> Addressable Key. Editor tools resolve by map ID first, then fallback to direct key.\n" +
            "Address key options are limited to folder keys inside Addressables group 'Editor'.",
            MessageType.Info);

        var entriesProp = serializedObject.FindProperty("entries");
        DrawEntries(entriesProp);

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Entry"))
            {
                entriesProp.arraySize++;
                var entry = entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1);
                entry.FindPropertyRelative("mapId").stringValue = string.Empty;
                entry.FindPropertyRelative("addressableKey").stringValue = string.Empty;
            }

            if (GUILayout.Button("Refresh Keys"))
            {
                RefreshAddressKeyOptions();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawEntries(SerializedProperty entriesProp)
    {
        if (entriesProp == null)
            return;

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var entryProp = entriesProp.GetArrayElementAtIndex(i);
            var mapIdProp = entryProp.FindPropertyRelative("mapId");
            var addressKeyProp = entryProp.FindPropertyRelative("addressableKey");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Entry {i}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Delete", GUILayout.Width(68f)))
                    {
                        entriesProp.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                mapIdProp.stringValue = EditorGUILayout.TextField("Map ID", mapIdProp.stringValue);
                DrawAddressableKeyPopup(addressKeyProp);
            }
        }
    }

    private void DrawAddressableKeyPopup(SerializedProperty addressKeyProp)
    {
        var current = addressKeyProp.stringValue ?? string.Empty;
        var currentIndex = FindIndexOrCustom(addressKeyOptions, current, out var options);
        var selectedIndex = EditorGUILayout.Popup("Addressable Key", currentIndex, options);

        if (selectedIndex != currentIndex)
            addressKeyProp.stringValue = selectedIndex == options.Length - 1 ? current : options[selectedIndex];

        if (selectedIndex == options.Length - 1)
            addressKeyProp.stringValue = EditorGUILayout.TextField("Address Key (Manual)", addressKeyProp.stringValue);
    }

    private void RefreshAddressKeyOptions()
    {
        addressKeyOptions = BuildAddressKeyOptions();
    }

    private static string[] BuildAddressKeyOptions()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return Array.Empty<string>();

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();
        for (int g = 0; g < settings.groups.Count; g++)
        {
            var group = settings.groups[g];
            if (group == null)
                continue;

            if (!string.Equals(group.Name, EditorGroupName, StringComparison.Ordinal))
                continue;

            foreach (var entry in group.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.address))
                    continue;

                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (string.IsNullOrWhiteSpace(path) || !AssetDatabase.IsValidFolder(path))
                    continue;

                if (!keys.Add(entry.address))
                    continue;

                ordered.Add(entry.address);
            }
        }

        ordered.Sort(StringComparer.Ordinal);
        return ordered.ToArray();
    }

    private int FindIndexOrCustom(string[] options, string current, out string[] withCustom)
    {
        optionsBuffer.Clear();
        if (options != null && options.Length > 0)
            optionsBuffer.AddRange(options);

        var index = optionsBuffer.FindIndex(x => string.Equals(x, current, StringComparison.Ordinal));
        if (index < 0)
        {
            optionsBuffer.Add("<Custom>");
            withCustom = optionsBuffer.ToArray();
            return withCustom.Length - 1;
        }

        optionsBuffer.Add("<Custom>");
        withCustom = optionsBuffer.ToArray();
        return index;
    }
}
#endif
