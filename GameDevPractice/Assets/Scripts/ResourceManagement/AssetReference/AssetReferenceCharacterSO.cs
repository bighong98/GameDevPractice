using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Character.Data
{
    [Serializable]
    public class AssetReferenceCharacterSO : AssetReferenceGeneric<CharacterTypeSO>, IAsyncInitializer
    {
        public async UniTask InitializeAsync(CancellationToken token)
        {
            this.Log($"InitializeAsync - {AssetGUID}", Logg.LoggingMode.Completed);
            var result = await ResourceManager.Instance.ExtractAssetRefAsync(this, token);
            
            this.Log($"InitializeAsync - assetRef: {AssetGUID}, result: {result.nameString}", Logg.LoggingMode.Completed);
            if (result is IAsyncInitializer i)
                await i.InitializeAsync(token);
        }
    }
}

