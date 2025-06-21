using System;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using Item;

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
    public event Action<Vector2> OnPointerMoved;
    // player
    public event Action<Vector2> OnMoved;
    public event Action<Vector2> OnSelected;
    // UI
    public event Action<Vector2> OnUIPointerMoved;
    public event Action<Vector2> OnSingleClicked;
    public event Action<Vector2> OnDoubleClicked;
    public event Action<Vector2> OnHolded;
    // UI.Drag
    public event Action<Vector2> OnDragStarted;
    public event Action<Vector2> OnDragging;
    public event Action<Vector2> OnDragEnded;

    #endregion 
    
    
    
    // 캐싱 좌표
    private Vector2 currentPointerPos = Vector2.zero; // update by OnPoint()
    private Vector2 dragStartPosition = Vector2.zero; // update by OnDrag()
    
    public Vector2 PointerPos
    {
        get { return currentPointerPos; }
    }

    private bool isDragging = false;
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
        
        userInput.Player.Enable(); // default: PlayerActions 활성화
    }

    protected override void OnSceneLoaded(bool dummy)
    {
        
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
        {
            UIManager.Instance.ShowPopupUI<InventoryUI>("UI_Inventory.prefab", allowDuplicatePopup: false);
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        OnMoved?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnSelect(InputAction.CallbackContext context)
    {
        OnSelected?.Invoke(context.ReadValue<Vector2>());
    }

    public void OnEscape(InputAction.CallbackContext context)
    {
        OnEscaped?.Invoke();
    }

    public void OnPoint(InputAction.CallbackContext context)
    {
        currentPointerPos = context.ReadValue<Vector2>();
        OnPointerMoved?.Invoke(currentPointerPos);
    }

    public void OnClick(InputAction.CallbackContext context)
    {
        if (context.interaction is UnityEngine.InputSystem.Interactions.TapInteraction)
        {
            OnSingleClicked?.Invoke(context.ReadValue<Vector2>());
        }
    }

    public void OnDoubleClick(InputAction.CallbackContext context)
    {
        if (context.interaction is UnityEngine.InputSystem.Interactions.MultiTapInteraction multiTapInteraction)
        {
            OnDoubleClicked?.Invoke(context.ReadValue<Vector2>());
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
        OnHolded?.Invoke(context.ReadValue<Vector2>());
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
}
