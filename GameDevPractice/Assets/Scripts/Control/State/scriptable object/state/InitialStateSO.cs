using System;
using UnityEngine;
using TH.Control.State;
using TH.Utils;

// ActionStateMachine의 초기 상태(initialState) 복귀용 가짜 StateSO
// 절대 내부 구현 매서드를 직접 호출하지 않을 것
namespace TH.Control.Data
{
    [CreateAssetMenu(fileName = "InitialStateSO", menuName = "Scriptable Objects/CharacterState/InitialStateSO")]
    public class InitialStateSO : ActionStateSO, IActionState
    {
        private const string Msg =
            "[InitialStateSO] This asset is a pesudo(marker-only) state. " +
            "It must never execute Enter/Update/Exit/Bind";
        
        void IActionState.EnterState(IActionStateController controller)
        {
            Logg.LogError(Msg, this);
        }

        void IActionState.UpdateState(IActionStateController controller)
        {
            Logg.LogError(Msg, this);
        }

        void IActionState.ExitState(IActionStateController controller)
        {
            Logg.LogError(Msg, this);
        }

        void IActionState.BindTransitions(IActionStateController controller, Action<IDisposable> register)
        {
            Logg.LogError(Msg, this);
        }

        IDisposable IActionState.BindTransitionUnlock(IActionStateController controller, Action register)
        {
            Logg.LogError(Msg, this);
            return null;
        }

        #region For Debug (Editor Only)

        #if UNITY_EDITOR
        private static bool _isValidating;
        // 다른 ActionStateSO와 별도의 정책을 사용하므로 base.OnValidate()는 의도적으로 호출하지 않음
        // 임의로 인스펙터 등을 통해 InitialStateSO의 필드를 수정할 경우 강제 제거 및 콘솔 에러 메시지 출력
        private new void OnValidate()
        {
            if (_isValidating) return;
            _isValidating = true;

            try
            {
                // ActionStateSO의 private [SerializeField] 필드들을 SerializedObject로 강제 정리
                var so = new UnityEditor.SerializedObject(this);

                var onEnter = so.FindProperty("onEnterActions");
                var update = so.FindProperty("updateActions");
                var onExit  = so.FindProperty("onExitActions");
                var trans   = so.FindProperty("transitions");

                var allowSelf = so.FindProperty("allowSelfTransition");
                var lockReq   = so.FindProperty("transitionLockRequired");

                bool hasInvalidData =
                    onEnter is { arraySize: > 0 } ||
                    update is { arraySize: > 0 } ||
                    onExit is { arraySize: > 0 } ||
                    trans is { arraySize: > 0 } ||
                    allowSelf is { boolValue: true } ||
                    lockReq is { boolValue: true };

                if (!hasInvalidData)
                    return;

                // 강제 제거
                if (onEnter != null) onEnter.arraySize = 0;
                if (update  != null) update.arraySize = 0;
                if (onExit  != null) onExit.arraySize = 0;
                if (trans   != null) trans.arraySize = 0;

                if (allowSelf != null) allowSelf.boolValue = false;
                if (lockReq   != null) lockReq.boolValue = false;

                // Undo 필요 없으면 WithoutUndo가 더 깔끔(프로젝트 정책에 맞게 선택)
                so.ApplyModifiedPropertiesWithoutUndo();
                UnityEditor.EditorUtility.SetDirty(this);

                Logg.LogError(
                    $"[InitialStateSO] '{name}' is a pseudo state asset (For marker-only). " +
                    "Actions/Transitions/Settings are not allowed and were cleared automatically.",
                    this);
            }
            finally
            {
                _isValidating = false;
            }
        }
#endif

        #endregion
    }
}