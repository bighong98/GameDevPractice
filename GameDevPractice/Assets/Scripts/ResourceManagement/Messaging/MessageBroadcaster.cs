using UnityEngine;
using System;

namespace TH.SceneManagement
{
    public sealed class MessageBroadcaster<T> : IMessageBroadcaster<T>
    {
        private event Action<T> Handlers;

        public IBroadcastSubscription Subscribe(Action<T> handler)
        {
            Handlers += handler;
            return new BroadcastSubscription<T>(this, handler);
        }

        public void Report(in T value)
        {
            // 각각의 구독자에서 예외가 나도 다른 구독자 호출은 계속
            var inv = Handlers;
            if (inv == null) return;

            foreach (var @delegate in inv.GetInvocationList())
            {
                var h = (Action<T>)@delegate;
                try { h(value); }
                catch (Exception e) { Debug.LogError($"[Progress] handler threw: {e}"); }
            }
        }

        public void Clear() => Handlers = null;
        
        private sealed class BroadcastSubscription<TValue> : IBroadcastSubscription
        {
            private MessageBroadcaster<TValue> owner;
            private Action<TValue> handler;

            internal BroadcastSubscription(MessageBroadcaster<TValue> owner, Action<TValue> handler)
            {
                this.owner = owner;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (owner == null || handler == null) return;
                
                owner.Handlers -= handler;
                owner = null;
                handler = null;
            }
        }
    }
}
