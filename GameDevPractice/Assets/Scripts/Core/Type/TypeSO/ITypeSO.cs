using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.Resource
{
    public interface ITypeSO : IAsyncInitializer
    {
        void RefreshStates();
    }

    public interface IAsyncInitializer
    {
        UniTask InitializeAsync(CancellationToken token);
    }
}

