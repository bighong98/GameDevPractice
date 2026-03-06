using System;
using TH.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TH.Core.Input
{
    public interface IApplicationLifecycleListener
    {
        event Action<bool> OnFocusChanged;
        event Action<bool> OnPauseChanged;
        event Action<bool> OnPointerInGameViewChanged;
    }
    
    public sealed class ApplicationLifecycleListener : MonoBehaviour, IApplicationLifecycleListener
    {
        public event Action<bool> OnFocusChanged;
        public event Action<bool> OnPauseChanged;

        /// <summary>
        /// true  : 포인터가 게임 화면 내부
        /// false : 포인터가 화면 외부 (또는 포커스 없음)
        /// </summary>
        public event Action<bool> OnPointerInGameViewChanged;

        private bool _lastPointerInView = true;

        private void Awake()
        {
            InputManager.Instance.RegisterListener(this);
            LogLifecycleDebug($"Awake registered listener. focused={Application.isFocused}, pointerInView={_lastPointerInView}");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            OnFocusChanged?.Invoke(hasFocus);
            LogLifecycleDebug($"OnApplicationFocus({hasFocus})");

            // 포커스를 잃은 경우, 포인터는 무조건 유효하지 않다고 보는 게 안전
            if (!hasFocus)
                NotifyPointerState(false);
        }

        private void OnApplicationPause(bool pause)
        {
            OnPauseChanged?.Invoke(pause);
            LogLifecycleDebug($"OnApplicationPause({pause})");

            if (pause)
                NotifyPointerState(false);
        }
        private void Update()
        {
            if (!Application.isFocused)
                return;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (!_lastPointerInView)
                {
                    LogLifecycleDebug("Update detected locked cursor. Restoring pointer-in-view=true");
                    NotifyPointerState(true);
                }
                return;
            }

            // Standalone 빌드 환경에서는 포인터 좌표계가 모니터/해상도 설정에 따라
            // 게임 뷰 기준과 다를 수 있어 false 판정이 발생할 수 있다.
            // 좌표 기반으로 false 전환하지 않고, true 복구만 수행한다.
            var pointer = Pointer.current;
            if (pointer == null)
            {
                if (!_lastPointerInView)
                {
                    LogLifecycleDebug("Update detected missing pointer. Restoring pointer-in-view=true");
                    NotifyPointerState(true);
                }
                return;
            }

            Vector2 pos = pointer.position.ReadValue();
            bool inView =
                pos.x >= 0 && pos.x <= Screen.width &&
                pos.y >= 0 && pos.y <= Screen.height;

            if (inView && !_lastPointerInView)
            {
                LogLifecycleDebug($"Update detected pointer re-entry at {pos}");
                NotifyPointerState(true);
            }
        }


        private void NotifyPointerState(bool inView)
        {
            _lastPointerInView = inView;
            LogLifecycleDebug(
                $"NotifyPointerState({inView}) focused={Application.isFocused}, lockState={Cursor.lockState}, screen=({Screen.width},{Screen.height})");
            OnPointerInGameViewChanged?.Invoke(inView);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogLifecycleDebug(string message)
        {
            Logg.Log($"[CamDebug][Lifecycle] {message}", Logg.LoggingMode.Completed);
        }
#else
        private static void LogLifecycleDebug(string message) { }
#endif
    }
}
