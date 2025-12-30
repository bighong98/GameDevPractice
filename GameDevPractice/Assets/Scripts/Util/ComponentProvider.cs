using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Utils
{
    public class ComponentProvider
    {
        private readonly GameObject _owner;
        private readonly Dictionary<Type, object> _cache = new();
        
        public ComponentProvider(GameObject owner) => _owner = owner;
        
        public T Get<T>() where T : class {
            if (_cache.TryGetValue(typeof(T), out var val)) return (T)val;
        
            var component = _owner.GetComponent<T>();
            if (component != null) _cache[typeof(T)] = component;
            return component;
        }

        public bool TryGet<T>(out T value) where T : class
        {
            // 캐시 우선 탐색
            if (_cache.TryGetValue(typeof(T), out var val) && val is T castVal)
            {
                value = castVal;
                return true;
            }
            // TryComponent 시도
            if (_owner.TryGetComponent(out value))
            {
                _cache[typeof(T)] = value;
                return true;
            }
            
            Logg.LogWarning($"[{GetType().Name}] TryGet - {_owner.name} requires {typeof(T).Name} but there is not");

            value = null;
            return false;
        }

        public void Register<T>(T instance) where T : class
        {
            _cache[typeof(T)] = instance;
        }
    }
}

