// 대기 애니메이션 제어 액션 에셋 스크립트
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Control.State;
using UnityEngine;

namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "AnimateIdleSO", menuName = "Scriptable Objects/CharacterAction/AnimateAction/AnimateIdleSO")]
    // AnimateIdleSO 애니메이션 제어 액션 ScriptableObject
    public class AnimateIdleSO : CharacterActionSO
    {
        private static readonly int ForwardSpeed = Animator.StringToHash("forwardSpeed");

        [SerializeField, Range(0f, 1f)] private float idleTransitionDuration = 0.2f;
        [SerializeField, Min(0f)] private float forwardSpeedBlendOutDuration = 0.12f;

        public override void Execute(IActionStateController controller)
        {
            if (controller == null ||
                !controller.Components.TryGet(out Animator animator) ||
                animator == null)
            {
                return;
            }

            if (animator.GetCurrentAnimatorStateInfo(AnimatorBaseLayer).shortNameHash != LocomotionASSHash)
            {
                animator.CrossFade(
                    stateHashName: LocomotionASSHash,
                    normalizedTransitionDuration: Mathf.Clamp01(idleTransitionDuration),
                    layer: AnimatorBaseLayer);
            }

            BlendForwardSpeedToZeroAsync(animator, controller.StateToken).Forget();
        }

        private async UniTaskVoid BlendForwardSpeedToZeroAsync(Animator animator, CancellationToken token)
        {
            float duration = Mathf.Max(0f, forwardSpeedBlendOutDuration);
            if (duration <= Mathf.Epsilon)
            {
                animator.SetFloat(ForwardSpeed, 0f);
                return;
            }

            float startForwardSpeed = animator.GetFloat(ForwardSpeed);
            float elapsed = 0f;

            while (!token.IsCancellationRequested && animator != null && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                animator.SetFloat(ForwardSpeed, Mathf.Lerp(startForwardSpeed, 0f, t));
                await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            }

            if (!token.IsCancellationRequested && animator != null)
            {
                animator.SetFloat(ForwardSpeed, 0f);
            }
        }
    }
}


