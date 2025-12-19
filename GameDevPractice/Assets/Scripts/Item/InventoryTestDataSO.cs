using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Resource;
using TH.Utils;
using UnityEngine;
using TH.Core.Service;

[CreateAssetMenu(fileName = "InventoryTestDataSO", menuName = "Scriptable Objects/TypeList/InventoryTestDataSO")]
public class InventoryTestDataSO : KeyValueListSO<AssetReferenceItemSO, int>, IAsyncInitializer
{
    public async UniTask InitializeAsync(CancellationToken token)
    {
        if (Items.Count == 0) return;
        foreach (var item in Keys)
        {
            await ResourceManager.Instance.ExtractAssetRefAsync(item, token);
        }
    }
}