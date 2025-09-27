

using System.Threading;
using Cysharp.Threading.Tasks;

namespace TH.SceneManagement
{
    public interface ISceneLoader
    {
        UniTask LoadSceneAsync(AssetReferenceScene sceneRef, CancellationToken token = default);
        UniTask LoadSceneAsync(string key, CancellationToken token = default);
    }
}

