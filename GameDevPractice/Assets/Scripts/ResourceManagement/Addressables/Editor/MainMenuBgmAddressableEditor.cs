using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace TH.Resource.Editor
{
    public static class MainMenuBgmAddressableEditor
    {
        private const string MenuPath = "Tools/Addressables/Migration/Ensure MainMenu Bgm Addressable";
        private const string AssetPath = "Assets/Resources/Audio/extenz - Endless Summer.wav";
        private const string PreferredGroupName = "UIs";
        private const string FallbackGroupName = "Shared";

        [MenuItem(MenuPath)]
        private static void EnsureMainMenuBgmAddressable()
        {
            if (!System.IO.File.Exists(AssetPath))
            {
                Debug.LogError($"[{nameof(MainMenuBgmAddressableEditor)}] Asset not found: {AssetPath}");
                return;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"[{nameof(MainMenuBgmAddressableEditor)}] Addressable settings not found");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(PreferredGroupName) ?? settings.FindGroup(FallbackGroupName);
            if (group == null)
            {
                Debug.LogError($"[{nameof(MainMenuBgmAddressableEditor)}] Target group not found: {PreferredGroupName}/{FallbackGroupName}");
                return;
            }

            string guid = AssetDatabase.AssetPathToGUID(AssetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[{nameof(MainMenuBgmAddressableEditor)}] Failed to resolve guid: {AssetPath}");
                return;
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            entry.SetAddress(AssetPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Object asset = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            Debug.Log(
                $"[{nameof(MainMenuBgmAddressableEditor)}] Addressable entry ensured. group={group.Name}, guid={guid}, address={entry.address}",
                asset);
        }
    }
}
