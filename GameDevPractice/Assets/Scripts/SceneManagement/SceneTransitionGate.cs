using System.Threading;
using Cysharp.Threading.Tasks;

namespace TH.SceneManagement
{
    public sealed class SceneTransitionGate
    {
        private readonly UniTaskCompletionSource _tcs = new UniTaskCompletionSource();
        private readonly int _transitionId;

        // 0=active, 1=completed, 2=canceled, 3=invalid(no-op)
        private int _state;

        internal SceneTransitionGate(int transitionId)
        {
            _transitionId = transitionId;
            _state = 0;
        }

        internal static SceneTransitionGate CreateCompletedNoop()
        {
            var g = new SceneTransitionGate(-1)
            {
                _state = 1
            };
            g._tcs.TrySetResult();
            return g;
        }

        public UniTask Task => _tcs.Task;

        public void TryComplete()
        {
            if (_state != 0) return;
            _state = 1;
            _tcs.TrySetResult();
        }

        internal void TryCancel(int transitionId, CancellationToken token)
        {
            if (transitionId != _transitionId) return;
            if (_state != 0) return;
            _state = 2;
            _tcs.TrySetCanceled(token);
        }

        internal void Invalidate(int transitionId)
        {
            if (transitionId != _transitionId) return;
            if (_state != 0) return; // 완료/취소된 건 건드리지 않음
            _state = 3;              // invalid
            // invalid 상태에서는 tcs를 건드리지 않음 (전환이 끝났으니 더 이상 await 대상도 아님)
        }
    }
}
