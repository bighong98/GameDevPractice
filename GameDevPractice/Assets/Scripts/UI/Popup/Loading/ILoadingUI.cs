using System.Threading;
using Cysharp.Threading.Tasks;

namespace TH.SceneManagement
{
    public interface ILoadingUI
    {
        void SetProgress(float ratio);
        UniTask ShowAsync(CancellationToken externalToken);
        UniTask HideAsync(CancellationToken externalToken);
    }
}

