using System;
using System.Collections.Generic;
using TH.Utils;

namespace TH.Core.Service
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, Func<IServiceProvider, object>> _factories = new();
        private static readonly Dictionary<Type, object> _instances = new();
        private static readonly object _gate = new();

        [ThreadStatic] private static HashSet<Type> _resolving;

        public static void Register<TService>(TService instance) where TService : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var t = typeof(TService);

            lock (_gate)
            {
                if (_instances.ContainsKey(t) || _factories.ContainsKey(t))
                {
                    Logg.LogError($"[ServiceLocator] service '{t.Name}' is already registered");
                    return;
                }

                _instances[t] = instance;
            }
        }

        public static void Register<TService>(Func<IServiceProvider, TService> factory) where TService : class
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            var t = typeof(TService);

            lock (_gate)
            {
                if (_instances.ContainsKey(t) || _factories.ContainsKey(t))
                {
                    Logg.LogError($"[ServiceLocator] service '{t.Name}' is already registered");
                    return;
                }
            }

            _factories[t] = factory;
        }

        public static TService Get<TService>() where TService : class
            => (TService)Get(typeof(TService));

        public static bool IsRegistered<TService>() where TService : class
        {
            var t = typeof(TService);
            lock (_gate)
                return _instances.ContainsKey(t) || _factories.ContainsKey(t);
        }

        public static object Get(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            lock (_gate)
            {
                if (_instances.TryGetValue(type, out var cached))
                    return cached;
            }

            Func<IServiceProvider, object> factory;

            lock (_gate)
            {
                if (!_factories.TryGetValue(type, out factory))
                {
                    Logg.LogError($"[ServiceLocator] service '{type.Name} is not registered'");
                    return null;
                }
            }

            _resolving ??= new HashSet<Type>();
            if (!_resolving.Add(type))
                throw new InvalidOperationException($"Circular dependency detected while resolving {type.Name}.");

            object created = null;
            try
            {
                created = factory(InternalProvider.Instance);
                
                if (created == null)
                    throw new InvalidOperationException($"Factory for {type.Name} failed.");

                lock (_gate)
                {
                    if (_instances.TryGetValue(type, out var existing))
                        return existing;

                    _instances[type] = created;
                }
            }
            finally { _resolving.Remove(type); }

            return created;
        }
        
        private sealed class InternalProvider : IServiceProvider
        {
            public static readonly InternalProvider Instance = new();

            public T Get<T>() where T : class => ServiceLocator.Get<T>();
            public object Get(Type type) => ServiceLocator.Get(type);
            public bool IsRegistered<T>() where T : class => ServiceLocator.IsRegistered<T>();
        }

        // 필요시 테스트/리셋용
        public static void ResetForTests()
        {
            lock (_gate)
            {
                _instances.Clear();
                _factories.Clear();
            }
            _resolving = null;
        }
    }
}


