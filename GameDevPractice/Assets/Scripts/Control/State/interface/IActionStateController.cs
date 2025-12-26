using TH.Control.Data;
using TH.Utils;
using UnityEngine;

namespace TH.Control.State
{
    public interface IActionStateController
    {
        ComponentProvider Components { get; }
        void TransitionToState(IActionState nextState, bool force = false);
    }
}

