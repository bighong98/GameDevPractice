#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets;
using UnityEngine;

public sealed class AddressablesGuidSearchWindow : EditorWindow
{
    private const string MenuPath = "Tools/Addressables/GUID Search";
    private const string GroupsWindowMenuPath = "Window/Asset Management/Addressables/Groups";
    private const int MaxApplyAttempts = 15;

    private static readonly Regex GuidRegex = new Regex("^[0-9a-fA-F]{32}$", RegexOptions.Compiled);

    [SerializeField] private string m_GuidInput = string.Empty;
    [SerializeField] private bool m_IncludeImplicit;
    [SerializeField] private bool m_AutoFocusGroupsWindow = true;
    [SerializeField] private string m_Status = "Enter a GUID and press Filter";

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        var window = GetWindow<AddressablesGuidSearchWindow>("Addressables GUID Search");
        window.minSize = new Vector2(430f, 150f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Addressables Groups Filter by GUID", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Looks up Addressables entry by GUID, then applies AssetPath text filter to the Groups window.", MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("GUID");
            m_GuidInput = EditorGUILayout.TextField(m_GuidInput);
        }

        m_IncludeImplicit = EditorGUILayout.ToggleLeft("Include implicit entries", m_IncludeImplicit);
        m_AutoFocusGroupsWindow = EditorGUILayout.ToggleLeft("Auto focus Groups window", m_AutoFocusGroupsWindow);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Filter", GUILayout.Height(26f)))
            {
                RunGuidFilter();
            }

            if (GUILayout.Button("Clear Filter", GUILayout.Height(26f)))
            {
                OpenGroupsAndApplyFilter(string.Empty, "<clear>", null);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(m_Status, MessageType.None);
    }

    private void RunGuidFilter()
    {
        var normalizedGuid = NormalizeGuid(m_GuidInput);
        if (!GuidRegex.IsMatch(normalizedGuid))
        {
            m_Status = "Invalid GUID format. Expected 32 hex characters.";
            Repaint();
            return;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            m_Status = "Addressables settings not found. Open Groups window once to initialize settings.";
            Repaint();
            return;
        }

        var entry = settings.FindAssetEntry(normalizedGuid, m_IncludeImplicit);
        if (entry == null)
        {
            var projectPath = AssetDatabase.GUIDToAssetPath(normalizedGuid);
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                m_Status = $"GUID not found in project or Addressables: {normalizedGuid}";
            }
            else
            {
                m_Status = $"Asset exists but is not an Addressables entry: {projectPath}";
                var projectAsset = AssetDatabase.LoadMainAssetAtPath(projectPath);
                if (projectAsset != null)
                {
                    EditorGUIUtility.PingObject(projectAsset);
                }
            }

            Repaint();
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.AssetPath))
        {
            m_Status = $"Addressables entry found, but AssetPath is empty for GUID: {normalizedGuid}";
            Repaint();
            return;
        }

        OpenGroupsAndApplyFilter(entry.AssetPath, normalizedGuid, entry.AssetPath);
    }

    private void OpenGroupsAndApplyFilter(string searchText, string guidForLog, string pingAssetPath)
    {
        EditorApplication.ExecuteMenuItem(GroupsWindowMenuPath);

        var attempt = 0;
        void TryApply()
        {
            attempt++;

            if (TryApplySearch(searchText, m_AutoFocusGroupsWindow, out var failureReason))
            {
                m_Status = string.IsNullOrEmpty(searchText)
                    ? "Groups filter cleared"
                    : $"Filtered Groups with AssetPath from GUID {guidForLog}: {searchText}";

                if (!string.IsNullOrWhiteSpace(pingAssetPath))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(pingAssetPath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                    }
                }

                Repaint();
                return;
            }

            if (attempt < MaxApplyAttempts)
            {
                EditorApplication.delayCall += TryApply;
                return;
            }

            m_Status = $"Failed to apply Groups filter: {failureReason}";
            Debug.LogWarning($"[AddressablesGuidSearch] Failed to apply filter after {MaxApplyAttempts} attempts. Reason: {failureReason}");
            Repaint();
        }

        EditorApplication.delayCall += TryApply;
    }

    private static string NormalizeGuid(string rawGuid)
    {
        if (string.IsNullOrWhiteSpace(rawGuid))
        {
            return string.Empty;
        }

        var trimmed = rawGuid.Trim();
        if (trimmed.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(5).Trim();
        }

        return trimmed.ToLowerInvariant();
    }

    private static bool TryApplySearch(string searchText, bool autoFocusGroupsWindow, out string failureReason)
    {
        failureReason = string.Empty;

        var groupsWindow = FindAddressablesGroupsWindow();
        if (groupsWindow == null)
        {
            failureReason = "Addressables Groups window is not available yet.";
            return false;
        }

        groupsWindow.Repaint();

        var groupEditorField = groupsWindow.GetType().GetField("m_GroupEditor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (groupEditorField == null)
        {
            failureReason = "m_GroupEditor field not found.";
            return false;
        }

        var groupEditor = groupEditorField.GetValue(groupsWindow);
        if (groupEditor == null)
        {
            failureReason = "Group editor is not initialized yet.";
            return false;
        }

        var entryTreeField = groupEditor.GetType().GetField("m_EntryTree", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (entryTreeField == null)
        {
            failureReason = "m_EntryTree field not found.";
            return false;
        }

        var entryTree = entryTreeField.GetValue(groupEditor);
        if (entryTree == null)
        {
            failureReason = "Entry tree is not initialized yet.";
            return false;
        }

        var searchMethod = entryTree.GetType().GetMethod(
            "Search",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new[] { typeof(string) },
            null);

        if (searchMethod == null)
        {
            failureReason = "Search(string) method not found on entry tree.";
            return false;
        }

        searchMethod.Invoke(entryTree, new object[] { searchText ?? string.Empty });

        if (autoFocusGroupsWindow)
        {
            groupsWindow.Focus();
        }

        groupsWindow.Repaint();
        return true;
    }

    private static EditorWindow FindAddressablesGroupsWindow()
    {
        var allEditorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
        foreach (var window in allEditorWindows)
        {
            if (window == null)
            {
                continue;
            }

            if (string.Equals(window.GetType().FullName, "UnityEditor.AddressableAssets.GUI.AddressableAssetsWindow", StringComparison.Ordinal))
            {
                return window;
            }
        }

        return null;
    }
}
#endif
