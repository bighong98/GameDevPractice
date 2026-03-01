// 걷기 이동 실행 액션 에셋 스크립트
using TH.Control.Movement;
using UnityEngine;
using TH.Control.State;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "WalkActionSO", menuName = "Scriptable Objects/CharacterAction/WalkActionSO")]
    // WalkActionSO 상태 동작 실행 액션 ScriptableObject
    public class WalkActionSO : CharacterActionSO
    {
        public override void Execute(IActionStateController controller)
        {
            if (!controller.Components.TryGet(out IMover mover)) return;
            
            mover.Move(MoveType.Walk);
        }
    }
}