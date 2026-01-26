#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class TerrainTreeNavMeshMaskBaker
{
    private struct TreeMaskPrototype
    {
        public Vector3 size;
        public Vector3 center;
        public bool useCustom;
    }

    [MenuItem("Tools/Navigation/Bake NavMesh With Terrain Tree Mask (Temporary)")]
    private static void BakeWithTreeMask()
    {
        var surface = Object.FindAnyObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            Debug.LogWarning("No NavMeshSurface found in the active scene.");
            return;
        }

        var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        if (terrains.Length == 0)
        {
            Debug.LogWarning("No Terrain found in the active scene.");
            return;
        }

        var sources = CollectSurfaceSources(surface);
        if (sources == null)
        {
            Debug.LogWarning("Failed to collect NavMesh sources from the surface.");
            return;
        }

        var area = NavMesh.GetAreaFromName("Not Walkable");
        if (area < 0)
        {
            area = 1;
            Debug.LogWarning("NavMesh area 'Not Walkable' was not found. Falling back to area index 1.");
        }

        var treeSources = BuildTreeMaskSources(terrains, area);
        sources.AddRange(treeSources);

        var bounds = surface.collectObjects == CollectObjects.Volume
            ? new Bounds(surface.center, Abs(surface.size))
            : CalculateWorldBounds(surface, sources);

        var data = NavMeshBuilder.BuildNavMeshData(surface.GetBuildSettings(), sources, bounds,
            surface.transform.position, surface.transform.rotation);

        if (data == null)
        {
            Debug.LogWarning("NavMesh build returned null data.");
            return;
        }

        data.name = surface.gameObject.name;
        surface.RemoveData();
        surface.navMeshData = data;
        if (surface.isActiveAndEnabled)
        {
            surface.AddData();
        }

        EditorUtility.SetDirty(surface);
        Debug.Log($"NavMesh rebuilt with {treeSources.Count} terrain tree mask sources.");
    }

    private static List<NavMeshBuildSource> CollectSurfaceSources(NavMeshSurface surface)
    {
        var method = typeof(NavMeshSurface).GetMethod("CollectSources", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            return null;
        }

        return method.Invoke(surface, null) as List<NavMeshBuildSource>;
    }

    private static List<NavMeshBuildSource> BuildTreeMaskSources(Terrain[] terrains, int area)
    {
        var sources = new List<NavMeshBuildSource>();
        var prototypeMasks = new Dictionary<GameObject, TreeMaskPrototype>();

        foreach (var terrain in terrains)
        {
            var data = terrain.terrainData;
            if (data == null)
            {
                continue;
            }

            foreach (var proto in data.treePrototypes)
            {
                if (proto.prefab == null || prototypeMasks.ContainsKey(proto.prefab))
                {
                    continue;
                }

                prototypeMasks[proto.prefab] = GetPrefabMaskPrototype(proto.prefab);
            }

            var trees = data.treeInstances;
            for (var i = 0; i < trees.Length; i++)
            {
                var tree = trees[i];
                if (tree.prototypeIndex < 0 || tree.prototypeIndex >= data.treePrototypes.Length)
                {
                    continue;
                }

                var prefab = data.treePrototypes[tree.prototypeIndex].prefab;
                if (prefab == null || !prototypeMasks.TryGetValue(prefab, out var prototype))
                {
                    continue;
                }

                var size = ApplyTreeScale(prototype.size, tree);
                if (prototype.useCustom)
                {
                    size.x = Mathf.Max(size.x, 0.01f);
                    size.y = Mathf.Max(size.y, 0.01f);
                    size.z = Mathf.Max(size.z, 0.01f);
                }
                else
                {
                    size.x = Mathf.Max(size.x, 0.5f);
                    size.y = Mathf.Max(size.y, 2f);
                    size.z = Mathf.Max(size.z, 0.5f);
                }

                var worldPos = Vector3.Scale(tree.position, data.size) + terrain.transform.position;
                worldPos.y = terrain.SampleHeight(worldPos) + terrain.transform.position.y;

                var rotation = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
                var scaledCenter = ApplyTreeScale(prototype.center, tree);
                var center = worldPos + rotation * scaledCenter;

                var source = new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.ModifierBox,
                    transform = Matrix4x4.TRS(center, rotation, Vector3.one),
                    size = size,
                    area = area
                };

                sources.Add(source);
            }
        }

        return sources;
    }
    private static TreeMaskPrototype GetPrefabMaskPrototype(GameObject prefab)
    {
        var path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            return DefaultPrototype();
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var custom = root.GetComponentInChildren<NavMeshTreeMaskBounds>(true);
            if (custom != null)
            {
                return new TreeMaskPrototype
                {
                    size = SanitizeCustomSize(custom.Size),
                    center = custom.Center,
                    useCustom = true
                };
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return DefaultPrototype();
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var size = bounds.size;
            size.x = Mathf.Max(size.x, 0.5f);
            size.y = Mathf.Max(size.y, 2f);
            size.z = Mathf.Max(size.z, 0.5f);

            return new TreeMaskPrototype
            {
                size = size,
                center = bounds.center,
                useCustom = false
            };
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static TreeMaskPrototype DefaultPrototype()
    {
        return new TreeMaskPrototype
        {
            size = new Vector3(1f, 2f, 1f),
            center = new Vector3(0f, 1f, 0f),
            useCustom = false
        };
    }

    private static Vector3 SanitizeCustomSize(Vector3 size)
    {
        size.x = Mathf.Max(size.x, 0.01f);
        size.y = Mathf.Max(size.y, 0.01f);
        size.z = Mathf.Max(size.z, 0.01f);
        return size;
    }

    private static Vector3 ApplyTreeScale(Vector3 value, TreeInstance tree)
    {
        return new Vector3(value.x * tree.widthScale, value.y * tree.heightScale, value.z * tree.widthScale);
    }

    private static Vector3 Abs(Vector3 v)
    {
        return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }

    private static Bounds GetWorldBounds(Matrix4x4 mat, Bounds bounds)
    {
        var absAxisX = Abs(mat.MultiplyVector(Vector3.right));
        var absAxisY = Abs(mat.MultiplyVector(Vector3.up));
        var absAxisZ = Abs(mat.MultiplyVector(Vector3.forward));
        var worldPosition = mat.MultiplyPoint(bounds.center);
        var worldSize = absAxisX * bounds.size.x + absAxisY * bounds.size.y + absAxisZ * bounds.size.z;
        return new Bounds(worldPosition, worldSize);
    }

    private static Bounds CalculateWorldBounds(NavMeshSurface surface, List<NavMeshBuildSource> sources)
    {
        var worldToLocal = Matrix4x4.TRS(surface.transform.position, surface.transform.rotation, Vector3.one).inverse;
        var hasBounds = false;
        var result = new Bounds();

        foreach (var src in sources)
        {
            Bounds bounds;
            switch (src.shape)
            {
                case NavMeshBuildSourceShape.Mesh:
                {
                    if (src.sourceObject is Mesh mesh)
                    {
                        bounds = GetWorldBounds(worldToLocal * src.transform, mesh.bounds);
                    }
                    else
                    {
                        continue;
                    }

                    break;
                }
                case NavMeshBuildSourceShape.Terrain:
                {
                    if (src.sourceObject is TerrainData terrainData)
                    {
                        bounds = GetWorldBounds(worldToLocal * src.transform, new Bounds(0.5f * terrainData.size, terrainData.size));
                    }
                    else
                    {
                        continue;
                    }

                    break;
                }
                case NavMeshBuildSourceShape.Box:
                case NavMeshBuildSourceShape.Sphere:
                case NavMeshBuildSourceShape.Capsule:
                case NavMeshBuildSourceShape.ModifierBox:
                    bounds = GetWorldBounds(worldToLocal * src.transform, new Bounds(Vector3.zero, src.size));
                    break;
                default:
                    continue;
            }

            if (!hasBounds)
            {
                result = bounds;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(bounds);
            }
        }

        if (!hasBounds)
        {
            return new Bounds(surface.transform.position, Vector3.one);
        }

        result.Expand(0.1f);
        return result;
    }
}
#endif
