using System;
using UnityEngine;

namespace TH.Utils
{
    public sealed class DisposableDelegate : IDisposable
    {
        public static readonly IDisposable Empty = new DisposableDelegate(null);
        private Action _handler;

        public DisposableDelegate(Action handler)
        {
            _handler = handler;
        }
        
        public void Dispose()
        {
            _handler?.Invoke();
            _handler = null;
        }
    }
}

