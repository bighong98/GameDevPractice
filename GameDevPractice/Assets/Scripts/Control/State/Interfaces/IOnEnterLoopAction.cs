using System.Threading;
using Cysharp.Threading.Tasks;

namespace TH.Control.State
{
    public interface IOnEnterLoopAction
    {
        UniTask ExecuteLoopAsync(IActionStateController controller, CancellationToken token);
    }
}
