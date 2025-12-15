using System;
using Cysharp.Threading.Tasks;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting;

namespace TH.Core
{
    [Preserve]
    public sealed class InputManager : Singleton<InputManager>,
        UserInput.IPlayerActions, UserInput.IGlobalActions, UserInput.IUIActions, UserInput.IQuickSlotActions
    {
        private InputManager()
        {
            InitOnce();
            Init();
        }

        #region 외부 접근용 프로퍼티

        // Input Action
        public UserInput UserInput { get; } = new UserInput();
        public UserInput.GlobalActions GlobalActions => UserInput.Global;
        public UserInput.PlayerActions PlayerActions => UserInput.Player;
        public UserInput.QuickSlotActions QuickSlotActions => UserInput.QuickSlot;
        public UserInput.UIActions UIActions => UserInput.UI;
        // Cached Pointer Position
        public Vector2 PointerPos => currentPointerPos;
        
        #endregion
        
        #region 외부 접근용 이벤트
        
        // global
        public event Action OnEscaped;
        public event Action<Vector2> OnPointerMoved; // 마우스/터치 등의 포인터 움직임 발생시 (UI 팝업과 무관하게 항상 사용 가능)
        // player
        public event Action<Vector2> OnMoved; // 플레이어 캐릭터가 이동시 (현재는 사용x)
        public event Action<Vector2> OnSelected; // 게임 오브젝트에 터치/클릭 시 (팝업UI와 상호작용은 미포함)
        public event Action<Vector2> OnScreenDragged; // 게임 스크린 드래그 시 (팝업UI와 상호작용은 OnDragStarted 혹은 pointerEvent 기반으로 처리 )
        // UI.Click
        public event Action<Vector2> OnUIPointerMoved; // UI 팝업이 활성화된 상태에서 포인터 움직임 발생시
        public event Action<Vector2> OnSingleClicked;
        public event Action<Vector2> OnDoubleClicked; // 더블클릭
        public event Action<Vector2> OnAltClicked; // 보조 입력 발생 시 (마우스 우클릭 등)
        // UI.Drag
        public event Action<Vector2> OnDragStarted; // 드래그 시작 시
        public event Action<Vector2> OnDragEnded; // 드래그 종료 시
        public event Action<Vector2> OnAdditived;
        // QuickSlot
        public event Action OnQuickSlot1Pressed;
        public event Action OnQuickSlot2Pressed;
        public event Action OnQuickSlot3Pressed;
        public event Action OnQuickSlot4Pressed;
        public event Action OnQuickSlot5Pressed;
        
        #endregion
        
        // 캐싱 좌표
        private Vector2 currentPointerPos = Vector2.zero; // update by OnPoint()
        private Vector2 dragStartPosition = Vector2.zero; // update by OnDrag()
        // 내부 플래그
        private bool isDragging = false;
        private bool wasDraggingOneFrameAgo = false;
        
        #region Initialization

        private void InitOnce()
        {
            UserInput.Player.SetCallbacks(this);
            UserInput.Global.SetCallbacks(this);
            UserInput.QuickSlot.SetCallbacks(this);
            UserInput.UI.SetCallbacks(this);
        }

        private void Init()
        {
            // default: GlobalActions, PlayerActions, QuickSlotActions 활성화
            UserInput.Global.Enable();
            UserInput.QuickSlot.Enable();
            UserInput.Player.Enable(); 
        }

        private UniTask Clear()
        {
            UserInput.Global.Disable();
            UserInput.Player.Disable();
        
            UserInput.QuickSlot.Disable();
            UserInput.UI.Disable();

            // userInput.Global.RemoveCallbacks(this);
            // userInput.Player.RemoveCallbacks(this);
            // userInput.UI.RemoveCallbacks(this);
            //
            // userInput?.Disable();
            // userInput?.Dispose();

            return UniTask.CompletedTask;
        }

        #endregion

        private void Update()
        {
            // todo: 제거 후 Input System 사용
            if (Input.GetKeyDown(KeyCode.I))
            {
                UIManager.Instance.ShowPopupUI<TH.UI.InventoryUI>("InventoryUI.prefab");
            }
        }

        #region Player Input Handle // 플레이어 캐릭터 조작에 사용하는 입력
        
        public void OnMove(InputAction.CallbackContext context)
        {
            OnMoved?.Invoke(context.ReadValue<Vector2>());
        }

        public void OnSelect(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed) return;
            
            OnSelected?.Invoke(currentPointerPos);
            Logg.Log($"[InputManager] OnSelect Invoked ({currentPointerPos})", Logg.LoggingMode.Completed);
        }

        public void OnDragScreen(InputAction.CallbackContext context)
        {
            Vector2 delta = context.ReadValue<Vector2>();

            if (context.phase != InputActionPhase.Performed) return;
            if (!(delta.magnitude > 8f)) return;
            
            OnScreenDragged?.Invoke(delta);
            Logg.Log($"[{GetType().Name}] OnScreenDragged({delta})", Logg.LoggingMode.Completed);
        }
        
        #endregion

        #region Global Input Handle // 전역 입력 
        
        public void OnEscape(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                OnEscaped?.Invoke();
            }
        }

        public void OnPoint(InputAction.CallbackContext context)
        {
            currentPointerPos = context.ReadValue<Vector2>();
            OnPointerMoved?.Invoke(currentPointerPos);
        }

        public void OnRelease(InputAction.CallbackContext context)
        {
            switch (context.phase)
            {
                case InputActionPhase.Canceled when !isDragging:
                    OnSingleClicked?.Invoke(currentPointerPos);
                    break;
                case InputActionPhase.Canceled when isDragging:
                    isDragging = false;
                    OnDragEnded?.Invoke(currentPointerPos);
            
                    wasDraggingOneFrameAgo = true;
                    UniTask.Void(async () =>
                    {
                        await UniTask.NextFrame();
                        wasDraggingOneFrameAgo = false;
                    });
                    break;
                default:
                    break;
            }
        }
        
        #endregion

        #region UI Input Handle // UI 상호작용 입력
        
        public void OnClick(InputAction.CallbackContext context)
        {
            if (isDragging || wasDraggingOneFrameAgo) return;
            if (context is not {
                    interaction: UnityEngine.InputSystem.Interactions.MultiTapInteraction,
                    phase: InputActionPhase.Performed }
                || !IsWithoutModifiers()) return;
            
            OnDoubleClicked?.Invoke(currentPointerPos);
            Logg.Log("Double Click Occured", Logg.LoggingMode.Completed);
        }

        public void OnAlt(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed || !IsWithoutModifiers()) return;
            
            OnAltClicked?.Invoke(PointerPos);
            Logg.Log("Alternative Click Occured", Logg.LoggingMode.Completed);
        }

        public void OnDrag(InputAction.CallbackContext context)
        {
            Vector2 delta = context.ReadValue<Vector2>();

            if (context.phase != InputActionPhase.Performed) return;
            if (isDragging || !(delta.magnitude > 2f)) return;
            
            isDragging = true;
            dragStartPosition = PointerPos;
            OnDragStarted?.Invoke(dragStartPosition);
        }

        public void OnPointUI(InputAction.CallbackContext context)
        {
            OnUIPointerMoved?.Invoke(context.ReadValue<Vector2>());
        }

        public void OnAdditive(InputAction.CallbackContext context)
        {
            if (context.phase == InputActionPhase.Performed)
            {
                OnAdditived?.Invoke(PointerPos);
            }
        }
        
        #endregion

        #region QuickSlot Input Handle

        public void OnQuickSlot1(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed) return;
            
            OnQuickSlot1Pressed?.Invoke();
            Logg.Log("[InputManager] QuickSlot1 Pressed", Logg.LoggingMode.Completed);
        }

        public void OnQuickSlot2(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed) return;
            
            OnQuickSlot2Pressed?.Invoke();
            Logg.Log("[InputManager] QuickSlot2 Pressed", Logg.LoggingMode.Completed);
        }

        public void OnQuickSlot3(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed) return;
            
            OnQuickSlot3Pressed?.Invoke();
            Logg.Log("[InputManager] QuickSlot3 Pressed", Logg.LoggingMode.Completed);
        }

        public void OnQuickSlot4(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed) return;
            
            OnQuickSlot4Pressed?.Invoke();
            Logg.Log("[InputManager] QuickSlot4 Pressed", Logg.LoggingMode.Completed);
        }

        public void OnQuickSlot5(InputAction.CallbackContext context)
        {
            if (context.phase != InputActionPhase.Performed) return;
            
            OnQuickSlot5Pressed?.Invoke();
            Logg.Log("[InputManager] QuickSlot5 Pressed", Logg.LoggingMode.Completed);
        }

        #endregion
        
        #region Action Map Handle

        private void SwitchActionMap(InputActionMap actionMap, bool exclusive)
        {
            if (!UserInput.Global.enabled)
                UserInput.Global.Enable();

            if (exclusive)
            {
                // 현재 활성화된 Map들 중 Global이 아닌 것만 Disable
                foreach (var map in UserInput.asset.actionMaps)
                {
                    if (map != UserInput.Global.Get() && map.enabled)
                        map.Disable();
                }
            }
        
            if (!actionMap.enabled)
                actionMap.Enable();
        }

        public void EnableUIActionMap()
        {
            if (!UIActions.enabled)
                UIActions.Enable();
        }

        public void DisableUIActionMap()
        {
            if (UIActions.enabled)
                UIActions.Disable();
        }

        #endregion
        
        #region Pause/Resume Game // 임시기능 (별도의 매니저로 기능 이관 예정)

        private bool isPaused;
        public void PauseGame()
        {
            if (isPaused) return;
        
            isPaused = true;
            Time.timeScale = 0f;
        
            if (PlayerActions.enabled)
                PlayerActions.Disable();
        }

        public void ResumeGame()
        {
            if (!isPaused) return;
        
            isPaused = false;
            Time.timeScale = 1f;
        
            if (!PlayerActions.enabled)
                PlayerActions.Enable();
        }

        #endregion
        
        #region Helper Methods

        private static bool IsWithoutModifiers()
        {
            if (Keyboard.current is { } mod)
            {
                return !(mod.shiftKey.isPressed || mod.ctrlKey.isPressed || mod.altKey.isPressed);
            }

            return true;
        }

        #endregion
    }
}

