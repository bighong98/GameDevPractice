using System;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using RPG.UI;
using RPG.SceneManagement;

public class InputManager : Singleton<InputManager>, UserInput.IPlayerActions, UserInput.IGlobalActions, UserInput.IUIActions
{
    //todo: Drag 및 Hold 간섭 방지 로직 추가
    // 외부 접근용 프로퍼티
    private UserInput userInput; // input action asset 자동생성 클래스
    public UserInput UserInput => userInput;
    public UserInput.GlobalActions GlobalActions => UserInput.Global;
    public UserInput.PlayerActions PlayerActions => UserInput.Player;
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
    public event Action<Vector2> OnDoubleClicked;
    // public event Action<Vector2> OnHolded;
    // UI.Drag
    public event Action<Vector2> OnDragStarted;
    public event Action<Vector2> OnDragEnded;

    #endregion 
    
    
    
    // 캐싱 좌표
    private Vector2 currentPointerPos = Vector2.zero; // update by OnPoint()
    private Vector2 dragStartPosition = Vector2.zero; // update by OnDrag()
    
    public Vector2 PointerPos { get { return currentPointerPos; } }

    private bool isDragging = false;
    private bool wasDraggingOneFrameAgo = false;
    private CancellationTokenSource clickCTS;
    
    protected override void Awake()
    {
        base.Awake();
        if (IsInvalidInstance()) return;
        // todo: 초기화 작업/마무리 작업 OnSceneLoaded(), GameSceneManger.Instance.RegisterCleanupTask()로 이전
        userInput = new UserInput();
        
        userInput.Player.SetCallbacks(this);
        userInput.Global.SetCallbacks(this);
        userInput.UI.SetCallbacks(this);
        
        // default: GlobalActions, PlayerActions 활성화
        userInput.Global.Enable();
        userInput.Player.Enable(); 
    }

    protected override void OnSceneLoaded(bool isDone)
    {
        if (!isDone) return;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            UIManager.Instance.ShowPopupUI<InventoryUI>("UI_Inventory.prefab");
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        OnMoved?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnSelect(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
        {
            OnSelected?.Invoke(currentPointerPos);
        }
    }

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

    public void OnClick(InputAction.CallbackContext context)
    {
        if (isDragging || wasDraggingOneFrameAgo) return;

        if (context is { interaction: UnityEngine.InputSystem.Interactions.MultiTapInteraction, phase: InputActionPhase.Performed })
        {
            // Util.Log("Double Click Occured");
            OnDoubleClicked?.Invoke(currentPointerPos);
        }
        else if (context is { interaction: UnityEngine.InputSystem.Interactions.TapInteraction, phase: InputActionPhase.Performed })
        {
            // Util.Log("Single Click Occured");
            OnSingleClicked?.Invoke(currentPointerPos);
        }
    }
    
    public void OnDrag(InputAction.CallbackContext context)
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

    public void OnHold(InputAction.CallbackContext context)
    {
        // OnHolded?.Invoke(context.ReadValue<Vector2>()); // 현재 미구현 추후 구현 예정
    }

    public void OnPointUI(InputAction.CallbackContext context)
    {
        OnUIPointerMoved?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnRelease(InputAction.CallbackContext context)
    {
        if (context.phase != InputActionPhase.Canceled) return;

        if (isDragging)
        {
            isDragging = false;
            OnDragEnded?.Invoke(currentPointerPos);
            
            wasDraggingOneFrameAgo = true;
            UniTask.Void(async () =>
            {
                await UniTask.NextFrame();
                wasDraggingOneFrameAgo = false;
            });
        }
    }

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

    // 임시기능 (별도의 매니저로 기능 이관 예정)
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
}
