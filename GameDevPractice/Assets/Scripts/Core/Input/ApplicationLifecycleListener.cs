using System;
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
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            OnFocusChanged?.Invoke(hasFocus);

            // 포커스를 잃은 경우, 포인터는 무조건 유효하지 않다고 보는 게 안전
            if (!hasFocus)
                NotifyPointerState(false);
        }

        private void OnApplicationPause(bool pause)
        {
            OnPauseChanged?.Invoke(pause);

            if (pause)
                NotifyPointerState(false);
        }

        private void Update()
        {
            if (!Application.isFocused)
                return;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (!_lastPointerInView) NotifyPointerState(true);
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            Vector2 pos = mouse.position.ReadValue();
            bool inView =
                pos.x >= 0 && pos.x <= Screen.width &&
                pos.y >= 0 && pos.y <= Screen.height;

            if (inView != _lastPointerInView)
                NotifyPointerState(inView);
        }


        private void NotifyPointerState(bool inView)
        {
            _lastPointerInView = inView;
            OnPointerInGameViewChanged?.Invoke(inView);
        }
    }
}
