using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.Resource
{
    public interface ITypeSO : IAsyncInitializer
    {
        
    }

    public interface IAsyncInitializer
    {
        UniTask InitializeAsync(CancellationToken token);
    }
}

