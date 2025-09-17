using UnityEngine;

namespace TH.Core.Service
{
    public interface IServiceProvider
    {
        T Get<T>() where T : class;
        bool TryGet<T>(out T service) where T : class;
    }
}

