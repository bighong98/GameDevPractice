#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TH.Item;
using TH.Resource;
using UnityEditor;
using UnityEngine;

public static class OutfitPartIconGenerator
{
    private const string OutputIconsAddressKey = "outfit.icons_output.folder";
    private const string MaleTemplateAddressKey = "outfit.template.male";
    private const string FemaleTemplateAddressKey = "outfit.template.female";

    private const int IconSize = 512;
    private const float CameraPadding = 1.2f;
    private const float MinBoundsRadius = 0.05f;

    private static readonly string[] TemplateAddressKeys =
    {
        MaleTemplateAddressKey,
        FemaleTemplateAddressKey
    };

    [MenuItem("Tools/Outfit/Generate Missing Outfit Part Icons")]
    private static void GenerateMissingOutfitPartIcons()
    {
        var outputFolderPath = ResolvePathByAddressKey(OutputIconsAddressKey);
        if (string.IsNullOrEmpty(outputFolderPath))
            return;

        EnsureFolder(outputFolderPath);

        var templatePaths = ResolveTemplatePrefabPaths();
        if (templatePaths.Count == 0)
        {
            Debug.LogError("[OutfitPartIconGenerator] No template prefabs resolved.");
            return;
        }

        var existingIconNames = CollectExistingIconNames(outputFolderPath);
        var processedIconNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int created = 0;
        int skippedExisting = 0;
        int skippedInvalid = 0;
        int failed = 0;

        for (int t = 0; t < templatePaths.Count; t++)
        {
            var templatePath = templatePaths[t];
            var root = PrefabUtility.LoadPrefabContents(templatePath);
            try
            {
                var tags = root.GetComponentsInChildren<OutfitPartKeyTag>(true);
                for (int i = 0; i < tags.Length; i++)
                {
                    var tag = tags[i];
                    if (tag == null)
                    {
                        skippedInvalid++;
                        continue;
                    }

                    var renderer = tag.GetComponent<SkinnedMeshRenderer>();
                    if (renderer == null)
                    {
                        skippedInvalid++;
                        continue;
                    }

                    var key = ResolveOutfitKey(tag);
                    if (key == null || string.IsNullOrWhiteSpace(key.id))
                    {
                        skippedInvalid++;
                        continue;
                    }

                    if (!OutfitAutoGenerationRules.IsAutoGeneratableOutfitKey(key))
                    {
                        skippedInvalid++;
                        continue;
                    }

                    var iconName = SanitizeFileName(key.id);
                    if (string.IsNullOrWhiteSpace(iconName))
                    {
                        skippedInvalid++;
                        continue;
                    }

                    if (!processedIconNames.Add(iconName))
                        continue;

                    if (existingIconNames.Contains(iconName))
                    {
                        skippedExisting++;
                        continue;
                    }

                    var iconAssetPath = $"{outputFolderPath}/{iconName}.png";
                    if (TryRenderIcon(renderer, iconAssetPath, IconSize, out var errorMessage))
                    {
                        created++;
                        existingIconNames.Add(iconName);
                    }
                    else
                    {
                        failed++;
                        Debug.LogWarning($"[OutfitPartIconGenerator] Failed to generate icon '{iconName}' from '{templatePath}': {errorMessage}");
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[OutfitPartIconGenerator] Completed. Templates={templatePaths.Count}, Created={created}, SkippedExisting={skippedExisting}, SkippedInvalid={skippedInvalid}, Failed={failed}");
    }

    private static bool TryRenderIcon(SkinnedMeshRenderer sourceRenderer, string iconAssetPath, int size, out string errorMessage)
    {
        errorMessage = null;

        if (sourceRenderer.sharedMesh == null)
        {
            errorMessage = "Source renderer mesh is null.";
            return false;
        }

        var bakedMesh = new Mesh
        {
            name = $"{sourceRenderer.name}_IconBake"
        };

        var previewGO = new GameObject("OutfitPartIconPreview")
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        PreviewRenderUtility preview = null;

        try
        {
            sourceRenderer.BakeMesh(bakedMesh, true);
            if (bakedMesh.vertexCount <= 0)
            {
                errorMessage = "Baked mesh has no vertices.";
                return false;
            }

            var filter = previewGO.AddComponent<MeshFilter>();
            var meshRenderer = previewGO.AddComponent<MeshRenderer>();
            filter.sharedMesh = bakedMesh;
            meshRenderer.sharedMaterials = sourceRenderer.sharedMaterials;

            // Center baked mesh for stable framing regardless of source hierarchy transform.
            previewGO.transform.position = -bakedMesh.bounds.center;
            previewGO.transform.rotation = Quaternion.identity;
            previewGO.transform.localScale = Vector3.one;

            preview = new PreviewRenderUtility();
            preview.AddSingleGO(previewGO);

            ConfigureLighting(preview);

            var renderBounds = GetRendererBounds(previewGO);
            SetupCamera(preview.camera, renderBounds);

            var target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
            try
            {
                var previousTarget = preview.camera.targetTexture;
                var previousActive = RenderTexture.active;

                preview.camera.targetTexture = target;
                preview.camera.Render();

                RenderTexture.active = target;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                try
                {
                    texture.ReadPixels(new Rect(0, 0, size, size), 0, 0, false);
                    texture.Apply(false, false);

                    var png = texture.EncodeToPNG();
                    WriteAssetBytes(iconAssetPath, png);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                RenderTexture.active = previousActive;
                preview.camera.targetTexture = previousTarget;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
            }

            AssetDatabase.ImportAsset(iconAssetPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureIconImporter(iconAssetPath);

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
        finally
        {
            if (preview != null)
            {
                preview.Cleanup();
            }

            UnityEngine.Object.DestroyImmediate(previewGO);
            UnityEngine.Object.DestroyImmediate(bakedMesh);
        }
    }

    private static OutfitKeySO ResolveOutfitKey(OutfitPartKeyTag tag)
    {
        var serializedObject = new SerializedObject(tag);
        var referenceProp = serializedObject.FindProperty("outfitKeyReference");
        var guidProp = referenceProp?.FindPropertyRelative("m_AssetGUID");
        var guid = guidProp?.stringValue;

        if (string.IsNullOrEmpty(guid))
            return null;

        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
            return null;

        return AssetDatabase.LoadAssetAtPath<OutfitKeySO>(path);
    }

    private static void ConfigureLighting(PreviewRenderUtility preview)
    {
        preview.ambientColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        var keyLight = preview.lights[0];
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.15f;
        keyLight.transform.rotation = Quaternion.Euler(28f, 150f, 0f);

        var fillLight = preview.lights[1];
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.75f;
        fillLight.transform.rotation = Quaternion.Euler(340f, 210f, 0f);
    }

    private static void SetupCamera(Camera camera, Bounds bounds)
    {
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.fieldOfView = 30f;

        var radius = Mathf.Max(bounds.extents.magnitude, MinBoundsRadius);
        var halfFov = Mathf.Max(1f, camera.fieldOfView * 0.5f) * Mathf.Deg2Rad;

        // Use sphere framing against FOV to avoid clipping at corners.
        var distance = (radius * CameraPadding) / Mathf.Sin(halfFov);
        distance = Mathf.Max(distance, 0.15f);

        var center = bounds.center;
        var cameraPosition = center + (Vector3.forward * distance);

        camera.transform.position = cameraPosition;
        camera.transform.rotation = Quaternion.LookRotation(center - cameraPosition, Vector3.up);

        camera.nearClipPlane = Mathf.Max(0.01f, distance - (radius * 2f));
        camera.farClipPlane = distance + (radius * 4f);
    }

    private static Bounds GetRendererBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, Vector3.one * MinBoundsRadius);

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        if (bounds.extents.magnitude < MinBoundsRadius)
            bounds.extents = Vector3.one * MinBoundsRadius;

        return bounds;
    }

    private static HashSet<string> CollectExistingIconNames(string outputFolderPath)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { outputFolderPath });

        for (int i = 0; i < guids.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(name))
                result.Add(name);
        }

        return result;
    }

    private static void ConfigureIconImporter(string iconAssetPath)
    {
        if (!(AssetImporter.GetAtPath(iconAssetPath) is TextureImporter importer))
            return;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            changed = true;
        }

        if (importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (importer.crunchedCompression)
        {
            importer.crunchedCompression = false;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
    }

    private static List<string> ResolveTemplatePrefabPaths()
    {
        var result = new List<string>();

        for (int i = 0; i < TemplateAddressKeys.Length; i++)
        {
            var path = ResolvePathByAddressKey(TemplateAddressKeys[i]);
            if (string.IsNullOrEmpty(path))
                continue;

            if (!result.Contains(path))
                result.Add(path);
        }

        return result;
    }

    private static string ResolvePathByAddressKey(string addressKey)
    {
        return EditorAddressablePathResolver.ResolvePathByMapIdOrAddressKey(
            addressKey,
            nameof(OutfitPartIconGenerator));
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var name = Path.GetFileName(folderPath);

        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return;

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static void WriteAssetBytes(string assetPath, byte[] data)
    {
        var absolutePath = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllBytes(absolutePath, data);
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var chars = raw.ToCharArray();
        var invalid = Path.GetInvalidFileNameChars();

        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }

        return new string(chars).Trim();
    }
}
#endif
