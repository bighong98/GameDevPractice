using UnityEngine;
using System;

namespace TH.SceneManagement
{
    public sealed class ProgressBroadcaster : IProgressBroadcaster
    {
        private event Action<float> Handlers;

        private sealed class ProgressSubscription : IProgressSubscription
        {
            private ProgressBroadcaster owner;
            private Action<float> handler;

            internal ProgressSubscription(ProgressBroadcaster owner, Action<float> handler)
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

        public IProgressSubscription Subscribe(Action<float> handler)
        {
            Handlers += handler;
            return new ProgressSubscription(this, handler);
        }

        public void Report(float value)
        {
            // 각각의 구독자에서 예외가 나도 다른 구독자 호출은 계속
            var inv = Handlers;
            if (inv == null) return;

            foreach (var @delegate in inv.GetInvocationList())
            {
                var h = (Action<float>)@delegate;
                try { h(value); }
                catch (Exception e) { Debug.LogError($"[Progress] handler threw: {e}"); }
            }
        }

        public void Clear() => Handlers = null;
    }
}
