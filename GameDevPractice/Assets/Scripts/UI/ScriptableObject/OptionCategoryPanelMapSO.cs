using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using UnityEngine;

[CreateAssetMenu(fileName = "OptionCategoryPanelMapSO", menuName = "Scriptable Objects/UI/Option Category Panel Map")]
public class OptionCategoryPanelMapSO : ScriptableObject, IAsyncInitializer
{
    [Serializable]
    public class OptionCategoryPanelEntry
    {
        public Enums.OptionCategory category;
        public string label;
        [NonSerialized] public GameObject panelPrefab;
        [SerializeField] private AssetReferenceGameObject panelPrefabReference;
        [NonSerialized] private bool initialized;

        public async UniTask InitializeAsync(CancellationToken token)
        {
            if (initialized)
            {
                return;
            }

            if (panelPrefabReference != null && panelPrefabReference.RuntimeKeyIsValid())
            {
                var loadedPanelPrefab = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(panelPrefabReference, token);
                if (loadedPanelPrefab != null)
                {
                    panelPrefab = loadedPanelPrefab;
                }
            }

            initialized = true;
        }
    }

    [NonSerialized] private OptionCategoryButton categoryButtonTemplate;
    [SerializeField] private AssetReferenceGameObject categoryButtonTemplateReference;
    [SerializeField] private List<OptionCategoryPanelEntry> entries = new();

    [NonSerialized] private bool initialized;

    public OptionCategoryButton CategoryButtonTemplate => categoryButtonTemplate;
    public IReadOnlyList<OptionCategoryPanelEntry> Entries => entries;

    public async UniTask InitializeAsync(CancellationToken token)
    {
        if (initialized)
        {
            return;
        }

        if (categoryButtonTemplateReference != null && categoryButtonTemplateReference.RuntimeKeyIsValid())
        {
            var loadedTemplate = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(categoryButtonTemplateReference, token);
            if (loadedTemplate != null && loadedTemplate.TryGetComponent<OptionCategoryButton>(out var loadedButtonTemplate))
            {
                categoryButtonTemplate = loadedButtonTemplate;
            }
        }

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                await entry.InitializeAsync(token);
            }
        }

        initialized = true;
    }
}
