using RPG.Saving;
using UnityEngine;

namespace TH.SaveLoad
{
    public interface ISaveRegistry
    {
        void Register(ISavableWithId savable);
        void UnRegister(ISavableWithId savable);
    }
}
