// 달리기 이동 실행 액션 에셋 스크립트
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Control.Movement;
using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "RunActionSO", menuName = "Scriptable Objects/CharacterAction/RunActionSO")]
    // RunActionSO 상태 동작 실행 액션 ScriptableObject
    public class RunActionSO : CharacterActionSO, IOnEnterLoopAction
    {
public override void Execute(IActionStateController controller)
        {
            if (controller.Components.TryGet(out TH.Combat.ISkillController skillController) &&
                skillController.HasExecutingSkill &&
                !skillController.CanMoveWhileCasting)
            {
                return;
            }

            if (!controller.Components.TryGet(out IMover mover)) return;
            
            mover.Move(MoveType.Run);
        }

        public async UniTask ExecuteLoopAsync(IActionStateController controller, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Execute(controller);
                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }
        }

    }
}
