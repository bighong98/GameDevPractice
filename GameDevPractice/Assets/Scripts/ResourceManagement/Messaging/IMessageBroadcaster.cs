using System;
using UnityEngine;

namespace TH.SceneManagement
{
    public interface IMessageBroadcaster<T>
    {
        IBroadcastSubscription Subscribe(Action<T> handler);
        
        void Report(in T value);
        void Clear();
    }
}

