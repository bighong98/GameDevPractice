using UnityEngine;
using TH.Resource;
using TH.Attribute.Stat;
using Cysharp.Threading.Tasks;
using System.Threading;
using TH.Core.Service;
using TH.Utils;
using System;

namespace TH.Attribute.Data
{
    [Serializable]
    public class AssetReferenceGameStatSO : AssetReferenceGeneric<GameStatSO>, IAsyncInitializer
    {
        public async UniTask InitializeAsync(CancellationToken token)
        {
            this.Log($"InitializeAsync - {AssetGUID}", Logg.LoggingMode.Completed);
            var result = await ResourceManager.Instance.ExtractAssetRefAsync(this, token);
            
            this.Log($"InitializeAsync - assetRef: {AssetGUID}, result: {result.DisplayName}", Logg.LoggingMode.Completed);
            if (result is IAsyncInitializer i)
                await i.InitializeAsync(token);
        }
    }
}

