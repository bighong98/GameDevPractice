using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Character.Data;
using TH.Resource;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterDataCatalogSO", menuName = "Scriptable Objects/Catalog/CharacterDataCatalogSO")]
public class CharacterDataCatalogSO : ScriptableObject, IAsyncInitializer
{
    [SerializeField] private List<AssetReferenceCharacterSO> list = new List<AssetReferenceCharacterSO>();
    
    public async UniTask InitializeAsync(CancellationToken token)
    {
        foreach (var assetRef in list)
        {
            if (assetRef is IAsyncInitializer asyncInitializer)
                await asyncInitializer.InitializeAsync(token);
        }
    }
}
