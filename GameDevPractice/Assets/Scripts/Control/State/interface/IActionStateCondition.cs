using System;
using UnityEngine;

namespace TH.Control.State
{
    public interface IActionStateCondition
    {
        bool Decide(IActionStateController controller);
        IDisposable Bind(IActionStateController controller, Action onTriggered);
    }
}

