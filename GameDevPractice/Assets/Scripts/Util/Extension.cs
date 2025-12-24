using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using TH.UI;
using TH.Utils;

// 확장 메소드 구현 목적의 static 클래스

public static class Extension
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : UnityEngine.Component
    {
        return Util.GetOrAddComponent<T>(go);
    }
    
    // public static void BindEvent(this GameObject go, Action action = null, Action<BaseEventData> dragAction = null,
    //     Enums.UIEvent type = Enums.UIEvent.Click)
    // {
    //     BaseUI.BindEvent(go, action, dragAction, type);
    // }

    public static Vector2 GetVectorTwo(this Vector3 vector)
    {
        return Util.GetVectorTwo(vector);
    }

    public static void SafeInvoke(this Action<bool> action, bool arg)
    {
        Util.SafeInvoke(action, arg);
    }

    public static T ShowPopupUI<T>(this T popup) where T : PopupUI
    {
        return popup.ShowPopupUI<T>();
    }

    public static bool IsEqualFloat(this float a, float b)
    {
        return Util.IsEqualFloat(a, b);
    }

    public static T ClearDelegate<T>(this T del) where T : Delegate
    {
        if (del == null) return null;
        foreach (var d in del.GetInvocationList())
        {
            del = (T)Delegate.Remove(del, d);
        }

        return del;
    }
    
    private const int DefaultMaxConcurrency = 10;
    public static UniTask InvokeAllThrottledAsync(
        this Func<CancellationToken, UniTask> multicast,
        CancellationToken token,
        int maxConcurrency = DefaultMaxConcurrency,
        TimeSpan? perCallbackTimeout = null,
        bool cancelAllOnFirstFailure = false,
        Action<Exception, Delegate> onException = null,
        bool throwAggregated = false)
    {
        return Util.InvokeAllCallbackAsync(
            multicast, token, maxConcurrency, perCallbackTimeout, cancelAllOnFirstFailure, onException, throwAggregated);
    }

    // fake null 이슈에 대응하기 위한 헬퍼 확장 메서드
    // 특정 인터페이스 구현 인스턴스가 MonoBehaviour 상속 받았을 가능성이 있는 경우 '==', 'is' 대신 사용 권장 
    // 반드시 호출 전 await UniTask.SwitchToMainThread(); 등으로 메인 쓰레드 상태임을 보장할 것
    #nullable enable
    public static bool IsNotNull<T>([NotNullWhen(true)] this T? obj) where T : class
    {
        if (obj is null) return false; // 가리키는 참조가 없는 경우 
        if (obj is UnityEngine.Object unityObject) return unityObject != null;
        return true;
    }
}
