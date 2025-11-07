using System;
using UnityEngine;

namespace TH.SceneManagement
{
    public interface IProgressBroadcaster
    {
        IProgressSubscription Subscribe(Action<float> handler);
        
        void Report(float value);
        void Clear();
    }
}

