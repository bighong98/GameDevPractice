using System;
using System.Collections.Generic;
using TH.Control.Data;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TH.Editor
{
    [CustomEditor(typeof(AnimateActionSO))]
    public sealed class AnimateActionSOEditor : UnityEditor.Editor
    {
        private const string ManualInputLabel = "<Manual Input>";

        private SerializedProperty targetAnimatorControllerProp;
        private SerializedProperty controllerMatchModeProp;
        private SerializedProperty searchInChildrenWhenControllerSpecifiedProp;
        
        private SerializedProperty applyOverrideControllerProp;
        private SerializedProperty overrideControllerProp;
        private SerializedProperty requireMatchingBaseControllerProp;
        private SerializedProperty skipWhenOverrideControllerMissingProp;
        private SerializedProperty skipWhenBaseControllerMismatchProp;
        private SerializedProperty skipWhenControllerMismatchProp;

        private SerializedProperty playStateProp;
        private SerializedProperty preferredStateNameProp;
        private SerializedProperty layerIndexProp;
        private SerializedProperty statePlayModeProp;
        private SerializedProperty transitionDurationProp;
        private SerializedProperty useNormalizedTimeOffsetProp;
        private SerializedProperty normalizedTimeOffsetProp;
        private SerializedProperty skipWhenAlreadyInStateProp;
        private SerializedProperty skipWhenStateNotFoundProp;

        private SerializedProperty applyParametersProp;
        private SerializedProperty parameterApplyOrderProp;
        private SerializedProperty skipMissingOrInvalidParameterProp;
        private SerializedProperty parameterCommandsProp;

        private SerializedProperty enableDebugLogProp;

        private void OnEnable()
        {
            targetAnimatorControllerProp = serializedObject.FindProperty("targetAnimatorController");
            controllerMatchModeProp = serializedObject.FindProperty("controllerMatchMode");
            searchInChildrenWhenControllerSpecifiedProp = serializedObject.FindProperty("searchInChildrenWhenControllerSpecified");
            
            applyOverrideControllerProp = serializedObject.FindProperty("applyOverrideController");
            overrideControllerProp = serializedObject.FindProperty("overrideController");
            requireMatchingBaseControllerProp = serializedObject.FindProperty("requireMatchingBaseController");
            skipWhenOverrideControllerMissingProp = serializedObject.FindProperty("skipWhenOverrideControllerMissing");
            skipWhenBaseControllerMismatchProp = serializedObject.FindProperty("skipWhenBaseControllerMismatch");
            skipWhenControllerMismatchProp = serializedObject.FindProperty("skipWhenControllerMismatch");

            playStateProp = serializedObject.FindProperty("playState");
            preferredStateNameProp = serializedObject.FindProperty("preferredStateName");
            layerIndexProp = serializedObject.FindProperty("layerIndex");
            statePlayModeProp = serializedObject.FindProperty("statePlayMode");
            transitionDurationProp = serializedObject.FindProperty("transitionDuration");
            useNormalizedTimeOffsetProp = serializedObject.FindProperty("useNormalizedTimeOffset");
            normalizedTimeOffsetProp = serializedObject.FindProperty("normalizedTimeOffset");
            skipWhenAlreadyInStateProp = serializedObject.FindProperty("skipWhenAlreadyInState");
            skipWhenStateNotFoundProp = serializedObject.FindProperty("skipWhenStateNotFound");

            applyParametersProp = serializedObject.FindProperty("applyParameters");
            parameterApplyOrderProp = serializedObject.FindProperty("parameterApplyOrder");
            skipMissingOrInvalidParameterProp = serializedObject.FindProperty("skipMissingOrInvalidParameter");
            parameterCommandsProp = serializedObject.FindProperty("parameterCommands");

            enableDebugLogProp = serializedObject.FindProperty("enableDebugLog");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            RuntimeAnimatorController runtimeController = ResolvePreviewRuntimeController();
            AnimatorController controller = ResolveAnimatorController(runtimeController);

            DrawAnimatorTargetSection();
            EditorGUILayout.Space(6f);
            DrawAnimatorOverrideSection();
            EditorGUILayout.Space(6f);
            DrawStateSection(controller);
            EditorGUILayout.Space(6f);
            DrawParameterSection(controller);
            EditorGUILayout.Space(6f);
            EditorGUILayout.PropertyField(enableDebugLogProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAnimatorTargetSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Animator Target", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(targetAnimatorControllerProp);
                EditorGUILayout.PropertyField(controllerMatchModeProp);
                EditorGUILayout.PropertyField(searchInChildrenWhenControllerSpecifiedProp);
                EditorGUILayout.PropertyField(skipWhenControllerMismatchProp);
            }
        }

        private void DrawAnimatorOverrideSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Animator Override", EditorStyles.boldLabel);

                EditorGUILayout.PropertyField(applyOverrideControllerProp);
                if (applyOverrideControllerProp == null || !applyOverrideControllerProp.boolValue)
                {
                    return;
                }

                EditorGUILayout.PropertyField(overrideControllerProp);
                EditorGUILayout.PropertyField(requireMatchingBaseControllerProp);
                EditorGUILayout.PropertyField(skipWhenOverrideControllerMissingProp);
                EditorGUILayout.PropertyField(skipWhenBaseControllerMismatchProp);

                RuntimeAnimatorController overrideRuntime = overrideControllerProp != null
                    ? overrideControllerProp.objectReferenceValue as RuntimeAnimatorController
                    : null;
                if (overrideRuntime == null)
                {
                    EditorGUILayout.HelpBox("Override application is enabled, but overrideController is not assigned.", MessageType.Warning);
                    return;
                }

                bool requireBaseMatch = requireMatchingBaseControllerProp != null && requireMatchingBaseControllerProp.boolValue;
                RuntimeAnimatorController targetRuntime = targetAnimatorControllerProp != null
                    ? targetAnimatorControllerProp.objectReferenceValue as RuntimeAnimatorController
                    : null;
                if (!requireBaseMatch || targetRuntime == null)
                {
                    return;
                }

                RuntimeAnimatorController targetBase = ResolveBaseRuntimeController(targetRuntime);
                RuntimeAnimatorController overrideBase = ResolveBaseRuntimeController(overrideRuntime);
                if (!ReferenceEquals(targetBase, overrideBase))
                {
                    string targetName = targetRuntime != null ? targetRuntime.name : "null";
                    string overrideName = overrideRuntime.name;
                    EditorGUILayout.HelpBox(
                        $"Base controller mismatch. target={targetName}, override={overrideName}",
                        MessageType.Warning);
                }
            }
        }


        private void DrawStateSection(AnimatorController controller)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("State", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(playStateProp);

                DrawLayerIndexField(controller);
                DrawStateNameField(controller);

                EditorGUILayout.PropertyField(statePlayModeProp);
                EditorGUILayout.PropertyField(transitionDurationProp);
                EditorGUILayout.PropertyField(useNormalizedTimeOffsetProp);
                if (useNormalizedTimeOffsetProp != null && useNormalizedTimeOffsetProp.boolValue)
                {
                    EditorGUILayout.PropertyField(normalizedTimeOffsetProp);
                }

                EditorGUILayout.PropertyField(skipWhenAlreadyInStateProp);
                EditorGUILayout.PropertyField(skipWhenStateNotFoundProp);

                if (controller == null)
                {
                    EditorGUILayout.HelpBox(
                        "Assign targetAnimatorController to use Layer/State dropdowns.\n" +
                        "Without it, fields stay as manual input.",
                        MessageType.Info);
                }
            }
        }

        private void DrawParameterSection(AnimatorController controller)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(applyParametersProp);

                if (applyParametersProp == null || !applyParametersProp.boolValue)
                {
                    return;
                }

                EditorGUILayout.PropertyField(parameterApplyOrderProp);
                EditorGUILayout.PropertyField(skipMissingOrInvalidParameterProp);

                DrawParameterCommands(controller);
            }
        }

        private void DrawParameterCommands(AnimatorController controller)
        {
            if (parameterCommandsProp == null)
            {
                return;
            }

            EditorGUILayout.PropertyField(parameterCommandsProp, includeChildren: false);
            if (!parameterCommandsProp.isExpanded)
            {
                return;
            }

            int nextSize = EditorGUILayout.IntField("Size", parameterCommandsProp.arraySize);
            if (nextSize != parameterCommandsProp.arraySize)
            {
                parameterCommandsProp.arraySize = Mathf.Max(0, nextSize);
            }

            for (int i = 0; i < parameterCommandsProp.arraySize; i++)
            {
                SerializedProperty commandProp = parameterCommandsProp.GetArrayElementAtIndex(i);
                if (commandProp == null)
                {
                    continue;
                }

                SerializedProperty enabledProp = commandProp.FindPropertyRelative("enabled");
                SerializedProperty operationProp = commandProp.FindPropertyRelative("operation");
                SerializedProperty parameterNameProp = commandProp.FindPropertyRelative("parameterName");
                SerializedProperty floatValueProp = commandProp.FindPropertyRelative("floatValue");
                SerializedProperty intValueProp = commandProp.FindPropertyRelative("intValue");
                SerializedProperty boolValueProp = commandProp.FindPropertyRelative("boolValue");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"Command {i}", EditorStyles.boldLabel);
                        if (GUILayout.Button("Delete", GUILayout.Width(64f)))
                        {
                            parameterCommandsProp.DeleteArrayElementAtIndex(i);
                            continue;
                        }
                    }

                    if (enabledProp != null)
                    {
                        EditorGUILayout.PropertyField(enabledProp);
                    }

                    if (operationProp != null)
                    {
                        EditorGUILayout.PropertyField(operationProp);
                    }

                    DrawParameterNameField(controller, operationProp, parameterNameProp);

                    if (operationProp == null)
                    {
                        continue;
                    }

                    string[] enumNames = operationProp.enumNames;
                    string operationName = operationProp.enumValueIndex >= 0 && operationProp.enumValueIndex < enumNames.Length
                        ? enumNames[operationProp.enumValueIndex]
                        : string.Empty;

                    switch (operationName)
                    {
                        case "SetFloat":
                            if (floatValueProp != null)
                            {
                                EditorGUILayout.PropertyField(floatValueProp);
                            }
                            break;

                        case "SetInt":
                            if (intValueProp != null)
                            {
                                EditorGUILayout.PropertyField(intValueProp);
                            }
                            break;

                        case "SetBool":
                            if (boolValueProp != null)
                            {
                                EditorGUILayout.PropertyField(boolValueProp);
                            }
                            break;
                    }
                }
            }
        }

        private static void DrawParameterNameField(
            AnimatorController controller,
            SerializedProperty operationProp,
            SerializedProperty parameterNameProp)
        {
            if (parameterNameProp == null)
            {
                return;
            }

            if (controller == null || operationProp == null)
            {
                EditorGUILayout.PropertyField(parameterNameProp);
                return;
            }

            string[] enumNames = operationProp.enumNames;
            string operationName = operationProp.enumValueIndex >= 0 && operationProp.enumValueIndex < enumNames.Length
                ? enumNames[operationProp.enumValueIndex]
                : string.Empty;

            if (!TryGetExpectedParameterType(operationName, out AnimatorControllerParameterType parameterType))
            {
                EditorGUILayout.PropertyField(parameterNameProp);
                return;
            }

            string[] options = BuildParameterOptions(controller, parameterType);
            if (options.Length == 0)
            {
                EditorGUILayout.PropertyField(parameterNameProp);
                return;
            }

            DrawStringPopupWithManualInput("Parameter Name", parameterNameProp, options);
        }

        private static bool TryGetExpectedParameterType(string operationName, out AnimatorControllerParameterType parameterType)
        {
            parameterType = AnimatorControllerParameterType.Float;
            switch (operationName)
            {
                case "SetFloat":
                    parameterType = AnimatorControllerParameterType.Float;
                    return true;

                case "SetInt":
                    parameterType = AnimatorControllerParameterType.Int;
                    return true;

                case "SetBool":
                    parameterType = AnimatorControllerParameterType.Bool;
                    return true;

                case "SetTrigger":
                case "ResetTrigger":
                    parameterType = AnimatorControllerParameterType.Trigger;
                    return true;

                default:
                    return false;
            }
        }

        private static string[] BuildParameterOptions(AnimatorController controller, AnimatorControllerParameterType parameterType)
        {
            if (controller == null)
            {
                return Array.Empty<string>();
            }

            AnimatorControllerParameter[] parameters = controller.parameters;
            if (parameters == null || parameters.Length == 0)
            {
                return Array.Empty<string>();
            }

            var options = new List<string>(parameters.Length);
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter == null || parameter.type != parameterType || string.IsNullOrWhiteSpace(parameter.name))
                {
                    continue;
                }

                options.Add(parameter.name);
            }

            options.Sort(StringComparer.Ordinal);
            return options.ToArray();
        }





        private void DrawLayerIndexField(AnimatorController controller)
        {
            if (layerIndexProp == null)
            {
                return;
            }

            if (controller == null || controller.layers == null || controller.layers.Length == 0)
            {
                EditorGUILayout.PropertyField(layerIndexProp);
                return;
            }

            AnimatorControllerLayer[] layers = controller.layers;
            string[] layerNames = new string[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                string layerName = string.IsNullOrWhiteSpace(layers[i].name) ? $"Layer {i}" : layers[i].name;
                layerNames[i] = $"{i}: {layerName}";
            }

            int currentIndex = Mathf.Clamp(layerIndexProp.intValue, 0, layers.Length - 1);
            int selectedIndex = EditorGUILayout.Popup("Layer Index", currentIndex, layerNames);
            layerIndexProp.intValue = selectedIndex;
        }

        private void DrawStateNameField(AnimatorController controller)
        {
            if (preferredStateNameProp == null)
            {
                return;
            }

            if (controller == null)
            {
                EditorGUILayout.PropertyField(preferredStateNameProp);
                return;
            }

            int selectedLayerIndex = layerIndexProp != null ? layerIndexProp.intValue : 0;
            if (selectedLayerIndex < 0 || selectedLayerIndex >= controller.layers.Length)
            {
                EditorGUILayout.PropertyField(preferredStateNameProp);
                return;
            }

            string[] options = BuildStateOptions(controller.layers[selectedLayerIndex]);
            if (options == null || options.Length == 0)
            {
                EditorGUILayout.PropertyField(preferredStateNameProp);
                return;
            }

            DrawStringPopupWithManualInput("Preferred State Name", preferredStateNameProp, options);
        }

        private static AnimatorController ResolveAnimatorController(RuntimeAnimatorController runtimeController)
        {
            if (runtimeController == null)
            {
                return null;
            }

            if (runtimeController is AnimatorController animatorController)
            {
                return animatorController;
            }

            if (runtimeController is AnimatorOverrideController overrideController &&
                overrideController.runtimeAnimatorController is AnimatorController baseController)
            {
                return baseController;
            }

            return null;
        }

        private RuntimeAnimatorController ResolvePreviewRuntimeController()
        {
            bool applyOverride = applyOverrideControllerProp != null && applyOverrideControllerProp.boolValue;
            if (applyOverride && overrideControllerProp != null && overrideControllerProp.objectReferenceValue != null)
            {
                return overrideControllerProp.objectReferenceValue as RuntimeAnimatorController;
            }

            return targetAnimatorControllerProp != null
                ? targetAnimatorControllerProp.objectReferenceValue as RuntimeAnimatorController
                : null;
        }

        private static RuntimeAnimatorController ResolveBaseRuntimeController(RuntimeAnimatorController runtimeController)
        {
            if (runtimeController is AnimatorOverrideController overrideController &&
                overrideController.runtimeAnimatorController != null)
            {
                return overrideController.runtimeAnimatorController;
            }

            return runtimeController;
        }


        private static string[] BuildStateOptions(AnimatorControllerLayer layer)
        {
            if (layer.stateMachine == null)
            {
                return Array.Empty<string>();
            }

            string layerName = string.IsNullOrWhiteSpace(layer.name) ? "Base Layer" : layer.name;
            var options = new List<string>(64);
            CollectStatePaths(layer.stateMachine, string.Empty, layerName, options);
            return options.ToArray();
        }

        private static void CollectStatePaths(
            AnimatorStateMachine stateMachine,
            string subStatePath,
            string layerName,
            List<string> results)
        {
            if (stateMachine == null || results == null)
            {
                return;
            }

            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                AnimatorState state = states[i].state;
                if (state == null || string.IsNullOrWhiteSpace(state.name))
                {
                    continue;
                }

                string localPath = string.IsNullOrWhiteSpace(subStatePath)
                    ? state.name
                    : $"{subStatePath}.{state.name}";

                results.Add($"{layerName}.{localPath}");
            }

            ChildAnimatorStateMachine[] children = stateMachine.stateMachines;
            for (int i = 0; i < children.Length; i++)
            {
                AnimatorStateMachine child = children[i].stateMachine;
                if (child == null || string.IsNullOrWhiteSpace(child.name))
                {
                    continue;
                }

                string nextPath = string.IsNullOrWhiteSpace(subStatePath)
                    ? child.name
                    : $"{subStatePath}.{child.name}";

                CollectStatePaths(child, nextPath, layerName, results);
            }
        }

        private static void DrawStringPopupWithManualInput(string label, SerializedProperty prop, string[] options)
        {
            if (prop == null)
            {
                return;
            }

            var popupOptions = new List<string>(options);
            if (!popupOptions.Contains(ManualInputLabel))
            {
                popupOptions.Add(ManualInputLabel);
            }

            string currentValue = prop.stringValue ?? string.Empty;
            int currentIndex = popupOptions.IndexOf(currentValue);
            if (currentIndex < 0)
            {
                currentIndex = popupOptions.Count - 1;
            }

            int selectedIndex = EditorGUILayout.Popup(label, currentIndex, popupOptions.ToArray());
            bool isManual = selectedIndex == popupOptions.Count - 1;

            if (!isManual)
            {
                prop.stringValue = popupOptions[selectedIndex];
                return;
            }

            prop.stringValue = EditorGUILayout.TextField($"{label} (Manual)", prop.stringValue);
        }
    }
}
