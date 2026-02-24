using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TH.Item
{
    [DisallowMultipleComponent]
    public sealed class OutfitPartKeyTag : MonoBehaviour
    {
        [SerializeField] private AssetReferenceOutfitKeySO outfitKeyReference;
        [SerializeField] private bool includeChildRenderers = true;

        [NonSerialized] private OutfitKeySO resolvedKey;
        [NonSerialized] private bool isInitialized;
        
        public event Action<OutfitPartKeyTag> RenderersChanged;
        [NonSerialized] private Renderer[] cachedRenderers;

        public bool HasResolvedKey => resolvedKey != null;
        public OutfitKeySO ResolvedKey => resolvedKey;

        private void Awake()
        {
            CacheRenderers();
        }

        private void OnTransformChildrenChanged()
        {
            cachedRenderers = null;
            RenderersChanged?.Invoke(this);
        }

        public async UniTask InitializeAsync(CancellationToken token = default)
        {
            if (isInitialized) return;
            isInitialized = true;

            resolvedKey = await LoadKeyAsync(token);
        }

        public void SetVisible(bool visible)
        {
            var renderers = GetRenderers();
            if (renderers == null || renderers.Length == 0)
                return;

            foreach (var renderer in renderers)
            {
                if (renderer == null)
                    continue;

                renderer.enabled = visible;
            }
        }

        public Renderer[] GetCachedRenderersForRuntime()
        {
            return GetRenderers();
        }


        public bool IsMatch(OutfitKeySO other)
        {
            if (other == null || resolvedKey == null)
                return false;

            if (ReferenceEquals(other, resolvedKey))
                return true;

            if (!string.Equals(resolvedKey.id, other.id, StringComparison.Ordinal))
                return false;

            if (resolvedKey.partType == null || other.partType == null)
                return false;

            return ReferenceEquals(resolvedKey.partType, other.partType);
        }

        private async UniTask<OutfitKeySO> LoadKeyAsync(CancellationToken token)
        {
            if (outfitKeyReference == null || !outfitKeyReference.RuntimeKeyIsValid())
                return null;

            return await ResourceManager.Instance.ExtractAssetRefAsync(outfitKeyReference, token);
        }

        private Renderer[] GetRenderers()
        {
            if (cachedRenderers == null)
                CacheRenderers();

            return cachedRenderers;
        }

        private void CacheRenderers()
        {
            cachedRenderers = includeChildRenderers
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
        }

#if UNITY_EDITOR
        public bool TryGetOutfitKeyForEditor(out OutfitKeySO key)
        {
            if (resolvedKey != null)
            {
                key = resolvedKey;
                return true;
            }

            if (outfitKeyReference != null && outfitKeyReference.editorAsset is OutfitKeySO editorKey)
            {
                key = editorKey;
                return true;
            }

            key = null;
            return false;
        }

        public bool IsVisibleForEditor()
        {
            var renderers = GetRenderers();
            if (renderers == null || renderers.Length == 0)
                return false;

            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    return true;
            }

            return false;
        }

        public void SetOutfitKeyForEditor(OutfitKeySO key)
        {
            if (key == null)
            {
                outfitKeyReference = null;
                resolvedKey = null;
                EditorUtility.SetDirty(this);
                return;
            }

            outfitKeyReference = new AssetReferenceOutfitKeySO(key);
            resolvedKey = key;
            EditorUtility.SetDirty(this);
        }
#endif
    }
}
