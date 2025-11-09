using System.Threading;
using Cysharp.Threading.Tasks;
using RPG.Saving;
using UnityEngine;

namespace TH.SaveLoad
{
    public interface ISaveSystem
    {
        UniTask LoadLastScene(string saveFile);
        
        UniTask SaveAsync(string saveFile, SceneEntry sceneEntry = null);
        UniTask DeleteAsync(string saveFile);
        UniTask LoadAsync(string saveFile);

        void Register(ISavableWithId savable);
        void UnRegister(ISavableWithId savable);

        void RegisterTesting(ISavableTesting savable, CancellationToken token = default);
        void UnRegisterTesting(ISavableTesting savable, CancellationToken token = default);
    }
}

