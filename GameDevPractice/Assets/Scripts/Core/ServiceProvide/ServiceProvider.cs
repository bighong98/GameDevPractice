using System;
using System.Collections.Generic;
using TH.Utils;

namespace TH.Core.Service
{
    public class ServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly object _gate = new();
        
        public T Get<T>() where T : class
        {
            if (TryGet(out T service))
            {
                Logg.Log($"[{nameof(ServiceProvider)}.{nameof(Get)}] Service '{typeof(T)} is provided'", Logg.LoggingMode.Completed);
                return service;
            }
            
            Logg.LogError($"[{nameof(ServiceProvider)}] Service '{typeof(T)}' not found");
            return null;
        }

        public bool TryGet<T>(out T service) where T : class
        {
            lock (_gate)
            {
                if (_services.TryGetValue(typeof(T), out var obj) && obj is T t)
                {
                    Logg.Log($"[{nameof(ServiceProvider)}.{nameof(TryGet)}] Service '{typeof(T)} is provided'", Logg.LoggingMode.Completed);
                    service = t;
                    return true;
                }
            }

            service = null;
            return false;
        }

        public object Get(Type type)
        {
            throw new NotImplementedException();
        }

        public bool IsRegistered<T>() where T : class
        {
            throw new NotImplementedException();
        }

        internal void Register<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            lock (_gate)
            {
                var key = typeof(T);
                if (_services.ContainsKey(key))
                {
                    Logg.LogError($"Service {key.Name} duplicated register");
                    return;
                }
                
                Logg.Log($"[{nameof(ServiceLocator)}.{nameof(Register)}] new service registered: {instance.GetType().Name}", Logg.LoggingMode.Completed);
                _services[key] = instance;
            }
        }

        internal void Replace<T>(T instance) where T : class
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            lock (_gate)
            {
                Logg.Log($"[{nameof(ServiceLocator)}.{nameof(Replace)}] new service registered: {instance.GetType().Name}", Logg.LoggingMode.Completed);
                _services[typeof(T)] = instance;
            }
        }

        internal bool UnRegister<T>() where T : class
        {
            lock (_gate)
            {
                return _services.Remove(typeof(T));
            }
        }

        internal void Clear()
        {
            lock (_gate)
            {
                _services.Clear();
            }
        }
    }
}

