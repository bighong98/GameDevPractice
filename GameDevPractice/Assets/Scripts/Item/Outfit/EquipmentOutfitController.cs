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

        [Header("Outline Cache")]
        [SerializeField] private bool trackRuntimeOutfitRendererChanges = false;
        [SerializeField] private List<Renderer> alwaysIncludeRenderers = new();
        [SerializeField] private bool autoCollectActiveAlwaysIncludeRenderersInEditor;
        [SerializeField] private bool autoCollectSkipContainedRenderersInEditor = true;
        [SerializeField, Range(0.01f, 1f)] private float autoCollectContainedVolumeRatioThreshold = 0.9f;

        private readonly Dictionary<OutfitPartTypeSO, List<OutfitPartKeyTag>> partsByPartType = new();
        private readonly Dictionary<OutfitPartTypeSO, OutfitKeySO> defaultKeyByPartType = new();
        private readonly Dictionary<OutfitPartTypeSO, OutfitKeySO> equippedKeyByPartType = new();

        private bool isInitialized;
        private bool isInitializing;

        private readonly HashSet<Renderer> activeRendererSet = new();
        private readonly List<Renderer> activeRenderers = new();
        private readonly HashSet<OutfitPartKeyTag> subscribedOutfitParts = new();
        private bool activeRendererTopologyDirty = true;
        private bool activeRendererSelectionDirty = true;
        private uint activeRendererVersion = 1;
        private bool hasLoggedFixedCacheSlotChangeWarning;


        private void Awake()
        {
            if (equipHolder == null)
                TryGetComponent(out equipHolder);

            if (autoCollectPartsOnAwake && (outfitParts == null || outfitParts.Count == 0))
                CollectOutfitParts();
            else
                RefreshOutfitPartSubscriptions();

            ClearVisualState();
            MarkActiveRendererTopologyDirty();
        }

        private void OnEnable()
        {
            if (equipHolder != null)
                equipHolder.OnSlotChanged += HandleSlotChanged;

            RefreshOutfitPartSubscriptions();
        }

        private void OnDisable()
        {
            if (equipHolder != null)
                equipHolder.OnSlotChanged -= HandleSlotChanged;

            UnsubscribeOutfitPartEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeOutfitPartEvents();
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
            RefreshOutfitPartSubscriptions();
            MarkActiveRendererTopologyDirty();
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

            if (!trackRuntimeOutfitRendererChanges && !hasLoggedFixedCacheSlotChangeWarning)
            {
                hasLoggedFixedCacheSlotChangeWarning = true;
                if (gameObject.name.Contains("Player")) return; // 플레이어 캐릭터는 아웃라인 대상이 아니므로 경고 로그 출력x

                string characterName = gameObject != null ? gameObject.name : "<unknown>";
                string slotIndexText = slot != null ? slot.Index.ToString() : "<null>";
                Debug.LogWarning(
                    $"[{nameof(EquipmentOutfitController)}] Runtime slot change detected while '{nameof(trackRuntimeOutfitRendererChanges)}' is disabled. Character='{characterName}', SlotIndex={slotIndexText}. Active outline renderer cache remains fixed.",
                    this);
            }

            ApplyAllFromHolder();
        }

        public bool TryGetActiveOutlineRenderers(out IReadOnlyList<Renderer> renderers, out uint version)
        {
            if (!isInitialized)
            {
                renderers = Array.Empty<Renderer>();
                version = activeRendererVersion;
                return false;
            }

            EnsureActiveRendererCache();
            renderers = activeRenderers;
            version = activeRendererVersion;
            return activeRenderers.Count > 0;
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

            MarkActiveRendererSelectionDirty();
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
            MarkActiveRendererSelectionDirty();
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
            RefreshOutfitPartSubscriptions();
            MarkActiveRendererTopologyDirty();
        }

        private void HandleOutfitPartRenderersChanged(OutfitPartKeyTag _)
        {
            MarkActiveRendererTopologyDirty();
        }

        private void RefreshOutfitPartSubscriptions()
        {
            UnsubscribeOutfitPartEvents();
            if (!trackRuntimeOutfitRendererChanges || outfitParts == null)
                return;

            foreach (var part in outfitParts)
            {
                if (part == null || !subscribedOutfitParts.Add(part))
                    continue;

                part.RenderersChanged += HandleOutfitPartRenderersChanged;
            }

            MarkActiveRendererTopologyDirty();
        }

        private void UnsubscribeOutfitPartEvents()
        {
            if (subscribedOutfitParts.Count == 0)
                return;

            foreach (var part in subscribedOutfitParts)
            {
                if (part == null)
                    continue;

                part.RenderersChanged -= HandleOutfitPartRenderersChanged;
            }

            subscribedOutfitParts.Clear();
        }

        private void MarkActiveRendererTopologyDirty()
        {
            if (!trackRuntimeOutfitRendererChanges && isInitialized)
                return;

            activeRendererTopologyDirty = true;
        }

        private void MarkActiveRendererSelectionDirty()
        {
            if (!trackRuntimeOutfitRendererChanges && isInitialized)
                return;

            activeRendererSelectionDirty = true;
        }

        private void EnsureActiveRendererCache()
        {
            if (!activeRendererTopologyDirty && !activeRendererSelectionDirty)
                return;

            RebuildActiveRendererCache();
        }

        private void RebuildActiveRendererCache()
        {
            activeRendererSet.Clear();
            activeRenderers.Clear();

            if (outfitParts != null)
            {
                foreach (var part in outfitParts)
                {
                    if (part == null)
                        continue;

                    var renderers = part.GetCachedRenderersForRuntime();
                    if (renderers == null || renderers.Length == 0)
                        continue;

                    foreach (var renderer in renderers)
                    {
                        AddRendererToActiveCache(renderer);
                    }
                }
            }

            if (alwaysIncludeRenderers != null)
            {
                foreach (var renderer in alwaysIncludeRenderers)
                {
                    AddRendererToActiveCache(renderer);
                }
            }

            activeRendererTopologyDirty = false;
            activeRendererSelectionDirty = false;
            activeRendererVersion++;
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

            if (autoCollectActiveAlwaysIncludeRenderersInEditor)
                CollectActiveAlwaysIncludeRenderersInEditor();

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

        private void CollectActiveAlwaysIncludeRenderersInEditor()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            var activeMeshRenderers = new List<Renderer>(renderers.Length);

            foreach (var renderer in renderers)
            {
                if (!IsActiveMeshRendererForOutlineAutoCollect(renderer))
                    continue;

                activeMeshRenderers.Add(renderer);
            }

            var collected = new List<Renderer>(alwaysIncludeRenderers != null ? alwaysIncludeRenderers.Count : 0);
            var seen = new HashSet<Renderer>();

            if (alwaysIncludeRenderers != null)
            {
                foreach (var existingRenderer in alwaysIncludeRenderers)
                {
                    if (existingRenderer == null || !seen.Add(existingRenderer))
                        continue;

                    collected.Add(existingRenderer);
                }
            }

            int addedCount = 0;
            int skippedContainedCount = 0;

            foreach (var renderer in activeMeshRenderers)
            {
                if (renderer.GetComponent<OutfitPartKeyTag>() != null)
                    continue;

                if (autoCollectSkipContainedRenderersInEditor && IsContainedRendererForOutlineAutoCollect(renderer, activeMeshRenderers))
                {
                    skippedContainedCount++;
                    continue;
                }

                if (!seen.Add(renderer))
                    continue;

                collected.Add(renderer);
                addedCount++;
            }

            alwaysIncludeRenderers = collected;
            autoCollectActiveAlwaysIncludeRenderersInEditor = false;
            EditorUtility.SetDirty(this);

            Debug.Log(
                $"[{nameof(EquipmentOutfitController)}] Auto-collected {addedCount} renderer(s) into '{nameof(alwaysIncludeRenderers)}' and skipped {skippedContainedCount} contained renderer(s).",
                this);
        }

        private bool IsActiveMeshRendererForOutlineAutoCollect(Renderer renderer)
        {
            if (renderer == null || renderer.gameObject == null)
                return false;

            if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer)
                return false;

            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return false;

            return true;
        }

        private bool IsContainedRendererForOutlineAutoCollect(Renderer candidate, List<Renderer> activeMeshRenderers)
        {
            if (candidate == null || activeMeshRenderers == null || activeMeshRenderers.Count <= 1)
                return false;

            var candidateBounds = candidate.bounds;
            var candidateVolume = GetBoundsVolume(candidateBounds);

            for (int i = 0; i < activeMeshRenderers.Count; i++)
            {
                var other = activeMeshRenderers[i];
                if (other == null || ReferenceEquals(other, candidate))
                    continue;

                var otherBounds = other.bounds;
                if (!otherBounds.Contains(candidateBounds.min) || !otherBounds.Contains(candidateBounds.max))
                    continue;

                var otherVolume = GetBoundsVolume(otherBounds);
                if (otherVolume <= 0f)
                    continue;

                if (candidateVolume <= 0f)
                    return true;

                var ratio = candidateVolume / otherVolume;
                if (ratio <= autoCollectContainedVolumeRatioThreshold)
                    return true;
            }

            return false;
        }

        private static float GetBoundsVolume(Bounds bounds)
        {
            var size = bounds.size;
            return Mathf.Abs(size.x * size.y * size.z);
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

        private void AddRendererToActiveCache(Renderer renderer)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return;

            if (activeRendererSet.Add(renderer))
                activeRenderers.Add(renderer);
        }
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
