using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using TH.UI;
using TH.Core;
using TH.Utils;

public class InputManager : Singleton<InputManager>, UserInput.IPlayerActions, UserInput.IGlobalActions, UserInput.IUIActions, UserInput.IQuickSlotActions
{
    // 외부 접근용 프로퍼티
    private UserInput userInput; // input action asset 자동생성 클래스
    public UserInput UserInput => userInput;
    public UserInput.GlobalActions GlobalActions => UserInput.Global;
    public UserInput.PlayerActions PlayerActions => UserInput.Player;
    public UserInput.QuickSlotActions QuickSlotActions => UserInput.QuickSlot;
    public UserInput.UIActions UIActions => UserInput.UI;

    #region 외부 접근용 인풋 이벤트
    // global
    public event Action OnEscaped;
    public event Action<Vector2> OnPointerMoved; // 마우스/터치 등의 포인터 움직임 발생시 (UI 팝업과 무관하게 항상 사용 가능)
    
    // player
    public event Action<Vector2> OnMoved; // 플레이어 캐릭터가 이동시 (현재는 사용x)
    public event Action<Vector2> OnSelected; // 게임 오브젝트에 터치/클릭 시 (팝업UI와 상호작용은 미포함)
    
    // UI
    public event Action<Vector2> OnUIPointerMoved; // UI 팝업이 활성화된 상태에서 포인터 움직임 발생시
    public event Action<Vector2> OnSingleClicked;
    public event Action<Vector2> OnDoubleClicked; // 더블클릭
    public event Action<Vector2> OnAltClicked; // 보조 입력 발생 시 (마우스 우클릭 등)
    // public event Action<Vector2> OnHolded;
    
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
    
    public Vector2 PointerPos => currentPointerPos;
    
    // 캐싱 좌표
    private Vector2 currentPointerPos = Vector2.zero; // update by OnPoint()
    private Vector2 dragStartPosition = Vector2.zero; // update by OnDrag()

    private bool isDragging = false;
    private bool wasDraggingOneFrameAgo = false;
    private CancellationTokenSource clickCTS;

    #region Initialization

    protected override void InitOnce()
    {
        base.InitOnce();
        userInput = new UserInput();
        
        userInput.Player.SetCallbacks(this);
        userInput.Global.SetCallbacks(this);
        
        userInput.QuickSlot.SetCallbacks(this);
        userInput.UI.SetCallbacks(this);
    }

    protected override void Init()
    {
        base.Init();
        // default: GlobalActions, PlayerActions 활성화

        userInput.Global.Enable();
         
        userInput.QuickSlot.Enable();
        userInput.Player.Enable(); 
    }

    protected override UniTask Clear()
    {
        base.Clear();
        
        userInput.Global.Disable();
        userInput.Player.Disable();
        
        userInput.QuickSlot.Disable();
        userInput.UI.Disable();

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
        if (context.phase == InputActionPhase.Performed)
        {
            Logg.Log($"[InputManager] OnSelect Invoked ({currentPointerPos})", Logg.LoggingMode.Completed);
            OnSelected?.Invoke(currentPointerPos);
        }
    }

    #endregion

    #region Global Input Handle // 전역 입력 

    public void OnEscape(InputAction.CallbackContext context) // ESC 등
    {
        if (context.phase == InputActionPhase.Performed)
        {
            OnEscaped?.Invoke();
        }
    }

    public void OnPoint(InputAction.CallbackContext context) // 포인터 이동 감지
    {
        currentPointerPos = context.ReadValue<Vector2>();
        OnPointerMoved?.Invoke(currentPointerPos);
    }
    
    public void OnRelease(InputAction.CallbackContext context) // 터치/클릭이 해제되었을 때 (더블클릭 등 처리 목적)
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

    public void OnClick(InputAction.CallbackContext context) // 더블클릭 전용
    {
        if (isDragging || wasDraggingOneFrameAgo) return;

        if (context is { interaction: UnityEngine.InputSystem.Interactions.MultiTapInteraction, phase: InputActionPhase.Performed }
            && IsWithoutModifiers())
        {
            Logg.Log("Double Click Occured", Logg.LoggingMode.Completed);
            OnDoubleClicked?.Invoke(currentPointerPos);
        }
    }
    
    public void OnDrag(InputAction.CallbackContext context) // 드래그
    {
        Vector2 delta = context.ReadValue<Vector2>();

        if (context.phase == InputActionPhase.Performed)
        {
            if (!isDragging && delta.magnitude > 2f)
            {
                isDragging = true;
                dragStartPosition = PointerPos;
                OnDragStarted?.Invoke(dragStartPosition);
            }
        }
    }

    public void OnHold(InputAction.CallbackContext context) // 홀드 (클릭 상태를 움직이지 않고 유지)
    {
        // OnHolded?.Invoke(context.ReadValue<Vector2>()); // 현재 미구현 추후 구현 예정
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

    public void OnAlt(InputAction.CallbackContext context) // 마우스 우클릭 등 보조 입력장치 처리
    {
        if (context.phase == InputActionPhase.Performed && IsWithoutModifiers())
        {
            Logg.Log("Alternative Click Occured", Logg.LoggingMode.Completed);
            OnAltClicked?.Invoke(PointerPos);
        }
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

    #region Helper Function

    private static bool IsWithoutModifiers()
    {
        if (Keyboard.current is { } mod)
        {
            return !(mod.shiftKey.isPressed || mod.ctrlKey.isPressed || mod.altKey.isPressed);
        }

        return true;
    }

    #endregion


    #region QuickSlot Input Handle

    public void OnQuickSlot1(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            Logg.Log("[InputManager] QuickSlot1 Pressed", Logg.LoggingMode.Completed);
            OnQuickSlot1Pressed?.Invoke();
        }
    }

    public void OnQuickSlot2(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            Logg.Log("[InputManager] QuickSlot2 Pressed", Logg.LoggingMode.Completed);
            OnQuickSlot2Pressed?.Invoke();
        }
    }

    public void OnQuickSlot3(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            Logg.Log("[InputManager] QuickSlot3 Pressed", Logg.LoggingMode.Completed);
            OnQuickSlot3Pressed?.Invoke();
        }
    }

    public void OnQuickSlot4(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            Logg.Log("[InputManager] QuickSlot4 Pressed", Logg.LoggingMode.Completed);
            OnQuickSlot4Pressed?.Invoke();
        }
    }

    public void OnQuickSlot5(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            Logg.Log("[InputManager] QuickSlot5 Pressed", Logg.LoggingMode.Completed);
            OnQuickSlot5Pressed?.Invoke();
        }
    }

    #endregion
}
