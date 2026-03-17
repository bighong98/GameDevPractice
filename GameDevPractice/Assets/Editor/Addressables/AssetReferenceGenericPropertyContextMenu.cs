#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TH.Resource;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[InitializeOnLoad]
public static class AssetReferenceGenericPropertyContextMenu
{
    private static readonly Dictionary<string, List<Type>> TypesByName = BuildTypeMap();

    static AssetReferenceGenericPropertyContextMenu()
    {
        EditorApplication.contextualPropertyMenu -= OnContextualPropertyMenu;
        EditorApplication.contextualPropertyMenu += OnContextualPropertyMenu;
    }

    private static void OnContextualPropertyMenu(GenericMenu menu, SerializedProperty property)
    {
        if (!IsAssetReferenceGenericField(property))
        {
            return;
        }

        var propertyPath = property.propertyPath;
        var targetObjects = property.serializedObject.targetObjects;

        menu.AddItem(new GUIContent("Validate"), false, () => Validate(targetObjects, propertyPath));
        menu.AddItem(new GUIContent("Delete"), false, () => Delete(targetObjects, propertyPath));
    }

    private static bool IsAssetReferenceGenericField(SerializedProperty property)
    {
        if (property == null)
        {
            return false;
        }

        if (property.FindPropertyRelative("m_AssetGUID") == null)
        {
            return false;
        }

        if (!TryResolvePropertyType(property.type, out var resolvedType))
        {
            return true;
        }

        return IsAssetReferenceGenericDerivedType(resolvedType);
    }

    private static bool TryResolvePropertyType(string typeName, out Type resolvedType)
    {
        resolvedType = null;
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        if (!TypesByName.TryGetValue(typeName, out var candidates) || candidates == null || candidates.Count == 0)
        {
            return false;
        }

        resolvedType = candidates.FirstOrDefault(IsAssetReferenceGenericDerivedType);
        return resolvedType != null;
    }

    private static bool IsAssetReferenceGenericDerivedType(Type type)
    {
        var current = type;
        while (current != null && current != typeof(object))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AssetReferenceGeneric<>))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }

    private static Dictionary<string, List<Type>> BuildTypeMap()
    {
        var map = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var types = GetAssemblyTypes(assembly);
            foreach (var type in types)
            {
                if (type == null)
                {
                    continue;
                }

                if (!map.TryGetValue(type.Name, out var list))
                {
                    list = new List<Type>();
                    map[type.Name] = list;
                }

                list.Add(type);
            }
        }

        return map;
    }

    private static IEnumerable<Type> GetAssemblyTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(type => type != null);
        }
    }

    private static void Validate(UnityEngine.Object[] targetObjects, string propertyPath)
    {
        foreach (var target in targetObjects)
        {
            if (target == null)
            {
                continue;
            }

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyPath);
            var guidProperty = property?.FindPropertyRelative("m_AssetGUID");
            if (guidProperty == null)
            {
                continue;
            }

            var guid = guidProperty.stringValue;
            if (string.IsNullOrWhiteSpace(guid))
            {
                Debug.LogWarning($"[AssetReferenceGeneric] Validate failed: guid is empty. property: {propertyPath}", target);
                continue;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            var existsInProject = !string.IsNullOrWhiteSpace(path) && AssetDatabase.LoadMainAssetAtPath(path) != null;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var existsInAddressables = settings?.FindAssetEntry(guid) != null;
            var isValid = existsInProject && existsInAddressables;

            Debug.Log(
                $"[AssetReferenceGeneric] Validate result - property: {propertyPath}, guid: {guid}, valid: {isValid}, existsInProject: {existsInProject}, existsInAddressables: {existsInAddressables}, path: {(string.IsNullOrWhiteSpace(path) ? "<missing>" : path)}",
                target);
        }
    }

    private static void Delete(UnityEngine.Object[] targetObjects, string propertyPath)
    {
        Undo.RecordObjects(targetObjects, "Delete AssetReferenceGeneric");

        foreach (var target in targetObjects)
        {
            if (target == null)
            {
                continue;
            }

            var serializedObject = new SerializedObject(target);
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                continue;
            }

            if (!TryClearAssetReference(property, out var previousGuid))
            {
                continue;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);

            Debug.Log($"[AssetReferenceGeneric] Delete complete - property: {propertyPath}, cleared guid: {previousGuid}", target);
        }
    }

    private static bool TryClearAssetReference(SerializedProperty property, out string previousGuid)
    {
        previousGuid = string.Empty;

        var guidProperty = property.FindPropertyRelative("m_AssetGUID");
        if (guidProperty == null)
        {
            return false;
        }

        previousGuid = guidProperty.stringValue;
        guidProperty.stringValue = string.Empty;

        ClearString(property, "m_SubObjectName");
        ClearString(property, "m_SubObjectType");
        ClearString(property, "m_SubObjectGUID");

        var editorChanged = property.FindPropertyRelative("m_EditorAssetChanged");
        if (editorChanged != null && editorChanged.propertyType == SerializedPropertyType.Boolean)
        {
            editorChanged.boolValue = true;
        }

        return true;
    }

    private static void ClearString(SerializedProperty property, string relativeName)
    {
        var child = property.FindPropertyRelative(relativeName);
        if (child != null && child.propertyType == SerializedPropertyType.String)
        {
            child.stringValue = string.Empty;
        }
    }
}
#endif
