using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
// IPointerHandler 구현 자동화 목적 클래스. 사용 시 반드시 씬에 EventSystem이 있어야 함 
public class UI_EventHandler : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler, IBeginDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    public Func<UniTask> OnClickAsyncHandler = null; // UI 상호작용으로 비동기 함수 호출 목적 델리게이트
    
    public Action OnClickHandler = null;
    public Action OnPressedHandler = null;
    public Action OnPointerDownHandler = null;
    public Action OnPointerUpHandler = null;
    public Action OnPointerEnterHandler = null;
    public Action OnPointerExitHandler = null;

    public Action<BaseEventData> OnDragHandler = null;
    public Action<BaseEventData> OnBeginDragHandler = null;
    public Action<BaseEventData> OnEndDragHandler = null;

    private bool _pressed = false;
    private bool _isClickAsyncRunning = false; // 중복 실행 방지용
    
    private void Update()
    {
        if (_pressed)
            OnPressedHandler?.Invoke();
    }

    private async UniTask OnPointerClickAsync()
    {
        if (_isClickAsyncRunning) return;
        _isClickAsyncRunning = true;

        try
        {
            if (OnClickAsyncHandler != null)
            {
                await OnClickAsyncHandler.Invoke();
            }
        }
        catch (Exception e)
        {
            Util.Log(e);
        }
        finally
        {
            _isClickAsyncRunning = false;
        }

    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClickHandler?.Invoke();

        if (OnClickAsyncHandler != null && !_isClickAsyncRunning)
        {
            _ = OnPointerClickAsync();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        OnPointerDownHandler?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        OnPointerUpHandler?.Invoke();
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        OnPointerEnterHandler?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnPointerExitHandler?.Invoke();
    }
    // Drag 관련 이벤트는 미구현
    public void OnDrag(PointerEventData eventData)
    {
        _pressed = true;
        OnDragHandler?.Invoke(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        OnBeginDragHandler?.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        OnEndDragHandler?.Invoke(eventData);
    }
    
    //todo: 풀링해서 사용시 풀에 반납될 떄 Clear() 호출하도록
    
    public void Clear()
    {
        OnClickAsyncHandler = null;
        
        OnClickHandler = null;
        OnPressedHandler = null;
        OnPointerDownHandler = null;
        OnPointerUpHandler = null;
        OnPointerEnterHandler = null;
        OnPointerExitHandler = null;
        OnDragHandler = null;
        OnBeginDragHandler = null;
        OnEndDragHandler = null;
    }
}
