using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.Utils
{
    public class LazyValue<T>
    {
        private T _value;
        public bool Initialized { get; private set; }
        private Func<T> _initializer;

        public LazyValue(Func<T> initializer)
        {
            _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
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

            value = default;
            return false;
        }
    }
}


