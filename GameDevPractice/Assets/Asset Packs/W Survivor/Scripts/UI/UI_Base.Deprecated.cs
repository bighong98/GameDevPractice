using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;
using DG.Tweening;
using Sequence = DG.Tweening.Sequence;


// base Class of all UI
public abstract class UI_Base : MonoBehaviour
{
    protected Dictionary<Type, UnityEngine.Object[]> _objects = new Dictionary<Type, Object[]>();
    protected bool _init = false;
    public int poolIndex;

    public virtual bool Init()
    {
        // if already initialized, return false
        if (_init)
            return false;

        _init = true;
        return true;
    }
    
    // add all components<T> from this.gameObject
    // object name and Enum Type name must be same
    // ex:  if type = Texts.KillCountsText => object name must be KillCountsText
    public void Bind<T>(Type type) where T : UnityEngine.Object
    {
        string[] names = Enum.GetNames(type);
        UnityEngine.Object[] objects = new Object[names.Length];
        _objects.Add(typeof(T), objects);
        
        for (int i = 0; i < names.Length; i++)
        {
            if (typeof(T) == typeof(GameObject))
                objects[i] = Util.FindChild(gameObject, names[i], true);
            else
                objects[i] = Util.FindChild<T>(gameObject, names[i], true);
        }
        
    }
    
    // shortcuts of Bind<T>()
    protected void BindObject(Type type) => Bind<GameObject>(type);
    protected void BindImage(Type type) => Bind<Image>(type);
    protected void BindTMPText(Type type) => Bind<TMP_Text>(type);
    protected void BindText(Type type) => Bind<Text>(type);
    protected void BindButton(Type type) => Bind<Button>(type);
    protected void BindToggle(Type type) => Bind<Toggle>(type);

    // Get i-th object(or component) from _objects dictionary
    // use input as (int)Enum
    protected T Get<T>(int idx) where T : UnityEngine.Object
    {
        UnityEngine.Object[] objects = null;
        if (_objects.TryGetValue(typeof(T), out objects) == false)
            return null;

        return objects[idx] as T;
    }
    // shortcuts of Get<T>()
    protected GameObject GetObject(int idx) => Get<GameObject>(idx);
    protected TMP_Text GetTMPText(int idx) => Get<TMP_Text>(idx);
    protected Text GetText(int idx) => Get<Text>(idx);
    protected Button GetButton(int idx) => Get<Button>(idx);
    protected Image GetImage(int idx) => Get<Image>(idx);
    protected Toggle GetToggle(int idx) => Get<Toggle>(idx);
    
    public static void BindEvent(GameObject go, Action action = null, Action<BaseEventData> dragAction = null,
        Enums.UIEvent type = Enums.UIEvent.Click)
    {
        UI_EventHandler eventHandler = go.GetComponent<UI_EventHandler>();
        if (eventHandler == null)
            eventHandler = go.AddComponent<UI_EventHandler>();

        switch ((int)type)
        {
            case (int)Enums.UIEvent.Click:
                eventHandler.OnClickHandler -= action;
                eventHandler.OnClickHandler += action;
                break;
            //todo: add more events
            default:
                Debug.Log("UIEvent type is wrong");
                break;
        }
    }

    // pseudo code (not developed)
    // play popup open animation
    public void PopUpOpenAnimation(GameObject contentObject)
    {
        contentObject.transform.localScale = new Vector3(0.8f, 0.8f, 1);
        contentObject.transform.DOScale(1f, 0.1f).SetEase(Ease.InOutBack).SetUpdate(true);
    }
}
