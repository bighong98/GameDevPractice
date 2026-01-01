using System.Collections.Generic;
using UnityEngine;

namespace TH.SaveLoad
{
    public interface ISavableEntity_deprecated
    {
        bool IsGlobal { get; }
        string UniqueIdentifier { get; }

        Dictionary<string, object> CaptureState();
        void RestoreState(Dictionary<string, object> state);
    }
}
