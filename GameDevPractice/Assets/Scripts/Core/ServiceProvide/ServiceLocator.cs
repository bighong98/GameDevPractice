using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Core.Service
{
    public static class ServiceLocator
    {
        private static IServiceProvider _provider = new ServiceProvider();
        public static IServiceProvider Provider => _provider;

        public static void SetProvider(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public static T Require<T>() where T : class
        {
            if (_provider == null)
            {
                throw new InvalidOperationException($"[{nameof(ServiceLocator)}.{nameof(Require)}] Provider is null");
            }

            return _provider.Get<T>();
        }

        public static T Get<T>() where T : class => Require<T>();

        public static bool TryGet<T>(out T service) where T : class
        {
            service = null;
            return _provider != null && _provider.TryGet(out service);
        }

        public static void Register<T>(T instance) where T : class
        {
            if (_provider is ServiceProvider sp) { sp.Register(instance); return; } // 현시점 최상위 IServiceProvider 구현 클래스 타입을 사용
            throw new InvalidOperationException("Current provider not support registration");
        }
        
        public static void Replace<T>(T instance) where T : class
        {
            if (_provider is ServiceProvider sp) { sp.Replace(instance); return; } // 현시점 최상위 IServiceProvider 구현 클래스 타입을 사용
            throw new InvalidOperationException("Current provider not support replacement");
        }
        
        public static void UnRegister<T>() where T : class
        {
            if (_provider is ServiceProvider sp) { sp.UnRegister<T>(); return; } // 현시점 최상위 IServiceProvider 구현 클래스 타입을 사용
            throw new InvalidOperationException("Current provider not support replacement");
        }

        public static void ClearAll()
        {
            if (_provider is ServiceProvider sp) { sp.Clear(); return; } // 현시점 최상위 IServiceProvider 구현 클래스 타입을 사용
            throw new InvalidOperationException("Current provider not support replacement");
        }
    }
}


