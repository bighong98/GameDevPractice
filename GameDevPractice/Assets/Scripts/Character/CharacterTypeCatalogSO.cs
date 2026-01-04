using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Character.Data;
using TH.Resource;
using UnityEngine;

[CreateAssetMenu(fileName = "CharacterTypeCatalogSO", menuName = "Scriptable Objects/Catalog/CharacterTypeCatalogSO")]
public class CharacterTypeCatalogSO : ScriptableObject, IAsyncInitializer
{
    [SerializeField] private List<AssetReferenceCharacterSO> list = new List<AssetReferenceCharacterSO>();
    
    public async UniTask InitializeAsync(CancellationToken token)
    {
        foreach (var a in list)
        {
            if (a is IAsyncInitializer asyncInitializer)
                await asyncInitializer.InitializeAsync(token);
        }
    }
}
