using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Attribute;
using TH.Control.State;
using TH.Utils;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "ScanActionSO", menuName = "Scriptable Objects/CharacterAction/ScanActionSO")]

    // ScanActionSO 상태 동작 실행 액션 ScriptableObject
    public class ScanActionSO : CharacterActionSO
    {
        private const int ScanIntervalFrames = 30;
        private LayerMask mask;
        public override void Execute(IActionStateController controller)
        {
            // 30프레임마다만 실행
            if ((Time.frameCount % ScanIntervalFrames) != 0)
                return;
            
            if (!controller.Components.TryGet(out IGameScanner<Health> scanner)) return;
            if (!controller.Components.TryGet(out IFighter fighter)) return;
            
            // 스캔 결과(Health) 수신
            var target = scanner.Scan();
            
            // 결과 전달 (null이면 타겟 해제/유지 정책은 IFighter 구현에 맡김)
            fighter.SetTarget(target);
            
            // var token = controller.StateToken;
            //
            // if (!controller.Components.TryGet(out IGameScanner<Health> scanner)) return;
            // if (!controller.Components.TryGet(out IFighter fighter)) return;
            //
            // InternalScan(scanner, fighter, token).Forget();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            mask = LayerMask.GetMask("Ally", "Character");
        }
#endif

        private async UniTask InternalScan(IGameScanner<Health> scanner, IFighter fighter, CancellationToken stateToken)
        {
            var token = stateToken;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 30프레임 대기
                    await UniTask.DelayFrame(
                        ScanIntervalFrames,
                        cancellationToken: token
                    ).SuppressCancellationThrow();

                    // 스캔 결과 전달
                    var target = scanner.Scan();
                    if (target.IsNotNull())
                        fighter.SetTarget(target);
                }
            }
            catch (Exception e) { Logg.LogError($"[{GetType().Name}] error occured while Scan - {e}", this); }
        }
    }
}