#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TH.Item;
using UnityEditor;
using UnityEngine;

public static class OutfitRendererToggleReadinessTool
{
    private const string MaleTemplateAddressKey = "outfit.template.male";
    private const string FemaleTemplateAddressKey = "outfit.template.female";

    private static readonly string[] TemplatePrefabAddressKeys =
    {
        MaleTemplateAddressKey,
        FemaleTemplateAddressKey
    };

    [MenuItem("Tools/Outfit/Audit Renderer Toggle Readiness (BasicHero Templates+Variants)")]
    private static void AuditRendererToggleReadiness()
    {
        var targetPrefabs = CollectTargetPrefabPaths();
        if (targetPrefabs.Count == 0)
        {
            Debug.LogWarning($"[{nameof(OutfitRendererToggleReadinessTool)}] No target prefabs found.");
            return;
        }

        int totalTags = 0;
        int tagsWithoutRenderer = 0;
        int tagsWithInactiveSelfOrAncestor = 0;
        int tagsWithPerFrameComponents = 0;
        int tagsCurrentlyVisibleByRenderer = 0;

        var noRendererDetails = new List<string>();
        var inactiveDetails = new List<string>();
        var perFrameDetails = new List<string>();
        var visibleDetails = new List<string>();
        var perPrefabSummary = new List<string>(targetPrefabs.Count);

        foreach (var prefabPath in targetPrefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var tags = root.GetComponentsInChildren<OutfitPartKeyTag>(true);
                totalTags += tags.Length;
                int localNoRenderer = 0;
                int localInactive = 0;
                int localPerFrame = 0;
                int localVisible = 0;

                foreach (var tag in tags)
                {
                    if (tag == null)
                        continue;

                    var tagPath = BuildHierarchyPath(tag.transform, root.transform);

                    var renderers = tag.GetComponentsInChildren<Renderer>(true);
                    if (renderers == null || renderers.Length == 0)
                    {
                        tagsWithoutRenderer++;
                        localNoRenderer++;
                        noRendererDetails.Add($"{prefabPath} :: {tagPath}");
                    }

                    var inactiveTransform = FindFirstInactiveSelfInHierarchy(tag.transform, root.transform);
                    if (inactiveTransform != null)
                    {
                        tagsWithInactiveSelfOrAncestor++;
                        localInactive++;
                        inactiveDetails.Add($"{prefabPath} :: {tagPath} (inactiveAt={BuildHierarchyPath(inactiveTransform, root.transform)})");
                    }

                    if (HasPerFrameLikeComponents(tag.transform, out var componentNames))
                    {
                        tagsWithPerFrameComponents++;
                        localPerFrame++;
                        perFrameDetails.Add($"{prefabPath} :: {tagPath} (components={string.Join(", ", componentNames)})");
                    }

                    if (IsRendererVisible(tag.transform))
                    {
                        tagsCurrentlyVisibleByRenderer++;
                        localVisible++;
                        visibleDetails.Add($"{prefabPath} :: {tagPath}");
                    }
                }

                perPrefabSummary.Add($"{prefabPath} :: tags={tags.Length}, visibleByRenderer={localVisible}, noRenderer={localNoRenderer}, inactiveHierarchy={localInactive}, perFrame={localPerFrame}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log(
            $"[{nameof(OutfitRendererToggleReadinessTool)}] Audit completed. " +
            $"Prefabs={targetPrefabs.Count}, OutfitPartKeyTags={totalTags}, " +
            $"VisibleByRenderer={tagsCurrentlyVisibleByRenderer}, NoRenderer={tagsWithoutRenderer}, InactiveHierarchy={tagsWithInactiveSelfOrAncestor}, PerFrameComponents={tagsWithPerFrameComponents}");

        LogSampleList("NoRenderer", noRendererDetails);
        LogSampleList("InactiveHierarchy", inactiveDetails);
        LogSampleList("PerFrameComponents", perFrameDetails);
        LogSampleList("VisibleByRenderer", visibleDetails);

        WriteAuditReport(
            targetPrefabs.Count,
            totalTags,
            tagsCurrentlyVisibleByRenderer,
            tagsWithoutRenderer,
            tagsWithInactiveSelfOrAncestor,
            tagsWithPerFrameComponents,
            perPrefabSummary,
            noRendererDetails,
            inactiveDetails,
            perFrameDetails,
            visibleDetails);
    }

    [MenuItem("Tools/Outfit/Fix Renderer Toggle Readiness (Activate Outfit Tag Hierarchy)")]
    private static void FixRendererToggleReadiness()
    {
        var targetPrefabs = CollectTargetPrefabPaths();
        if (targetPrefabs.Count == 0)
        {
            Debug.LogWarning($"[{nameof(OutfitRendererToggleReadinessTool)}] No target prefabs found.");
            return;
        }

        int changedPrefabs = 0;
        int changedObjects = 0;
        int disabledRenderers = 0;

        foreach (var prefabPath in targetPrefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            bool prefabChanged = false;
            try
            {
                var renderersToDisable = CollectRenderersFromInitiallyInactiveHierarchy(root.transform);

                var tags = root.GetComponentsInChildren<OutfitPartKeyTag>(true);
                foreach (var tag in tags)
                {
                    if (tag == null)
                        continue;

                    var changedCount = ActivateTagHierarchy(tag.transform, root.transform);
                    if (changedCount <= 0)
                        continue;

                    changedObjects += changedCount;
                    prefabChanged = true;
                }

                var disabledCount = DisableRenderers(renderersToDisable);
                if (disabledCount > 0)
                {
                    disabledRenderers += disabledCount;
                    prefabChanged = true;
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    changedPrefabs++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[{nameof(OutfitRendererToggleReadinessTool)}] Fix completed. " +
            $"TargetPrefabs={targetPrefabs.Count}, ChangedPrefabs={changedPrefabs}, ActivatedObjects={changedObjects}, DisabledRenderers={disabledRenderers}");
    }

    private static List<string> CollectTargetPrefabPaths()
    {
        var templatePaths = ResolveTemplatePrefabPaths();
        if (templatePaths.Count == 0)
            return new List<string>();

        var templateSet = new HashSet<string>(templatePaths, StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(templatePaths);
        var searchFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < templatePaths.Count; i++)
        {
            var folder = Path.GetDirectoryName(templatePaths[i])?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder))
                searchFolders.Add(folder);
        }

        if (searchFolders.Count == 0)
        {
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        var allPrefabGuids = AssetDatabase.FindAssets("t:Prefab", searchFolders.ToArray());
        foreach (var guid in allPrefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) || templateSet.Contains(path))
                continue;

            if (!IsVariantOfAnyTemplate(path, templateSet))
                continue;

            result.Add(path);
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static List<string> ResolveTemplatePrefabPaths()
    {
        var paths = new List<string>();

        for (int i = 0; i < TemplatePrefabAddressKeys.Length; i++)
        {
            var path = ResolvePathByAddressKey(TemplatePrefabAddressKeys[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[{nameof(OutfitRendererToggleReadinessTool)}] Address key points to missing/non-prefab asset: key={TemplatePrefabAddressKeys[i]}, path={path}");
                continue;
            }

            if (!paths.Contains(path))
                paths.Add(path);
        }

        if (paths.Count == 0)
            Debug.LogError($"[{nameof(OutfitRendererToggleReadinessTool)}] No template prefab path resolved.");

        return paths;
    }

    private static string ResolvePathByAddressKey(string addressKey)
    {
        return EditorAddressablePathResolver.ResolvePathByMapIdOrAddressKey(
            addressKey,
            nameof(OutfitRendererToggleReadinessTool));
    }

    private static bool IsVariantOfAnyTemplate(string prefabPath, HashSet<string> templateSet)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (PrefabUtility.GetPrefabAssetType(root) != PrefabAssetType.Variant)
                return false;

            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            while (source != null)
            {
                var sourcePath = AssetDatabase.GetAssetPath(source);
                if (!string.IsNullOrEmpty(sourcePath) && templateSet.Contains(sourcePath))
                    return true;

                source = PrefabUtility.GetCorrespondingObjectFromSource(source);
            }

            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int ActivateTagHierarchy(Transform tagTransform, Transform prefabRoot)
    {
        int changed = 0;
        var cursor = tagTransform;

        while (cursor != null)
        {
            if (!cursor.gameObject.activeSelf)
            {
                cursor.gameObject.SetActive(true);
                changed++;
            }

            if (cursor == prefabRoot)
                break;

            cursor = cursor.parent;
        }

        return changed;
    }

    private static HashSet<Renderer> CollectRenderersFromInitiallyInactiveHierarchy(Transform prefabRoot)
    {
        var result = new HashSet<Renderer>();
        if (prefabRoot == null)
            return result;

        var renderers = prefabRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null || renderer.gameObject == null)
                continue;

            if (!renderer.gameObject.activeInHierarchy)
                result.Add(renderer);
        }

        return result;
    }

    private static int DisableRenderers(IEnumerable<Renderer> renderers)
    {
        if (renderers == null)
            return 0;

        int changed = 0;
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            renderer.enabled = false;
            changed++;
        }

        return changed;
    }

    private static Transform FindFirstInactiveSelfInHierarchy(Transform child, Transform prefabRoot)
    {
        var cursor = child;
        while (cursor != null)
        {
            if (!cursor.gameObject.activeSelf)
                return cursor;

            if (cursor == prefabRoot)
                break;

            cursor = cursor.parent;
        }

        return null;
    }

    private static bool HasPerFrameLikeComponents(Transform root, out List<string> componentNames)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        var animators = root.GetComponentsInChildren<Animator>(true);
        if (animators != null && animators.Length > 0)
            set.Add(nameof(Animator));

        var animations = root.GetComponentsInChildren<Animation>(true);
        if (animations != null && animations.Length > 0)
            set.Add(nameof(Animation));

        var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var behaviour in behaviours)
        {
            if (behaviour == null)
                continue;

            var type = behaviour.GetType();
            if (DeclaresPerFrameMethod(type))
                set.Add(type.Name);
        }

        componentNames = set.OrderBy(x => x, StringComparer.Ordinal).ToList();
        return componentNames.Count > 0;
    }

    private static bool DeclaresPerFrameMethod(Type type)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        for (var cursor = type; cursor != null && cursor != typeof(MonoBehaviour); cursor = cursor.BaseType)
        {
            if (cursor.GetMethod("Update", Flags) != null)
                return true;
            if (cursor.GetMethod("LateUpdate", Flags) != null)
                return true;
            if (cursor.GetMethod("FixedUpdate", Flags) != null)
                return true;
        }

        return false;
    }

    private static bool IsRendererVisible(Transform root)
    {
        if (root == null)
            return false;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;

            if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private static string BuildHierarchyPath(Transform target, Transform root)
    {
        if (target == null)
            return "<null>";

        var names = new List<string>();
        var cursor = target;
        while (cursor != null)
        {
            names.Add(cursor.name);
            if (cursor == root)
                break;
            cursor = cursor.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    private static void LogSampleList(string label, List<string> entries)
    {
        if (entries == null || entries.Count == 0)
            return;

        var previewCount = Mathf.Min(20, entries.Count);
        var preview = string.Join("\n", entries.Take(previewCount));
        Debug.LogWarning(
            $"[{nameof(OutfitRendererToggleReadinessTool)}][{label}] Count={entries.Count} (showing {previewCount})\n{preview}");
    }

    private static void WriteAuditReport(
        int prefabCount,
        int totalTags,
        int visibleByRenderer,
        int noRenderer,
        int inactiveHierarchy,
        int perFrame,
        List<string> perPrefabSummary,
        List<string> noRendererDetails,
        List<string> inactiveDetails,
        List<string> perFrameDetails,
        List<string> visibleDetails)
    {
        var lines = new List<string>
        {
            $"[{nameof(OutfitRendererToggleReadinessTool)}] Audit Report",
            $"Prefabs={prefabCount}, OutfitPartKeyTags={totalTags}, VisibleByRenderer={visibleByRenderer}, NoRenderer={noRenderer}, InactiveHierarchy={inactiveHierarchy}, PerFrameComponents={perFrame}",
            string.Empty,
            "[PerPrefabSummary]"
        };

        lines.AddRange(perPrefabSummary.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        lines.Add(string.Empty);
        lines.Add("[NoRendererDetails]");
        lines.AddRange(noRendererDetails.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        lines.Add(string.Empty);
        lines.Add("[InactiveHierarchyDetails]");
        lines.AddRange(inactiveDetails.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        lines.Add(string.Empty);
        lines.Add("[PerFrameDetails]");
        lines.AddRange(perFrameDetails.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        lines.Add(string.Empty);
        lines.Add("[VisibleByRendererDetails]");
        lines.AddRange(visibleDetails.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));

        const string reportPath = "Temp/OutfitRendererToggleAuditReport.txt";
        Directory.CreateDirectory("Temp");
        File.WriteAllLines(reportPath, lines);
        Debug.Log($"[{nameof(OutfitRendererToggleReadinessTool)}] Audit report saved: {reportPath}");
    }
}
#endif
