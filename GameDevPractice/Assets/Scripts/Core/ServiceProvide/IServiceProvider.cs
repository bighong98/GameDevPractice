using System;

namespace TH.Core.Service
{
    public interface IServiceProvider
    {
        T Get<T>() where T : class;
        object Get(Type type); // for InternalProvider
        bool IsRegistered<T>() where T : class; // for InternalProvider
    }
}

