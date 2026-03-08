using System;
using System.Collections.Generic;
using TH.Resource;
using UnityEditor;
using UnityEngine;

namespace TH.Item.Editor
{
    // Temporary editor utility: fill missing ItemTypeSO.prefabReference with shared drop prefab.
    public static class ItemDropDefaultPrefabAssignEditor
    {
        private const string MenuPath = "Tools/Item/Assign Missing PrefabReference To DropItem Default";
        private const string DefaultDropPrefabPath = "Assets/Game/Item/Weapon/DroppedWeapon/DropItem Default.prefab";
        private static readonly string[] SearchFolders = { "Assets/Game/Item" };

        [MenuItem(MenuPath)]
        private static void AssignMissingPrefabReferences()
        {
            var defaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultDropPrefabPath);
            if (defaultPrefab == null)
            {
                Debug.LogError($"[{nameof(ItemDropDefaultPrefabAssignEditor)}] default prefab not found at '{DefaultDropPrefabPath}'");
                return;
            }

            var defaultGuid = AssetDatabase.AssetPathToGUID(DefaultDropPrefabPath);
            if (string.IsNullOrWhiteSpace(defaultGuid))
            {
                Debug.LogError($"[{nameof(ItemDropDefaultPrefabAssignEditor)}] failed to resolve GUID for '{DefaultDropPrefabPath}'");
                return;
            }

            var scanned = 0;
            var updated = 0;
            var alreadyAssigned = 0;
            var missingProperty = 0;

            foreach (var itemAsset in EnumerateItemTypeAssets())
            {
                if (itemAsset == null)
                    continue;

                scanned++;

                var serialized = new SerializedObject(itemAsset);
                var prefabReferenceProp = serialized.FindProperty("prefabReference");
                if (prefabReferenceProp == null)
                {
                    missingProperty++;
                    continue;
                }

                var guidProp = prefabReferenceProp.FindPropertyRelative("m_AssetGUID");
                var subObjectNameProp = prefabReferenceProp.FindPropertyRelative("m_SubObjectName");
                var subObjectTypeProp = prefabReferenceProp.FindPropertyRelative("m_SubObjectType");
                var subObjectGuidProp = prefabReferenceProp.FindPropertyRelative("m_SubObjectGUID");

                if (guidProp == null || subObjectNameProp == null || subObjectTypeProp == null)
                {
                    missingProperty++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(guidProp.stringValue))
                {
                    alreadyAssigned++;
                    continue;
                }

                guidProp.stringValue = defaultGuid;
                subObjectNameProp.stringValue = string.Empty;
                subObjectTypeProp.stringValue = string.Empty;
                if (subObjectGuidProp != null)
                    subObjectGuidProp.stringValue = string.Empty;

                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorUtility.SetDirty(itemAsset);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[{nameof(ItemDropDefaultPrefabAssignEditor)}] scanned={scanned}, updated={updated}, alreadyAssigned={alreadyAssigned}, missingProperty={missingProperty}, defaultPrefab='{DefaultDropPrefabPath}'");
        }

        private static IEnumerable<ItemTypeSO> EnumerateItemTypeAssets()
        {
            var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var typeName in GetItemTypeSearchNames())
            {
                var guids = AssetDatabase.FindAssets($"t:{typeName}", SearchFolders);
                for (var i = 0; i < guids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (string.IsNullOrWhiteSpace(path) || !visitedPaths.Add(path))
                        continue;

                    var item = AssetDatabase.LoadAssetAtPath<ItemTypeSO>(path);
                    if (item != null)
                        yield return item;
                }
            }
        }

        private static IEnumerable<string> GetItemTypeSearchNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(ItemTypeSO)
            };

            foreach (var type in TypeCache.GetTypesDerivedFrom<ItemTypeSO>())
            {
                if (type == null || type.IsAbstract)
                    continue;

                names.Add(type.Name);
            }

            return names;
        }
    }
}
