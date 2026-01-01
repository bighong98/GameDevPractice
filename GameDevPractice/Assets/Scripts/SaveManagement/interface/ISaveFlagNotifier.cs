using UnityEngine;

namespace TH.SaveLoad
{
    public interface ISaveFlagNotifier
    {
        void MarkDirty();
        void MarkDeleted();
    }
}


