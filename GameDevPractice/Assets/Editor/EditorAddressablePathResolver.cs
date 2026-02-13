#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class EditorAddressablePathResolver
{
    private const string DefaultMapAssetPath = "Assets/Editor/Scriptable Object/EditorAddressablePathMap.asset";
    private static EditorAddressablePathMapSO cachedMapAsset;

    public static string ResolvePathByMapIdOrAddressKey(string mapIdOrAddressKey, string callerName, bool logOnError = true)
    {
        if (string.IsNullOrWhiteSpace(mapIdOrAddressKey))
            return null;

        var resolvedAddressKey = ResolveAddressableKey(mapIdOrAddressKey);
        if (string.IsNullOrWhiteSpace(resolvedAddressKey))
            return null;

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            if (logOnError)
                Debug.LogError($"[{callerName}] Addressable settings not found. key={resolvedAddressKey}");
            return null;
        }

        for (int g = 0; g < settings.groups.Count; g++)
        {
            var group = settings.groups[g];
            if (group == null)
                continue;

            foreach (var entry in group.entries)
            {
                if (entry == null || !string.Equals(entry.address, resolvedAddressKey, StringComparison.Ordinal))
                    continue;

                var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                if (!string.IsNullOrWhiteSpace(path))
                    return path;
            }
        }

        if (logOnError)
            Debug.LogError($"[{callerName}] Addressable key not found: {resolvedAddressKey} (from mapId/address={mapIdOrAddressKey})");

        return null;
    }

    private static string ResolveAddressableKey(string mapIdOrAddressKey)
    {
        var map = LoadMapAsset();
        if (map != null && map.entries != null)
        {
            for (int i = 0; i < map.entries.Count; i++)
            {
                var entry = map.entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.mapId))
                    continue;

                if (!string.Equals(entry.mapId, mapIdOrAddressKey, StringComparison.Ordinal))
                    continue;

                if (!string.IsNullOrWhiteSpace(entry.addressableKey))
                    return entry.addressableKey;

                break;
            }
        }

        return mapIdOrAddressKey;
    }

    private static EditorAddressablePathMapSO LoadMapAsset()
    {
        if (cachedMapAsset != null)
            return cachedMapAsset;

        cachedMapAsset = AssetDatabase.LoadAssetAtPath<EditorAddressablePathMapSO>(DefaultMapAssetPath);
        return cachedMapAsset;
    }
}
#endif
