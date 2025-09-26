using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.Utils
{
    public class LazyValue<T>
    {
        private T _value;
        private Func<T> _initializer;

        public bool Initialized { get; private set; }
        private T Default = default;
        
        public LazyValue(Func<T> initializer, T defaultValue = default)
        {
            _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
            Default = defaultValue;
        }
        
        public T Value
        {
            get { ForceInit(); return _value; }
            set { _value = value; Initialized = true; }
        }

        public void ForceInit()
        {
            if (Initialized) return;
            if (_initializer == null) 
                throw new InvalidOperationException("Invalid initializer");
            
            _value = _initializer();
            Initialized = true;
        }

        public bool TrGet(out T value)
        {
            if (Initialized)
            {
                value = _value;
                return true;
            }

            value = Default;
            return false;
        }
    }
}


