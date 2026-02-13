#if UNITY_EDITOR
using System;
using TH.Resource;
using UnityEditor;
using UnityEngine;

public sealed class SkillTypeValidationWindow : EditorWindow
{
    private const string WindowTitle = "Skill Validator";
    private const string MenuPath = "Tools/Skill/Validation/Addressable Folder Validator";
    private const string DefaultAddressKey = "item.weapon_data.folder"; //"skill.data.folder";
    private const string PrefAddressKey = "TH.SkillValidator.AddressKey";
    private const string PrefValidateStepSkills = "TH.SkillValidator.ValidateStepSkills";

    [SerializeField] private string addressKey = DefaultAddressKey;
    [SerializeField] private bool validateStepSkillsFromSequence = true;

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        var window = GetWindow<SkillTypeValidationWindow>(WindowTitle);
        window.minSize = new Vector2(420f, 140f);
        window.Show();
    }

    private void OnEnable()
    {
        addressKey = EditorPrefs.GetString(PrefAddressKey, DefaultAddressKey);
        validateStepSkillsFromSequence = EditorPrefs.GetBool(PrefValidateStepSkills, true);
    }

    private void OnDisable()
    {
        EditorPrefs.SetString(PrefAddressKey, addressKey ?? string.Empty);
        EditorPrefs.SetBool(PrefValidateStepSkills, validateStepSkillsFromSequence);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Addressable Folder Validation", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        addressKey = EditorGUILayout.TextField("Address Key or Map ID", addressKey);
        validateStepSkillsFromSequence = EditorGUILayout.ToggleLeft(
            "Validate step SkillTypeSO from ComboSequenceSO",
            validateStepSkillsFromSequence);

        EditorGUILayout.Space(8f);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(addressKey)))
        {
            if (GUILayout.Button("Validate Folder"))
            {
                SkillTypeValidationBatchTool.ValidateAddressableFolder(addressKey, validateStepSkillsFromSequence);
            }
        }
    }
}

public static class SkillTypeValidationBatchTool
{
    private const string DefaultAddressKey = "skill.data.folder";

    [MenuItem("Tools/Skill/Validation/Validate Addressable Folder (Default Key)")]
    private static void ValidateDefaultFolder()
    {
        ValidateAddressableFolder(DefaultAddressKey, includeStepSkillValidation: true);
    }

    public static bool ValidateAddressableFolder(string addressKey, bool includeStepSkillValidation)
    {
        var rootPath = ResolvePathByAddressKey(addressKey);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            Debug.LogError($"[SkillTypeValidationBatchTool] Addressable key not resolved: {addressKey}");
            return false;
        }

        if (!AssetDatabase.IsValidFolder(rootPath))
        {
            var candidateDir = System.IO.Path.GetDirectoryName(rootPath)?.Replace("\\", "/");
            if (string.IsNullOrWhiteSpace(candidateDir) || !AssetDatabase.IsValidFolder(candidateDir))
            {
                Debug.LogError($"[SkillTypeValidationBatchTool] Resolved path is not a valid folder: {rootPath}");
                return false;
            }

            Debug.LogWarning($"[SkillTypeValidationBatchTool] Resolved key points to an asset. Using parent folder: {candidateDir}");
            rootPath = candidateDir;
        }

        var summary = new ValidationSummary(rootPath, addressKey);

        ValidateSkills(rootPath, summary);
        ValidateSequences(rootPath, includeStepSkillValidation, summary);

        LogSummary(summary);
        return summary.TotalErrorCount == 0;
    }

    private static void ValidateSkills(string rootPath, ValidationSummary summary)
    {
        var guidArray = AssetDatabase.FindAssets("t:SkillTypeSO", new[] { rootPath });
        summary.SkillAssetCount = guidArray.Length;

        for (int i = 0; i < guidArray.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guidArray[i]);
            var skill = AssetDatabase.LoadAssetAtPath<SkillTypeSO>(path);
            if (skill == null)
            {
                continue;
            }

            var isValid = skill.ValidateInEditor(out var errors, out var warnings);
            summary.TotalWarningCount += warnings.Count;
            summary.TotalErrorCount += errors.Count;

            if (!isValid)
            {
                summary.InvalidSkillAssetCount++;
            }

            LogIssues($"SkillTypeSO:{skill.name}", path, errors, warnings, skill);
        }
    }

    private static void ValidateSequences(string rootPath, bool includeStepSkillValidation, ValidationSummary summary)
    {
        var guidArray = AssetDatabase.FindAssets("t:ComboSequenceSO", new[] { rootPath });
        summary.SequenceAssetCount = guidArray.Length;

        for (int i = 0; i < guidArray.Length; i++)
        {
            var path = AssetDatabase.GUIDToAssetPath(guidArray[i]);
            var sequence = AssetDatabase.LoadAssetAtPath<ComboSequenceSO>(path);
            if (sequence == null)
            {
                continue;
            }

            var isValid = sequence.ValidateInEditor(out var errors, out var warnings, includeStepSkillValidation);
            summary.TotalWarningCount += warnings.Count;
            summary.TotalErrorCount += errors.Count;

            if (!isValid)
            {
                summary.InvalidSequenceAssetCount++;
            }

            LogIssues($"ComboSequenceSO:{sequence.name}", path, errors, warnings, sequence);
        }
    }

    private static void LogIssues(string title, string path, System.Collections.Generic.List<string> errors, System.Collections.Generic.List<string> warnings, UnityEngine.Object context)
    {
        for (int i = 0; i < warnings.Count; i++)
        {
            Debug.LogWarning($"[SkillTypeValidationBatchTool] {title} Warning: {warnings[i]} (path={path})", context);
        }

        for (int i = 0; i < errors.Count; i++)
        {
            Debug.LogError($"[SkillTypeValidationBatchTool] {title} Error: {errors[i]} (path={path})", context);
        }
    }

    private static void LogSummary(ValidationSummary summary)
    {
        var message =
            "[SkillTypeValidationBatchTool] Completed. " +
            $"AddressKey={summary.AddressKey}, RootPath={summary.RootPath}, " +
            $"SkillAssets={summary.SkillAssetCount}, InvalidSkills={summary.InvalidSkillAssetCount}, " +
            $"SequenceAssets={summary.SequenceAssetCount}, InvalidSequences={summary.InvalidSequenceAssetCount}, " +
            $"Warnings={summary.TotalWarningCount}, Errors={summary.TotalErrorCount}";

        if (summary.TotalErrorCount > 0)
        {
            Debug.LogError(message);
            return;
        }

        if (summary.TotalWarningCount > 0)
        {
            Debug.LogWarning(message);
            return;
        }

        Debug.Log(message);
    }

    private static string ResolvePathByAddressKey(string addressKey)
    {
        return EditorAddressablePathResolver.ResolvePathByMapIdOrAddressKey(
            addressKey,
            nameof(SkillTypeValidationBatchTool),
            logOnError: false);
    }

    private struct ValidationSummary
    {
        public readonly string RootPath;
        public readonly string AddressKey;

        public int SkillAssetCount;
        public int InvalidSkillAssetCount;
        public int SequenceAssetCount;
        public int InvalidSequenceAssetCount;
        public int TotalWarningCount;
        public int TotalErrorCount;

        public ValidationSummary(string rootPath, string addressKey)
        {
            RootPath = rootPath;
            AddressKey = addressKey;
            SkillAssetCount = 0;
            InvalidSkillAssetCount = 0;
            SequenceAssetCount = 0;
            InvalidSequenceAssetCount = 0;
            TotalWarningCount = 0;
            TotalErrorCount = 0;
        }
    }
}

[CustomEditor(typeof(SkillTypeSO))]
public sealed class SkillTypeSOInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Validate Skill (Editor)"))
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is SkillTypeSO skill)
                {
                    skill.ValidateAndLogInEditor("Inspector");
                }
            }
        }
    }
}

[CustomEditor(typeof(ComboSequenceSO))]
public sealed class ComboSequenceSOInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Validate Combo Sequence (Editor)"))
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] is ComboSequenceSO sequence)
                {
                    sequence.ValidateAndLogInEditor("Inspector", includeStepSkillValidation: true);
                }
            }
        }
    }
}
#endif
