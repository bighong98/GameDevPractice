using System;
using UnityEngine;
using UnityEngine.EventSystems;
using RPG.UI;

// 확장 메소드 구현 목적의 static 클래스

public static class Extension
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : UnityEngine.Component
    {
        return Util.GetOrAddComponent<T>(go);
    }
    
    public static void BindEvent(this GameObject go, Action action = null, Action<BaseEventData> dragAction = null,
        Enums.UIEvent type = Enums.UIEvent.Click)
    {
        BaseUI.BindEvent(go, action, dragAction, type);
    }

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
}
