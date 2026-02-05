using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TH.Item
{
    [DisallowMultipleComponent]
    public sealed class EquipmentOutfitController : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField] private EquipmentHolder equipHolder;
        
        [Header("Outfit")]
        [SerializeField] private bool autoCollectPartsOnAwake = true;
        [SerializeField] private List<OutfitPartKeyTag> outfitParts = new();
        [SerializeField] private List<OutfitSlotDefaultKeyEntry> slotDefaultKeys = new();
        [FormerlySerializedAs("autoCollectActiveFallbackPartsInEditor")]
        [SerializeField] private bool autoCollectActiveDefaultKeysInEditor;

        private readonly Dictionary<OutfitPartTypeSO, List<OutfitPartKeyTag>> partsByPartType = new();
        private readonly Dictionary<OutfitPartTypeSO, OutfitKeySO> defaultKeyByPartType = new();
        private readonly Dictionary<OutfitPartTypeSO, OutfitKeySO> equippedKeyByPartType = new();

        private bool isInitialized;
        private bool isInitializing;

        private void Awake()
        {
            if (equipHolder == null)
                TryGetComponent(out equipHolder);

            if (autoCollectPartsOnAwake && (outfitParts == null || outfitParts.Count == 0))
                CollectOutfitParts();

            ClearVisualState();
        }

        private void OnEnable()
        {
            if (equipHolder != null)
                equipHolder.OnSlotChanged += HandleSlotChanged;
        }

        private void OnDisable()
        {
            if (equipHolder != null)
                equipHolder.OnSlotChanged -= HandleSlotChanged;
        }

        private void Start()
        {
            InitializeAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid InitializeAsync(CancellationToken token)
        {
            if (isInitializing || isInitialized) return;
            isInitializing = true;

            try
            {
                await BuildPartCacheAsync(token);
                await BuildDefaultKeyCacheAsync(token);

                isInitialized = true;
                ApplyAllFromHolder();
            }
            finally
            {
                isInitializing = false;
            }
        }

        private async UniTask BuildPartCacheAsync(CancellationToken token)
        {
            partsByPartType.Clear();
            if (outfitParts == null)
                outfitParts = new List<OutfitPartKeyTag>();

            var uniqueParts = new List<OutfitPartKeyTag>(outfitParts.Count);
            var seen = new HashSet<OutfitPartKeyTag>();
            foreach (var part in outfitParts)
            {
                if (part == null || !seen.Add(part))
                    continue;

                uniqueParts.Add(part);
            }

            var initTasks = new List<UniTask>(uniqueParts.Count);
            foreach (var part in uniqueParts)
                initTasks.Add(part.InitializeAsync(token));

            await UniTask.WhenAll(initTasks);

            foreach (var part in uniqueParts)
            {
                if (part == null || !part.HasResolvedKey)
                    continue;

                var resolvedKey = part.ResolvedKey;
                var partType = ResolvePartTypeForKey(resolvedKey);
                if (partType == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(EquipmentOutfitController)}] Part '{part.name}' has OutfitKey '{resolvedKey.name}' without resolvable partType. Skipping.",
                        part);
                    continue;
                }

                if (!partsByPartType.TryGetValue(partType, out var list))
                {
                    list = new List<OutfitPartKeyTag>();
                    partsByPartType[partType] = list;
                }

                list.Add(part);
            }

            outfitParts = uniqueParts;
        }

        private async UniTask BuildDefaultKeyCacheAsync(CancellationToken token)
        {
            defaultKeyByPartType.Clear();
            if (slotDefaultKeys == null || slotDefaultKeys.Count == 0)
                return;

            var initTasks = new List<UniTask>(slotDefaultKeys.Count);
            foreach (var entry in slotDefaultKeys)
            {
                if (entry == null)
                    continue;

                initTasks.Add(entry.InitializeAsync(token));
            }

            await UniTask.WhenAll(initTasks);

            foreach (var entry in slotDefaultKeys)
            {
                if (entry == null || entry.ResolvedKey == null)
                    continue;

                var partType = entry.ResolvedPartType;
                if (partType == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(EquipmentOutfitController)}] Default key '{entry.ResolvedKey.name}' has no resolvable partType. Skipping.",
                        this);
                    continue;
                }

                if (defaultKeyByPartType.ContainsKey(partType))
                {
                    Debug.LogWarning(
                        $"[{nameof(EquipmentOutfitController)}] Duplicate default key entry for partType '{partType.name}'. " +
                        "Keeping the first entry.",
                        this);
                    continue;
                }

                defaultKeyByPartType[partType] = entry.ResolvedKey;
            }
        }

        private void HandleSlotChanged(IGameItemSlot slot)
        {
            if (!isInitialized) return;

            ApplyAllFromHolder();
        }

        public void ApplyAllFromHolder()
        {
            if (!isInitialized) return;

            equippedKeyByPartType.Clear();

            if (equipHolder != null)
            {
                foreach (var slot in equipHolder.ItemSlots)
                {
                    if (slot == null) continue;

                    var slotType = (Enums.EquippedItemSlotType)slot.Index;
                    if (slot.GetItemInfo is ArmorTypeSO armor && armor.OutfitKey != null)
                    {
                        var partType = ResolvePartTypeForKey(armor.OutfitKey);
                        if (partType == null)
                        {
                            Debug.LogWarning(
                                $"[{nameof(EquipmentOutfitController)}] Equipped key '{armor.OutfitKey.name}' has no resolvable partType. " +
                                $"Slot '{slotType}' visual update skipped.",
                                this);
                            continue;
                        }

                        equippedKeyByPartType[partType] = armor.OutfitKey;
                    }
                }
            }

            ClearVisualState();
            foreach (var partType in partsByPartType.Keys)
            {
                var selectedKey = ResolveSelectedKey(partType);
                if (selectedKey == null)
                    continue;

                ApplyPartTypeVisual(partType, selectedKey);
            }
        }

        private OutfitKeySO ResolveSelectedKey(OutfitPartTypeSO partType)
        {
            if (partType == null)
                return null;

            if (equippedKeyByPartType.TryGetValue(partType, out var equippedKey) && equippedKey != null)
                return equippedKey;

            if (defaultKeyByPartType.TryGetValue(partType, out var defaultKey) && defaultKey != null)
                return defaultKey;

            return null;
        }

        private bool ApplyPartTypeVisual(OutfitPartTypeSO partType, OutfitKeySO selectedKey)
        {
            if (partType == null)
                return false;

            if (!partsByPartType.TryGetValue(partType, out var parts) || parts == null)
                return false;

            var hasMatch = false;

            foreach (var part in parts)
            {
                if (part == null) continue;

                var isMatch = part.IsMatch(selectedKey);
                part.SetVisible(isMatch);
                if (isMatch)
                    hasMatch = true;
            }

            return hasMatch;
        }

        private static OutfitPartTypeSO ResolvePartTypeForKey(OutfitKeySO key)
        {
            if (key == null)
                return null;

            return key.partType;
        }

        private readonly HashSet<GameObject> processedVisualObjects = new();
        private void ClearVisualState()
        {
            processedVisualObjects.Clear();
            DisablePartObjects(outfitParts, processedVisualObjects);
        }

        private static void DisablePartObjects(IEnumerable<OutfitPartKeyTag> parts, ISet<GameObject> processedObjects)
        {
            if (parts == null || processedObjects == null)
                return;

            foreach (var part in parts)
            {
                if (part == null || part.gameObject == null)
                    continue;

                if (!processedObjects.Add(part.gameObject))
                    continue;

                part.SetVisible(false);
            }
        }

        private void CollectOutfitParts()
        {
            outfitParts = new List<OutfitPartKeyTag>(GetComponentsInChildren<OutfitPartKeyTag>(true));
        }

        #region Validation (Editor Only)

#if UNITY_EDITOR

        [Header("Validation (Editor Only)")]
        [SerializeField] private List<AssetReferenceOutfitPartTypeSO> requiredPartTypes = new();

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (autoCollectActiveDefaultKeysInEditor)
                CollectActiveDefaultKeysInEditor();

            ValidateRequiredPartTypesInEditor();
        }

        private void CollectActiveDefaultKeysInEditor()
        {
            var activeParts = GetComponentsInChildren<OutfitPartKeyTag>(true);
            var collected = new List<OutfitSlotDefaultKeyEntry>(activeParts.Length);
            var firstByPartType = new Dictionary<OutfitPartTypeSO, OutfitPartKeyTag>();

            foreach (var part in activeParts)
            {
                if (part == null || part.gameObject == null || !part.IsVisibleForEditor())
                    continue;

                if (!part.TryGetOutfitKeyForEditor(out var key) || key == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(EquipmentOutfitController)}] Active part '{part.name}' has no resolvable OutfitKey. " +
                        "Skipping default key collection for this part.",
                        part);
                    continue;
                }

                var partType = key.partType;
                if (partType == null)
                {
                    Debug.LogWarning(
                        $"[{nameof(EquipmentOutfitController)}] Active part '{part.name}' key '{key.name}' has no partType. " +
                        "Skipping default key collection for this part.",
                        part);
                    continue;
                }

                if (firstByPartType.TryGetValue(partType, out var firstPart))
                {
                    part.SetVisible(false);
                    EditorUtility.SetDirty(part);
                    Debug.LogWarning(
                        $"[{nameof(EquipmentOutfitController)}] Disabled duplicate visible part '{part.name}' for partType '{partType.name}'. " +
                        $"Using '{firstPart.name}' to collect default key.",
                        part);
                    continue;
                }

                firstByPartType[partType] = part;
                collected.Add(new OutfitSlotDefaultKeyEntry
                {
                    partTypeReference = new AssetReferenceOutfitPartTypeSO(partType),
                    keyReference = new AssetReferenceOutfitKeySO(key)
                });
            }

            slotDefaultKeys = collected;
            autoCollectActiveDefaultKeysInEditor = false;
            EditorUtility.SetDirty(this);
        }

        private void ValidateRequiredPartTypesInEditor()
        {
            if (requiredPartTypes == null || requiredPartTypes.Count == 0)
                return;

            var uniqueRequired = new HashSet<OutfitPartTypeSO>();
            var missingDefaultEntries = new List<string>();
            var missingOutfitParts = new List<string>();

            foreach (var requiredPartTypeRef in requiredPartTypes)
            {
                if (requiredPartTypeRef == null || !(requiredPartTypeRef.editorAsset is OutfitPartTypeSO requiredPartType) || requiredPartType == null)
                {
                    Debug.LogError(
                        $"[{nameof(EquipmentOutfitController)}] Required partType reference is missing or invalid.",
                        this);
                    continue;
                }

                if (!uniqueRequired.Add(requiredPartType))
                    continue;

                if (!HasValidDefaultKeyEntryForRequiredPartType(requiredPartType))
                    missingDefaultEntries.Add(requiredPartType.name);

                if (!HasOutfitPartForRequiredPartType(requiredPartType))
                    missingOutfitParts.Add(requiredPartType.name);
            }

            if (missingDefaultEntries.Count > 0)
            {
                Debug.LogError(
                    $"[{nameof(EquipmentOutfitController)}] Missing default key entries for required partTypes: {string.Join(", ", missingDefaultEntries)}.",
                    this);
            }

            if (missingOutfitParts.Count > 0)
            {
                Debug.LogError(
                    $"[{nameof(EquipmentOutfitController)}] Missing outfit parts for required partTypes: {string.Join(", ", missingOutfitParts)}.",
                    this);
            }
        }

        private bool HasValidDefaultKeyEntryForRequiredPartType(OutfitPartTypeSO requiredPartType)
        {
            if (requiredPartType == null || slotDefaultKeys == null)
                return false;

            var hasValidEntry = false;

            foreach (var entry in slotDefaultKeys)
            {
                if (entry == null || !entry.TryGetPartTypeForEditor(out var entryPartType) || !ReferenceEquals(entryPartType, requiredPartType))
                    continue;

                if (!entry.TryGetOutfitKeyForEditor(out var key) || key == null)
                    continue;

                if (!ReferenceEquals(key.partType, requiredPartType))
                {
                    Debug.LogError(
                        $"[{nameof(EquipmentOutfitController)}] Default key '{key.name}' partType does not match required partType '{requiredPartType.name}'.",
                        this);
                    continue;
                }

                hasValidEntry = true;
            }

            return hasValidEntry;
        }

        private bool HasOutfitPartForRequiredPartType(OutfitPartTypeSO requiredPartType)
        {
            if (requiredPartType == null)
                return false;

            var parts = GetComponentsInChildren<OutfitPartKeyTag>(true);
            foreach (var part in parts)
            {
                if (part == null)
                    continue;

                if (!part.TryGetOutfitKeyForEditor(out var key) || key == null)
                    continue;

                if (ReferenceEquals(key.partType, requiredPartType))
                    return true;
            }

            return false;
        }
#endif
        #endregion
    
    }

    [Serializable]
    public sealed class OutfitSlotDefaultKeyEntry
    {
        public AssetReferenceOutfitPartTypeSO partTypeReference;
        public AssetReferenceOutfitKeySO keyReference;

        [NonSerialized] private OutfitPartTypeSO resolvedPartType;
        [NonSerialized] private OutfitKeySO resolvedKey;
        public OutfitPartTypeSO ResolvedPartType => resolvedPartType;
        public OutfitKeySO ResolvedKey => resolvedKey;

        public async UniTask InitializeAsync(CancellationToken token)
        {
            resolvedPartType = null;
            resolvedKey = null;

            if (partTypeReference != null && partTypeReference.RuntimeKeyIsValid())
                resolvedPartType = await ResourceManager.Instance.ExtractAssetRefAsync(partTypeReference, token);

            if (keyReference == null || !keyReference.RuntimeKeyIsValid())
                return;

            resolvedKey = await ResourceManager.Instance.ExtractAssetRefAsync(keyReference, token);

            if (resolvedPartType == null)
                resolvedPartType = resolvedKey?.partType;
        }

#if UNITY_EDITOR
        public bool TryGetPartTypeForEditor(out OutfitPartTypeSO partType)
        {
            if (partTypeReference != null && partTypeReference.editorAsset is OutfitPartTypeSO editorPartType && editorPartType != null)
            {
                partType = editorPartType;
                return true;
            }

            if (keyReference != null && keyReference.editorAsset is OutfitKeySO editorKey && editorKey != null && editorKey.partType != null)
            {
                partType = editorKey.partType;
                return true;
            }

            partType = null;
            return false;
        }

        public bool TryGetOutfitKeyForEditor(out OutfitKeySO key)
        {
            if (keyReference != null && keyReference.editorAsset is OutfitKeySO editorKey && editorKey != null)
            {
                key = editorKey;
                return true;
            }

            key = null;
            return false;
        }
#endif

    }
}
