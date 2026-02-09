using System.Threading;
using TH.Control.Data;
using TH.Utils;
using UnityEngine;
using System;

namespace TH.Control.State
{
    public interface IActionStateController
    {
        ComponentProvider Components { get; }
        CancellationToken StateToken { get; }
        bool IsTransitionLocked { get; }
        
        void TransitionToState(IActionState nextState, bool ignoreLock = false);
        IDisposable AcquireTransitionLock(object owner = null);
        void HandleConditionTriggered(
            IActionStateCondition condition,
            IActionState destination,
            bool isGlobal,
            bool ignoreForce = false);
    }
}

