using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using TH.Utils;

namespace RPG.UI
{
    public class BaseUI : MonoBehaviour
    {
        private readonly Dictionary<Type, UnityEngine.Object[]> _objects = new Dictionary<Type, UnityEngine.Object[]>();
        protected bool _init = false;
        protected Vector3 originScale;
        
        protected Canvas canvas;
        protected CanvasGroup canvasGroup;

        protected virtual void Awake()
        {
            
        }

        public virtual bool Init()
        {
            if (_init) // if already initialized, return false
                return false;

            originScale = transform.localScale;
            _init = true;
            return true;
        }
        
        #region Bind

        protected void Bind<T>(Type type) where T : UnityEngine.Object
        {
            string[] names = Enum.GetNames(type);
            UnityEngine.Object[] objects = new UnityEngine.Object[names.Length];
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
                case (int)Enums.UIEvent.PointerUp:
                    eventHandler.OnPointerUpHandler -= action;
                    eventHandler.OnPointerUpHandler += action;
                    break;
                case (int)Enums.UIEvent.PointerDown:
                    eventHandler.OnPointerDownHandler -= action;
                    eventHandler.OnPointerDownHandler += action;
                    break;
                case (int)Enums.UIEvent.PointerEnter:
                    eventHandler.OnPointerEnterHandler -= action;
                    eventHandler.OnPointerEnterHandler += action;
                    break;
                case (int)Enums.UIEvent.PointerExit:
                    eventHandler.OnPointerExitHandler -= action;
                    eventHandler.OnPointerExitHandler += action;
                    break;
                //todo: UI 이벤트 추가
                default:
                    Debug.Log("UIEvent type is wrong");
                    break;
            }
        }
        // UI 상호작용을 통해 비동기 함수를 실행시켜야할 때에 사용
        // 현재 클릭 이벤트에만 등록 가능함
        // cancelOnDestroy = true이면 UI 파괴시 비동기 작업이 취소됨
        public static void BindAsyncEvent(GameObject go, Func<UniTask> asyncAction = null, bool cancelOnDestroy = false)
        {
            if (go == null || asyncAction == null)
            {
                Logg.Log($"BindAsyncEvent(): go or asyncAction is null: {go.name}");
                return;
            }
            UI_EventHandler eventHandler = go.GetOrAddComponent<UI_EventHandler>();

            // eventHandler.OnClickAsyncHandler -= asyncAction;
            // eventHandler.OnClickAsyncHandler += asyncAction;
            // 비동기 작업은 한 종류만 등록 가능함 (멀티 캐스트 불가능)
            eventHandler.OnClickAsyncHandler = async () =>
            {
                if (cancelOnDestroy)
                {
                    await asyncAction().AttachExternalCancellation(eventHandler.GetCancellationTokenOnDestroy());
                }
                else
                {
                    await asyncAction();
                }
            };
        }
        
        #endregion

        #region Get

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

        #endregion

        #region UI Animation (DOTween based)

        protected Sequence PlayUIAnimation(
            Enums.UIAnimationType type, 
            Action onStart = null, Action onComplete = null,
            bool ignoreTimeScale = true)
        {
            Sequence sequence = DOTween.Sequence();
            
            if (onStart != null)
            {
                sequence.AppendCallback(() => onStart());
            }

            sequence.Append(GetUIAnimationByType(type));

            if (onComplete != null)
            {
                sequence.AppendCallback(() => onComplete());
            }

            return sequence.SetUpdate(ignoreTimeScale);
        }

        protected async UniTask PlayUIAnimationAsync(
            Enums.UIAnimationType type,
            Action onStart = null, Action onComplete = null)
        {
            var sequence = PlayUIAnimation(type, onStart, onComplete);
            await sequence.AsyncWaitForCompletion();
        }

        private Sequence GetUIAnimationByType(Enums.UIAnimationType type)
        {
            return type switch
            {
                Enums.UIAnimationType.PopIn => PopInUIAnimation(),
                Enums.UIAnimationType.PopOut => PopOutAnimation(),
                _ => DOTween.Sequence() // default: 빈 시퀀스 반환
            };
        }

        private Sequence PopInUIAnimation()
        {
            return DOTween.Sequence()
                .AppendCallback(() =>
                {
                    transform.localScale = Vector3.zero;
                    canvasGroup.alpha = 1f;
                })
                .Append(transform.DOScale(1.0f, 0.1f));
        }

        private Sequence PopOutAnimation()
        {
            return DOTween.Sequence()
                .Append(transform.DOScale(0.1f, 0.1f));
        }

        protected void RollBackScale()
        {
            transform.localScale = originScale;
        }

        #endregion

        protected virtual void Clear() { } // 정리 작업. PopupUI의 경우 UIManager에 의해 
    }
}

