using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    // [CreateAssetMenu(fileName = "CharacterActionSO", menuName = "Scriptable Objects/Action/CharacterActionSO")]
    public abstract class CharacterActionSO : ScriptableObject, ICharacterAction
    {
        public abstract void Execute(IActionStateController controller);
    }
}

