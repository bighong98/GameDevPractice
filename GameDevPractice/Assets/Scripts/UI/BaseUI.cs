using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using TH.Utils;

namespace TH.UI
{
    public class BaseUI : MonoBehaviour
    {
        private static readonly Dictionary<Type, string[]> enumNamesLookup = new();
        private readonly Dictionary<Type, UnityEngine.Object[]> _objects = new Dictionary<Type, UnityEngine.Object[]>();
        protected bool _init = false;
        protected Vector3 originScale;
        
        protected Canvas canvas;
        protected CanvasGroup canvasGroup;
        protected UI_EventHandler eventHandler;

        protected virtual void Awake()
        {
            // 상속 클래스에서 필요 시 다음 작업을 수행
            // Bind 계열 메서드 (BindObject, BindImage, BindEvent, etc)
            // 기타 UI 이외 계열 컴포넌트 바인딩
        }

        // UI 초기화 메서드 (최초 1회만 실행)
        // 이미 초기화되었으면 false 반환
        public virtual bool Init()
        {
            if (_init) // 이미 초기화되었으면 false 반환
                return false;

            // 원본 스케일 저장 (애니메이션 후 복구용)
            originScale = transform.localScale;
            _init = true;
            return true;
        }
        
        #region Bind

        // UI 컴포넌트 바인딩 시스템
        // Enum 타입을 기반으로 UI 컴포넌트들을 딕셔너리에 바인딩
        // 예: enum Images { Icon, Background } -> GameObject.Find("Icon"), GameObject.Find("Background")
        protected void Bind<T>(Type type) where T : UnityEngine.Object
        {
            if (!enumNamesLookup.TryGetValue(type, out var names))
            {
                names = Enum.GetNames(type);
                enumNamesLookup[type] = names;
            }
            
            UnityEngine.Object[] objects = new UnityEngine.Object[names.Length];
            _objects.Add(typeof(T), objects);
            
            // Enum의 각 이름으로 하위 오브젝트 검색
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
        
        // UI 이벤트 바인딩 (동기 방식)
        // UI_EventHandler 컴포넌트를 통한 이벤트 등록
        // Click, PointerUp, PointerDown, PointerEnter, PointerExit 지원
        public void BindEvent(GameObject go, Action action = null, Action<BaseEventData> dragAction = null,
            Enums.UIEvent type = Enums.UIEvent.Click)
        {
            // UI_EventHandler 컴포넌트 추가 또는 가져오기
            eventHandler = go.GetOrAddComponent<UI_EventHandler>();
            
            // 이벤트 타입에 따라 핸들러 등록 (중복 방지를 위해 먼저 해제 후 등록)
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
                default:
                    Debug.Log("UIEvent type is wrong");
                    break;
            }
        }
        // UI 상호작용을 통해 비동기 함수를 실행시켜야할 때에 사용
        // 현재 클릭 이벤트에만 등록 가능함
        // GameObject에 비동기 함수를 클릭 이벤트로 바인딩하는 정적 메서드
        // UI 상호작용으로 비동기 작업을 실행할 때 사용
        // cancelOnDestroy = true면 UI 파괴 시 비동기 작업이 취소됨
        public void BindAsyncEvent(GameObject go, Func<UniTask> asyncAction = null, bool cancelOnDestroy = false)
        {
            if (go == null || asyncAction == null)
            {
                Logg.Log($"BindAsyncEvent(): go or asyncAction is null: {(go.IsNotNull() ? go.name : string.Empty)}");
                return;
            }
            
            if (eventHandler == null)
                eventHandler = go.GetOrAddComponent<UI_EventHandler>();

            // 비동기 작업은 한 종류만 등록 가능함 (멀티 캐스트 불가능)
            eventHandler.OnClickAsyncHandler = async () =>
            {
                // cancelOnDestroy: true -> UI 파괴 시 자동 취소되도록 CancellationToken 연결
                if (cancelOnDestroy) await asyncAction().AttachExternalCancellation(eventHandler.GetCancellationTokenOnDestroy());
                else await asyncAction();
            };
        }
        
        #endregion

        #region Get

        // 바인딩된 컴포넌트를 인덱스로 가져오기
        // 사용 예시: GetButton((int)Buttons.ConfirmButton)
        protected T Get<T>(int idx) where T : UnityEngine.Object
        {
            if (!_objects.TryGetValue(typeof(T), out var objects))
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

        // DOTween 기반 UI 애니메이션 재생 (동기)
        // PopIn/PopOut 등의 애니메이션 타입 지원
        // ignoreTimeScale: true -> 게임 일시정지 중에도 애니메이션 작동
        protected Sequence PlayUIAnimation(
            Enums.UIAnimationType type, 
            Action onStart = null, Action onComplete = null,
            bool ignoreTimeScale = true)
        {
            Sequence sequence = DOTween.Sequence();
            
            // 시작 콜백 추가
            if (onStart != null)
            {
                sequence.AppendCallback(() => onStart());
            }

            // 타입에 맞는 애니메이션 추가
            sequence.Append(GetUIAnimationByType(type));

            // 완료 콜백 추가
            if (onComplete != null)
            {
                sequence.AppendCallback(() => onComplete());
            }

            return sequence.SetUpdate(ignoreTimeScale);
        }

        // DOTween 기반 UI 애니메이션 재생 (비동기, 애니메이션 완료까지 대기)
        protected async UniTask PlayUIAnimationAsync(
            Enums.UIAnimationType type,
            Action onStart = null, Action onComplete = null)
        {
            var sequence = PlayUIAnimation(type, onStart, onComplete);
            await sequence.AsyncWaitForCompletion();
        }

        // 애니메이션 타입에 맞는 DOTween Sequence 반환
        private Sequence GetUIAnimationByType(Enums.UIAnimationType type)
        {
            return type switch
            {
                Enums.UIAnimationType.PopIn => PopInUIAnimation(),   // 팝업 입장 애니메이션
                Enums.UIAnimationType.PopOut => PopOutAnimation(),   // 팝업 퇴장 애니메이션
                _ => DOTween.Sequence()                              // 기본값: 빈 시퀀스 반환
            };
        }

        // 팝업 입장 애니메이션 (0 -> 1 확대)
        private Sequence PopInUIAnimation()
        {
            return DOTween.Sequence()
                .AppendCallback(() =>
                {
                    transform.localScale = Vector3.zero;  // 초기 스케일 0으로 설정
                    canvasGroup.alpha = 1f;                // 불투명도 1로 설정
                })
                .Append(transform.DOScale(1.0f, 0.1f));   // 0.1초에 걸쳐 스케일 1로 확대
        }

        // 팝업 퇴장 애니메이션 (1 -> 0.1 축소)
        private Sequence PopOutAnimation()
        {
            return DOTween.Sequence()
                .Append(transform.DOScale(0.1f, 0.1f));  // 0.1초에 걸쳐 스케일 0.1로 축소
        }

        // 애니메이션으로 변경된 스케일을 원래대로 복구
        protected void RollBackScale()
        {
            transform.localScale = originScale;
        }

        #endregion

        protected virtual void Clear() { } // 정리 작업. PopupUI의 경우 UIManager에 의해 호출
    }
}

