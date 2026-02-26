#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TH.Control.Movement.Editor
{
    public static class CharacterFootIKCurveBakerEditor
    {
        private const string MenuPath = "Tools/Control/Foot IK/Bake IK Curves From Foot Up-Down (Unarmed)";

        private const string TargetRootFolder = "Assets/Asset Packs/Animations/Unarmed";

        private const string LeftUpDownProperty = "Left Foot Up-Down";
        private const string RightUpDownProperty = "Right Foot Up-Down";
        private const string LeftFootTYProperty = "LeftFootT.y";
        private const string RightFootTYProperty = "RightFootT.y";

        private const string FootIkWeightCurveName = "FootIKWeight";
        private const string LeftFootPlantCurveName = "LeftFootPlant";
        private const string RightFootPlantCurveName = "RightFootPlant";

        private const float LiftStartRatio = 0.25f;
        private const float LiftEndRatio = 0.6f;
        private const float PlantWeightPower = 1.8f;
        private const float SampleRate = 60f;

        private static readonly HashSet<string> TargetClipNames = new(StringComparer.Ordinal)
        {
            "HumanoidIdle",
            "HumanoidWalk",
            "HumanoidRun"
        };

        [MenuItem(MenuPath)]
        private static void BakeCurves()
        {
            string[] modelGuids = AssetDatabase.FindAssets("t:Model", new[] { TargetRootFolder });
            if (modelGuids.Length == 0)
            {
                Debug.LogWarning($"[{nameof(CharacterFootIKCurveBakerEditor)}] No model assets found under '{TargetRootFolder}'.");
                return;
            }

            int updatedImporterCount = 0;
            int updatedClipCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in modelGuids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (TryBakeCurvesForModel(assetPath, out int clipCount))
                    {
                        updatedImporterCount++;
                        updatedClipCount += clipCount;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[{nameof(CharacterFootIKCurveBakerEditor)}] Completed. updatedImporters={updatedImporterCount}, updatedClips={updatedClipCount}");
        }

        private static bool TryBakeCurvesForModel(string assetPath, out int updatedClipCount)
        {
            updatedClipCount = 0;

            if (AssetImporter.GetAtPath(assetPath) is not ModelImporter importer)
            {
                return false;
            }

            ModelImporterClipAnimation[] sourceClips = importer.clipAnimations;
            if (sourceClips == null || sourceClips.Length == 0)
            {
                sourceClips = importer.defaultClipAnimations;
            }

            if (sourceClips == null || sourceClips.Length == 0)
            {
                return false;
            }

            AnimationClip[] clipsAtPath = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(c => !string.Equals(c.name, "__preview__Take 001", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (clipsAtPath.Length == 0)
            {
                return false;
            }

            var editedClips = new ModelImporterClipAnimation[sourceClips.Length];
            bool importerChanged = false;

            for (int i = 0; i < sourceClips.Length; i++)
            {
                ModelImporterClipAnimation clipSettings = sourceClips[i];
                editedClips[i] = clipSettings;

                if (!TargetClipNames.Contains(clipSettings.name))
                {
                    continue;
                }

                AnimationClip clipAsset = clipsAtPath.FirstOrDefault(c => string.Equals(c.name, clipSettings.name, StringComparison.Ordinal));
                if (clipAsset == null)
                {
                    Debug.LogWarning($"[{nameof(CharacterFootIKCurveBakerEditor)}] Clip asset not found. path='{assetPath}', clip='{clipSettings.name}'");
                    continue;
                }

                if (!TryGetSourceCurve(clipAsset, LeftUpDownProperty, LeftFootTYProperty, out var leftUpDown) ||
                    !TryGetSourceCurve(clipAsset, RightUpDownProperty, RightFootTYProperty, out var rightUpDown))
                {
                    Debug.LogWarning($"[{nameof(CharacterFootIKCurveBakerEditor)}] Missing source curves. path='{assetPath}', clip='{clipSettings.name}'");
                    continue;
                }

                AnimationCurve leftPlantCurve = BuildPlantCurve(clipAsset.length, leftUpDown);
                AnimationCurve rightPlantCurve = BuildPlantCurve(clipAsset.length, rightUpDown);
                AnimationCurve footIkWeightCurve = BuildFootIkWeightCurve(clipAsset.length, leftUpDown, rightUpDown);

                ClipAnimationInfoCurve[] curves = clipSettings.curves ?? Array.Empty<ClipAnimationInfoCurve>();
                curves = UpsertCurve(curves, LeftFootPlantCurveName, leftPlantCurve);
                curves = UpsertCurve(curves, RightFootPlantCurveName, rightPlantCurve);
                curves = UpsertCurve(curves, FootIkWeightCurveName, footIkWeightCurve);

                clipSettings.curves = curves;
                editedClips[i] = clipSettings;
                importerChanged = true;
                updatedClipCount++;
            }

            if (!importerChanged)
            {
                return false;
            }

            importer.clipAnimations = editedClips;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            return true;
        }

        private static bool TryGetSourceCurve(AnimationClip clip, string primaryProperty, string fallbackProperty, out AnimationCurve curve)
        {
            curve = null;
            var bindings = AnimationUtility.GetCurveBindings(clip);

            EditorCurveBinding? selectedBinding = bindings.FirstOrDefault(b =>
                string.Equals(b.path, string.Empty, StringComparison.Ordinal) &&
                b.type == typeof(Animator) &&
                string.Equals(b.propertyName, primaryProperty, StringComparison.Ordinal));

            if (selectedBinding == null || selectedBinding.Value.propertyName == null)
            {
                selectedBinding = bindings.FirstOrDefault(b =>
                    string.Equals(b.path, string.Empty, StringComparison.Ordinal) &&
                    b.type == typeof(Animator) &&
                    string.Equals(b.propertyName, fallbackProperty, StringComparison.Ordinal));
            }

            if (selectedBinding == null || selectedBinding.Value.propertyName == null)
            {
                return false;
            }

            curve = AnimationUtility.GetEditorCurve(clip, selectedBinding.Value);
            return curve != null;
        }

private static AnimationCurve BuildPlantCurve(float clipLength, AnimationCurve upDownCurve)
        {
            var result = new AnimationCurve();
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(clipLength * SampleRate) + 1);
            (float min, float max) = CalculateRange(upDownCurve, clipLength, sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount == 1 ? 0f : (i / (float)(sampleCount - 1)) * clipLength;
                float value = upDownCurve.Evaluate(t);
                float plantWeight = EvaluatePlantWeight(value, min, max);
                result.AddKey(t, plantWeight);
            }

            return result;
        }

private static AnimationCurve BuildFootIkWeightCurve(float clipLength, AnimationCurve leftUpDown, AnimationCurve rightUpDown)
        {
            var result = new AnimationCurve();
            int sampleCount = Mathf.Max(2, Mathf.CeilToInt(clipLength * SampleRate) + 1);
            (float leftMin, float leftMax) = CalculateRange(leftUpDown, clipLength, sampleCount);
            (float rightMin, float rightMax) = CalculateRange(rightUpDown, clipLength, sampleCount);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount == 1 ? 0f : (i / (float)(sampleCount - 1)) * clipLength;
                float leftPlant = EvaluatePlantWeight(leftUpDown.Evaluate(t), leftMin, leftMax);
                float rightPlant = EvaluatePlantWeight(rightUpDown.Evaluate(t), rightMin, rightMax);

                float footIkWeight = Mathf.Max(leftPlant, rightPlant);
                result.AddKey(t, footIkWeight);
            }

            return result;
        }

        private static (float min, float max) CalculateRange(AnimationCurve curve, float clipLength, int sampleCount)
        {
            float min = float.PositiveInfinity;
            float max = float.NegativeInfinity;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = sampleCount == 1 ? 0f : (i / (float)(sampleCount - 1)) * clipLength;
                float value = curve.Evaluate(t);
                if (value < min) min = value;
                if (value > max) max = value;
            }

            if (!float.IsFinite(min) || !float.IsFinite(max))
            {
                return (0f, 1f);
            }

            if (Mathf.Abs(max - min) < 0.0001f)
            {
                max = min + 0.0001f;
            }

            return (min, max);
        }

        private static float EvaluatePlantWeight(float value, float min, float max)
        {
            float liftStart = Mathf.Lerp(min, max, LiftStartRatio);
            float liftEnd = Mathf.Lerp(min, max, LiftEndRatio);
            float lifted = Mathf.InverseLerp(liftStart, liftEnd, value);
            float smoothLifted = lifted * lifted * (3f - 2f * lifted);
            float planted = 1f - smoothLifted;
            return Mathf.Pow(Mathf.Clamp01(planted), PlantWeightPower);
        }

        
private static ClipAnimationInfoCurve[] UpsertCurve(ClipAnimationInfoCurve[] source, string curveName, AnimationCurve curve)
        {
            var list = source != null ? source.ToList() : new List<ClipAnimationInfoCurve>();
            int index = list.FindIndex(c => string.Equals(c.name, curveName, StringComparison.Ordinal));

            var next = new ClipAnimationInfoCurve
            {
                name = curveName,
                curve = curve
            };

            if (index >= 0)
            {
                list[index] = next;
            }
            else
            {
                list.Add(next);
            }

            return list.ToArray();
        }
    }
}
#endif
