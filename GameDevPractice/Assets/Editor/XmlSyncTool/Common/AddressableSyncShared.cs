#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;

public static class AddressableSyncShared
{
    public static string ResolveFolderByMapIdOrAddressKey(string mapIdOrAddressKey, string callerName, bool logOnError = true)
    {
        var resolvedPath = EditorAddressablePathResolver.ResolvePathByMapIdOrAddressKey(mapIdOrAddressKey, callerName, logOnError);
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            return null;
        }

        if (AssetDatabase.IsValidFolder(resolvedPath))
        {
            return resolvedPath;
        }

        var parent = Path.GetDirectoryName(resolvedPath)?.Replace("\\", "/");
        if (!string.IsNullOrWhiteSpace(parent) && AssetDatabase.IsValidFolder(parent))
        {
            return parent;
        }

        if (logOnError)
        {
            UnityEngine.Debug.LogError($"[{callerName}] Resolved path is not a valid folder. Path={resolvedPath}");
        }

        return null;
    }

    public static string ResolveFolderByAddressKey(string addressKey, string callerName, bool logOnError = true)
    {
        if (string.IsNullOrWhiteSpace(addressKey))
        {
            if (logOnError)
            {
                UnityEngine.Debug.LogError($"[{callerName}] Address key is empty.");
            }

            return null;
        }

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            if (logOnError)
            {
                UnityEngine.Debug.LogError($"[{callerName}] Addressable settings not found.");
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
            UnityEngine.Debug.LogError($"[{callerName}] Address key not resolved: {addressKey}");
        }

        return null;
    }

    public static string ResolveAddressableKeyByAssetPath(string assetPath)
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

    public static string ResolveAssetPathByAddressableKey(string addressableKey)
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

    public static string NormalizeFilePath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace("\\", "/");
    }

    public static string NormalizeDataTypeName(string dataTypeName)
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

    public static string SanitizeFileName(string raw)
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

    public static void EnsureFolder(string folderPath)
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
#endif
