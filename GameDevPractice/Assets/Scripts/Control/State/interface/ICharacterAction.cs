using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    public interface ICharacterAction
    {
        void Execute(IActionStateController controller);
    }
}

