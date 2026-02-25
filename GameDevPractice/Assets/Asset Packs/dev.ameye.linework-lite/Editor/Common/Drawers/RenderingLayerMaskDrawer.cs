using System;
using System.Collections.Generic;
using LineworkLite.Common.Attributes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace LineworkLite.Editor.Common.Drawers
{
    [CustomPropertyDrawer(typeof(RenderingLayerMaskAttribute))]
    public sealed class RenderingLayerMaskDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attr = (RenderingLayerMaskAttribute) attribute;
            var maskNames = GetMaskNamesForDisplay(GetRenderingLayerMaskNames(), property.uintValue, out var hasUndefinedLayer);
            var selectedLayer = GetSelectedLayerIndex(property.uintValue, maskNames.Count);

            var popup = new PopupField<string>(attr.ShowLabel ? property.displayName : string.Empty, maskNames, selectedLayer);
            popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            popup.RegisterValueChangedCallback(_ => SetValue(popup.index, property));

            if (!hasUndefinedLayer)
            {
                return popup;
            }

            var container = new VisualElement();
            container.Add(popup);
            container.Add(new HelpBox(
                "One or more of the Rendering Layers is not defined in the Universal Global Settings asset.",
                HelpBoxMessageType.Warning));
            return container;
        }

        private static void SetValue(int selectedLayer, SerializedProperty property)
        {
            property.uintValue = ToSingleLayerMask(selectedLayer);
            property.serializedObject.ApplyModifiedProperties();
        }

        private readonly GUIContent renderingLayerMaskContent = EditorGUIUtility.TrTextContent(
            "Rendering Layers",
            "Specify the rendering layer mask for this projector. Unity renders decals on all meshes where at least one Rendering Layer value matches."
        );

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (RenderingLayerMaskAttribute) attribute;
            var maskNames = GetMaskNamesForDisplay(GetRenderingLayerMaskNames(), property.uintValue, out var hasUndefinedLayer);
            var selectedLayer = GetSelectedLayerIndex(property.uintValue, maskNames.Count);
            var popupNames = maskNames.ToArray();

            if (hasUndefinedLayer)
            {
                EditorGUILayout.HelpBox(
                    "One or more of the Rendering Layers is not defined in the Universal Global Settings asset.",
                    MessageType.Warning);
            }

            EditorGUI.BeginProperty(position, renderingLayerMaskContent, property);

            EditorGUI.BeginChangeCheck();
            int newSelectedLayer;
            if (attr.ShowLabel)
            {
                renderingLayerMaskContent.text = property.displayName;
                var popupLabels = Array.ConvertAll(popupNames, name => new GUIContent(name));
                newSelectedLayer = EditorGUI.Popup(position, renderingLayerMaskContent, selectedLayer, popupLabels);
            }
            else
            {
                newSelectedLayer = EditorGUI.Popup(position, selectedLayer, popupNames);
            }

            if (EditorGUI.EndChangeCheck())
            {
                property.uintValue = ToSingleLayerMask(newSelectedLayer);
            }

            EditorGUI.EndProperty();
        }

        private static string[] GetRenderingLayerMaskNames()
        {
#if UNITY_6000_0_OR_NEWER
            return RenderingLayerMask.GetDefinedRenderingLayerNames();
#else
            var renderPipeline = GraphicsSettings.currentRenderPipeline;
            return renderPipeline != null ? renderPipeline.renderingLayerMaskNames : Array.Empty<string>();
#endif
        }

        private static List<string> GetMaskNamesForDisplay(string[] sourceNames, uint renderingLayerMask, out bool hasUndefinedLayer)
        {
            var highestSetLayer = GetSelectedLayerIndex(renderingLayerMask, 32);
            var requiredCount = Mathf.Max(sourceNames.Length, highestSetLayer + 1, 1);

            var maskNames = new List<string>(requiredCount);
            hasUndefinedLayer = false;

            for (var i = 0; i < requiredCount; i++)
            {
                if (i < sourceNames.Length && !string.IsNullOrEmpty(sourceNames[i]))
                {
                    maskNames.Add(sourceNames[i]);
                    continue;
                }

                maskNames.Add($"Unused Layer {i}");
                hasUndefinedLayer = true;
            }

            return maskNames;
        }

        private static int GetSelectedLayerIndex(uint renderingLayerMask, int maxLayerCount)
        {
            if (renderingLayerMask == 0)
            {
                return 0;
            }

            for (var i = 0; i < 32; i++)
            {
                var layerBit = 1u << i;
                if ((renderingLayerMask & layerBit) == 0)
                {
                    continue;
                }

                return Mathf.Clamp(i, 0, Mathf.Max(0, maxLayerCount - 1));
            }

            return 0;
        }

        private static uint ToSingleLayerMask(int selectedLayer)
        {
            var clampedLayer = Mathf.Clamp(selectedLayer, 0, 31);
            return 1u << clampedLayer;
        }
    }
}
