using System.Threading;
using Cysharp.Threading.Tasks;
using TH.SaveLoad;
using UnityEngine;

namespace TH.SaveLoad
{
    public interface ISaveSystem
    {
        UniTask LoadLastScene(string saveFile = null);
        
        UniTask SaveAsync(string saveFile = null, SceneEntry sceneEntry = null);
        UniTask DeleteAsync(string saveFile);
        UniTask LoadAsync(string saveFile = null);

        // void RegisterEntity(ISavableEntity entity, bool saveImmediately = false, CancellationToken token = default);
        // void UnRegisterEntity(ISavableEntity savable, CancellationToken token = default);
    }
}

