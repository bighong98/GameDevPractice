#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

using TH.Control.Data;
using TH.Control.State;
using TH.Combat;
using TH.Resource;
using TH.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;

namespace TH.Resource.Editor
{
    // 레거시 직참조 필드 값을 AssetReference 기반 필드로 일괄 복사하는 임시 마이그레이션 도구
    public static class LegacyAssetReferenceMigrationEditor
    {
        private const string MenuRoot = "Tools/Addressables/Migration/Legacy To AssetReference/";

        [MenuItem(MenuRoot + "Preview")]
        private static void Preview()
        {
            RunMigration(applyChanges: false, clearLegacyFields: false);
        }

        [MenuItem(MenuRoot + "Apply (Keep Legacy Fields)")]
        private static void ApplyKeepLegacy()
        {
            RunMigration(applyChanges: true, clearLegacyFields: false);
        }

        [MenuItem(MenuRoot + "Apply (Clear Legacy Fields)")]
        private static void ApplyAndClearLegacy()
        {
            bool proceed = EditorUtility.DisplayDialog(
                "Legacy To AssetReference Migration",
                "legacy 직참조 필드를 null/empty 로 비우고 AssetReference 필드만 남깁니다. 진행할까요?",
                "진행",
                "취소");

            if (!proceed)
            {
                return;
            }

            RunMigration(applyChanges: true, clearLegacyFields: true);
        }

        
        [MenuItem(MenuRoot + "Register Missing StateMachine Assets To Shared")]
        private static void RegisterMissingStateMachineAssetsToShared()
        {
            var stats = new MigrationStats();

            ProcessAssets(LoadAssetsOfType<ActionStateSO>(), "ActionStateSO", asset =>
                MigrateActionState(asset, applyChanges: false, clearLegacyFields: false, stats));

            ProcessObjects(LoadPrefabComponentsOfType<ActionStateMachine>(), "ActionStateMachine(prefab)", asset =>
                MigrateActionStateMachine(asset, applyChanges: false, clearLegacyFields: false, stats));

            RegisterNonAddressableAssetsToGroup(stats, "Shared");
        }

        [MenuItem(MenuRoot + "Apply (Clear Legacy Fields - Force)")]
        private static void ApplyAndClearLegacyForce()
        {
            RunMigration(applyChanges: true, clearLegacyFields: true);
        }

        
        private static void RunMigration(bool applyChanges, bool clearLegacyFields)
        {
            var stats = new MigrationStats();

            ProcessAssets(LoadAssetsOfType<OptionCategoryPanelMapSO>(), "OptionCategoryPanelMapSO", asset =>
                MigrateOptionCategoryPanelMap(asset, applyChanges, clearLegacyFields, stats));

            ProcessAssets(LoadBaseTypeAssets(), "BaseTypeSO", asset =>
                MigrateBaseType(asset, applyChanges, clearLegacyFields, stats));

            ProcessAssets(LoadAssetsOfType<WeaponTypeSO>(), "WeaponTypeSO", asset =>
                MigrateWeaponType(asset, applyChanges, clearLegacyFields, stats));

            ProcessAssets(LoadAssetsOfType<SkillTypeSO>(), "SkillTypeSO", asset =>
                MigrateSkillType(asset, applyChanges, clearLegacyFields, stats));

            ProcessAssets(LoadAssetsOfType<SkillOnHitExplosionAreaDamageEffectSO>(), "SkillOnHitExplosionAreaDamageEffectSO", asset =>
                MigrateExplosionOnHitEffect(asset, applyChanges, clearLegacyFields, stats));

            ProcessAssets(LoadCharacterTypeAssets(), "CharacterTypeSO", asset =>
                MigrateCharacterType(asset, applyChanges, clearLegacyFields, stats));

            ProcessAssets(LoadAssetsOfType<PlayerTypeSO>(), "PlayerTypeSO", asset =>
                MigratePlayerType(asset, applyChanges, clearLegacyFields, stats));

            ProcessAssets(LoadAssetsOfType<ActionStateSO>(), "ActionStateSO", asset =>
                MigrateActionState(asset, applyChanges, clearLegacyFields, stats));

            ProcessObjects(LoadPrefabComponentsOfType<ActionStateMachine>(), "ActionStateMachine(prefab)", asset =>
                MigrateActionStateMachine(asset, applyChanges, clearLegacyFields, stats));

            ProcessObjects(LoadPrefabComponentsOfType<LoadSlotPanelUI>(), "LoadSlotPanelUI(prefab)", asset =>
                MigrateLoadSlotPanel(asset, applyChanges, clearLegacyFields, stats));

            ProcessObjects(LoadPrefabComponentsOfType<LoadSlotPanelEmbeddedUI>(), "LoadSlotPanelEmbeddedUI(prefab)", asset =>
                MigrateLoadSlotPanelEmbedded(asset, applyChanges, clearLegacyFields, stats));

            if (applyChanges)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string mode = applyChanges ? "APPLY" : "PREVIEW";
            Debug.Log(
                $"[{nameof(LegacyAssetReferenceMigrationEditor)}] {mode} finished. " +
                $"scanned={stats.ScannedAssets}, changedAssets={stats.ChangedAssets}, " +
                $"objectAssignments={stats.ObjectAssignments}, listAssignments={stats.ListAssignments}, " +
                $"legacyCleared={stats.LegacyCleared}, skipped={stats.Skipped}, " +
                $"nonAddressable={stats.NonAddressableAssets.Count}");

            if (!applyChanges)
            {
                PrintNonAddressableAssets(stats);
            }
        }

        private static void PrintNonAddressableAssets(MigrationStats stats)
        {
            if (stats.NonAddressableAssets.Count == 0)
            {
                Debug.Log($"[{nameof(LegacyAssetReferenceMigrationEditor)}] PREVIEW: 모든 대상 에셋이 Addressables에 등록되어 있습니다.");
                return;
            }

            Debug.LogWarning("[" + nameof(LegacyAssetReferenceMigrationEditor) + "] PREVIEW: Addressables 미등록 대상 목록 (" + stats.NonAddressableAssets.Count + ")");

            foreach (var item in stats.NonAddressableAssets)
            {
                Debug.LogWarning("[" + nameof(LegacyAssetReferenceMigrationEditor) + "] NonAddressable: " + item.Key, item.Value);
            }

                }

        private static void RegisterNonAddressableAssetsToGroup(MigrationStats stats, string groupName)
        {
            if (stats == null)
            {
                return;
            }

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.LogError($"[{nameof(LegacyAssetReferenceMigrationEditor)}] Addressable settings not found");
                return;
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);
            if (group == null)
            {
                Debug.LogError($"[{nameof(LegacyAssetReferenceMigrationEditor)}] Group not found: {groupName}");
                return;
            }

            int added = 0;
            int skipped = 0;

            foreach (var item in stats.NonAddressableAssets)
            {
                Object asset = item.Value;
                if (asset == null)
                {
                    skipped++;
                    continue;
                }

                string assetPath = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    skipped++;
                    Debug.LogWarning($"[{nameof(LegacyAssetReferenceMigrationEditor)}] Skip non-project asset: {assetPath} <= {item.Key}", asset);
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid))
                {
                    skipped++;
                    continue;
                }

                AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
                if (existingEntry != null)
                {
                    continue;
                }

                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
                if (entry == null)
                {
                    skipped++;
                    continue;
                }

                added++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[{nameof(LegacyAssetReferenceMigrationEditor)}] RegisterMissingStateMachineAssetsToShared finished. " +
                $"targetGroup={groupName}, added={added}, skipped={skipped}");
        }

        
        private static void RegisterNonAddressableAssetIfNeeded(MigrationStats stats, string assetGuid, Object source, string context)
        {
            if (string.IsNullOrEmpty(assetGuid) || source == null)
            {
                return;
            }

            if (IsAddressableAsset(assetGuid))
            {
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(source);
            string displayName = string.IsNullOrEmpty(assetPath)
                ? source.name
                : $"{assetPath} ({source.name})";

            string itemKey = $"{displayName} <= {context}";
            if (!stats.NonAddressableAssets.ContainsKey(itemKey))
            {
                stats.NonAddressableAssets.Add(itemKey, source);
            }
        }

        private static bool IsAddressableAsset(string assetGuid)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return false;
            }

            AddressableAssetEntry entry = settings.FindAssetEntry(assetGuid);
            return entry != null;
        }

        private static void ProcessAssets<T>(IReadOnlyList<T> assets, string label, Func<T, bool> migrator)
            where T : ScriptableObject
        {
            if (assets == null || assets.Count == 0)
            {
                Debug.Log($"[{nameof(LegacyAssetReferenceMigrationEditor)}] No assets found for {label}");
                return;
            }

            foreach (var asset in assets)
            {
                if (asset == null)
                {
                    continue;
                }

                migrator(asset);
            }
        }

        private static void ProcessObjects<T>(IReadOnlyList<T> assets, string label, Func<T, bool> migrator)
            where T : UnityEngine.Object
        {
            if (assets == null || assets.Count == 0)
            {
                Debug.Log($"[{nameof(LegacyAssetReferenceMigrationEditor)}] No assets found for {label}");
                return;
            }

            foreach (var asset in assets)
            {
                if (asset == null)
                {
                    continue;
                }

                migrator(asset);
            }
        }

        private static bool MigrateOptionCategoryPanelMap(
            OptionCategoryPanelMapSO asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;
            bool changed = false;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate OptionCategoryPanelMapSO");
            }

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "categoryButtonTemplate",
                assetReferenceFieldName: "categoryButtonTemplateReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats,
                normalizeSource: obj => obj is Component component ? component.gameObject : obj);

            IList entries = GetFieldValue<IList>(asset, "entries");
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    object entry = entries[i];
                    if (entry == null)
                    {
                        continue;
                    }

                    changed |= CopyObjectFieldToAssetReference(
                        owner: entry,
                        legacyFieldName: "panelPrefab",
                        assetReferenceFieldName: "panelPrefabReference",
                        applyChanges: applyChanges,
                        clearLegacyField: clearLegacyFields,
                        stats: stats);
                }
            }

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateBaseType(
            BaseTypeSO asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;
            bool changed = false;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate BaseTypeSO");
            }

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "prefab",
                assetReferenceFieldName: "prefabReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats,
                normalizeSource: obj => obj is Component component ? component.gameObject : obj);

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "sprite",
                assetReferenceFieldName: "spriteReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateWeaponType(
            WeaponTypeSO asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;
            bool changed = false;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate WeaponTypeSO");
            }

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "EquippedPrefab",
                assetReferenceFieldName: "equippedPrefabReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "equippedPrefabLeft",
                assetReferenceFieldName: "equippedPrefabLeftReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            changed |= CopyObjectListToAssetReferenceList(
                owner: asset,
                legacyListFieldName: "defaultSkills",
                assetReferenceListFieldName: "defaultSkillReferences",
                applyChanges: applyChanges,
                clearLegacyList: clearLegacyFields,
                stats: stats);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateSkillType(
            SkillTypeSO asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;
            bool changed = false;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate SkillTypeSO");
            }

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "animatorOverride",
                assetReferenceFieldName: "animatorOverrideReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "projectilePrefab",
                assetReferenceFieldName: "projectilePrefabReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "skillSlotImage",
                assetReferenceFieldName: "skillSlotImageReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            IList cues = GetFieldValue<IList>(asset, "skillVfxCues");
            if (cues != null)
            {
                for (int i = 0; i < cues.Count; i++)
                {
                    object cue = cues[i];
                    if (cue == null)
                    {
                        continue;
                    }

                    changed |= CopyObjectFieldToAssetReference(
                        owner: cue,
                        legacyFieldName: "effectPrefab",
                        assetReferenceFieldName: "effectPrefabReference",
                        applyChanges: applyChanges,
                        clearLegacyField: clearLegacyFields,
                        stats: stats);

                    changed |= CopyObjectFieldToAssetReference(
                        owner: cue,
                        legacyFieldName: "sfxClip",
                        assetReferenceFieldName: "sfxClipReference",
                        applyChanges: applyChanges,
                        clearLegacyField: clearLegacyFields,
                        stats: stats);
                }
            }

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateExplosionOnHitEffect(
            SkillOnHitExplosionAreaDamageEffectSO asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate SkillOnHitExplosionAreaDamageEffectSO");
            }

            bool changed = CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "explosionVfxPrefab",
                assetReferenceFieldName: "explosionVfxPrefabReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateCharacterType(
            CharacterTypeSO asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate CharacterTypeSO");
            }

            bool changed = CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "hpBarPrefab",
                assetReferenceFieldName: "hpBarPrefabReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigratePlayerType(
            PlayerTypeSO asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate PlayerTypeSO");
            }

            bool changed = CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "levelUpEffect",
                assetReferenceFieldName: "levelUpEffectReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateActionState(
            ActionStateSO asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;
            bool changed = false;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate ActionStateSO");
            }

            changed |= CopyObjectListToAssetReferenceList(
                owner: asset,
                legacyListFieldName: "onEnterActions",
                assetReferenceListFieldName: "onEnterActionReferences",
                applyChanges: applyChanges,
                clearLegacyList: clearLegacyFields,
                stats: stats);

            changed |= CopyObjectListToAssetReferenceList(
                owner: asset,
                legacyListFieldName: "updateActions",
                assetReferenceListFieldName: "updateActionReferences",
                applyChanges: applyChanges,
                clearLegacyList: clearLegacyFields,
                stats: stats);

            changed |= CopyObjectListToAssetReferenceList(
                owner: asset,
                legacyListFieldName: "onExitActions",
                assetReferenceListFieldName: "onExitActionReferences",
                applyChanges: applyChanges,
                clearLegacyList: clearLegacyFields,
                stats: stats);

            IList transitions = GetFieldValue<IList>(asset, "transitions");
            changed |= MigrateTransitionEntries(transitions, applyChanges, clearLegacyFields, stats);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateActionStateMachine(
            ActionStateMachine asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;
            bool changed = false;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate ActionStateMachine");
            }

            changed |= CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "initialState",
                assetReferenceFieldName: "initialStateReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats);

            IList globalTransitions = GetFieldValue<IList>(asset, "globalTransitions");
            changed |= MigrateTransitionEntries(globalTransitions, applyChanges, clearLegacyFields, stats);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateTransitionEntries(
            IList transitions,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            if (transitions == null || transitions.Count == 0)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < transitions.Count; i++)
            {
                object transition = transitions[i];
                if (transition == null)
                {
                    continue;
                }

                bool transitionChanged = false;
                transitionChanged |= CopyObjectFieldToAssetReference(
                    owner: transition,
                    legacyFieldName: "condition",
                    assetReferenceFieldName: "conditionReference",
                    applyChanges: applyChanges,
                    clearLegacyField: clearLegacyFields,
                    stats: stats);

                transitionChanged |= CopyObjectFieldToAssetReference(
                    owner: transition,
                    legacyFieldName: "destinationState",
                    assetReferenceFieldName: "destinationStateReference",
                    applyChanges: applyChanges,
                    clearLegacyField: clearLegacyFields,
                    stats: stats);

                if (transitionChanged && applyChanges)
                {
                    transitions[i] = transition;
                }

                changed |= transitionChanged;
            }

            return changed;
        }

        
        private static bool MigrateLoadSlotPanel(
            LoadSlotPanelUI asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate LoadSlotPanelUI");
            }

            bool changed = CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "loadSlotTemplate",
                assetReferenceFieldName: "loadSlotTemplateReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats,
                normalizeSource: obj => obj is Component component ? component.gameObject : obj);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool MigrateLoadSlotPanelEmbedded(
            LoadSlotPanelEmbeddedUI asset,
            bool applyChanges,
            bool clearLegacyFields,
            MigrationStats stats)
        {
            stats.ScannedAssets++;

            if (applyChanges)
            {
                Undo.RecordObject(asset, "Migrate LoadSlotPanelEmbeddedUI");
            }

            bool changed = CopyObjectFieldToAssetReference(
                owner: asset,
                legacyFieldName: "loadSlotTemplate",
                assetReferenceFieldName: "loadSlotTemplateReference",
                applyChanges: applyChanges,
                clearLegacyField: clearLegacyFields,
                stats: stats,
                normalizeSource: obj => obj is Component component ? component.gameObject : obj);

            if (applyChanges && changed)
            {
                EditorUtility.SetDirty(asset);
                stats.ChangedAssets++;
            }

            return changed;
        }

        private static bool CopyObjectFieldToAssetReference(
            object owner,
            string legacyFieldName,
            string assetReferenceFieldName,
            bool applyChanges,
            bool clearLegacyField,
            MigrationStats stats,
            Func<Object, Object> normalizeSource = null)
        {
            FieldInfo legacyField = GetFieldRecursive(owner.GetType(), legacyFieldName);
            FieldInfo referenceField = GetFieldRecursive(owner.GetType(), assetReferenceFieldName);
            if (legacyField == null || referenceField == null)
            {
                stats.Skipped++;
                return false;
            }

            Object legacyObject = legacyField.GetValue(owner) as Object;
            if (legacyObject == null)
            {
                return false;
            }

            if (normalizeSource != null)
            {
                legacyObject = normalizeSource(legacyObject);
            }

            if (legacyObject == null)
            {
                stats.Skipped++;
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(legacyObject);
            string guid = string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                stats.Skipped++;
                return false;
            }

            RegisterNonAddressableAssetIfNeeded(
                stats,
                guid,
                legacyObject,
                $"{owner.GetType().Name}.{legacyFieldName}");

            AssetReference assetReference = referenceField.GetValue(owner) as AssetReference;
            if (assetReference == null)
            {
                if (!applyChanges)
                {
                    stats.ObjectAssignments++;
                    return true;
                }

                assetReference = Activator.CreateInstance(referenceField.FieldType) as AssetReference;
                if (assetReference == null)
                {
                    stats.Skipped++;
                    return false;
                }

                referenceField.SetValue(owner, assetReference);
            }

            bool needsAssign = !string.Equals(assetReference.AssetGUID, guid, StringComparison.Ordinal);
            bool changed = false;

            if (needsAssign)
            {
                stats.ObjectAssignments++;
                changed = true;

                if (applyChanges)
                {
                    bool setResult = assetReference.SetEditorAsset(legacyObject);
                    if (!setResult)
                    {
                        stats.Skipped++;
                        return false;
                    }
                }
            }

            if (clearLegacyField)
            {
                changed = true;
                stats.LegacyCleared++;

                if (applyChanges)
                {
                    legacyField.SetValue(owner, null);
                }
            }

            return changed;
        }

        private static bool CopyObjectListToAssetReferenceList(
            object owner,
            string legacyListFieldName,
            string assetReferenceListFieldName,
            bool applyChanges,
            bool clearLegacyList,
            MigrationStats stats)
        {
            FieldInfo legacyField = GetFieldRecursive(owner.GetType(), legacyListFieldName);
            FieldInfo referenceField = GetFieldRecursive(owner.GetType(), assetReferenceListFieldName);
            if (legacyField == null || referenceField == null)
            {
                stats.Skipped++;
                return false;
            }

            IList legacyList = legacyField.GetValue(owner) as IList;
            if (legacyList == null || legacyList.Count == 0)
            {
                return false;
            }

            IList referenceList = referenceField.GetValue(owner) as IList;
            if (referenceList == null)
            {
                if (!applyChanges)
                {
                    stats.ListAssignments += legacyList.Count;
                    return true;
                }

                referenceList = Activator.CreateInstance(referenceField.FieldType) as IList;
                if (referenceList == null)
                {
                    stats.Skipped++;
                    return false;
                }

                referenceField.SetValue(owner, referenceList);
            }

            Type elementType = referenceField.FieldType.IsGenericType
                ? referenceField.FieldType.GetGenericArguments()[0]
                : null;

            if (elementType == null)
            {
                stats.Skipped++;
                return false;
            }

            var existingGuids = new HashSet<string>(StringComparer.Ordinal);
            foreach (object entry in referenceList)
            {
                if (entry is AssetReference existingRef && !string.IsNullOrEmpty(existingRef.AssetGUID))
                {
                    existingGuids.Add(existingRef.AssetGUID);
                }
            }

            bool changed = false;
            for (int i = 0; i < legacyList.Count; i++)
            {
                if (legacyList[i] is not Object source || source == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(source);
                string guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(guid))
                {
                    stats.Skipped++;
                    continue;
                }

                RegisterNonAddressableAssetIfNeeded(
                    stats,
                    guid,
                    source,
                    $"{owner.GetType().Name}.{legacyListFieldName}[{i}]");

                if (!existingGuids.Add(guid))
                {
                    continue;
                }

                changed = true;
                stats.ListAssignments++;

                if (!applyChanges)
                {
                    continue;
                }

                var newReference = Activator.CreateInstance(elementType) as AssetReference;
                if (newReference == null)
                {
                    stats.Skipped++;
                    continue;
                }

                bool setResult = newReference.SetEditorAsset(source);
                if (!setResult)
                {
                    stats.Skipped++;
                    continue;
                }

                referenceList.Add(newReference);
            }

            if (clearLegacyList)
            {
                changed = true;
                stats.LegacyCleared += legacyList.Count;

                if (applyChanges)
                {
                    legacyList.Clear();
                }
            }

            return changed;
        }

        private static FieldInfo GetFieldRecursive(Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, flags);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static T GetFieldValue<T>(object owner, string fieldName) where T : class
        {
            FieldInfo field = GetFieldRecursive(owner.GetType(), fieldName);
            if (field == null)
            {
                return null;
            }

            return field.GetValue(owner) as T;
        }

        private static List<T> LoadAssetsOfType<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            var assets = new List<T>(guids.Length);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
                if (asset != null)
                {
                    assets.Add(asset);
                }
            }

            return assets;
        }

        private static List<BaseTypeSO> LoadBaseTypeAssets()
        {
            var assets = LoadAssetsOfType<BaseTypeSO>();
            if (assets.Count > 0)
            {
                return assets;
            }

            var fallback = new List<BaseTypeSO>();
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ScriptableObject loaded = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (loaded is BaseTypeSO baseType)
                {
                    fallback.Add(baseType);
                }
            }

            return fallback;
        }

        private static List<T> LoadPrefabComponentsOfType<T>() where T : Component
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            var results = new List<T>();

            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefabRoot == null)
                {
                    continue;
                }

                T[] components = prefabRoot.GetComponentsInChildren<T>(true);
                if (components == null || components.Length == 0)
                {
                    continue;
                }

                for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                {
                    if (components[componentIndex] != null)
                    {
                        results.Add(components[componentIndex]);
                    }
                }
            }

            return results;
        }

        private static List<CharacterTypeSO> LoadCharacterTypeAssets()
        {
            var assets = LoadAssetsOfType<CharacterTypeSO>();
            if (assets.Count > 0)
            {
                return assets;
            }

            var fallback = new List<CharacterTypeSO>();
            List<BaseTypeSO> baseTypeAssets = LoadAssetsOfType<BaseTypeSO>();
            for (int i = 0; i < baseTypeAssets.Count; i++)
            {
                if (baseTypeAssets[i] is CharacterTypeSO characterType)
                {
                    fallback.Add(characterType);
                }
            }

            return fallback;
        }

        private sealed class MigrationStats
        {
            public int ScannedAssets;
            public int ChangedAssets;
            public int ObjectAssignments;
            public int ListAssignments;
            public int LegacyCleared;
            public int Skipped;
            public readonly SortedDictionary<string, Object> NonAddressableAssets = new(StringComparer.Ordinal);
        }
    }
}
#endif
