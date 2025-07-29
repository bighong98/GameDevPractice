using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RPG.UI
{
    public class PopupUI : BaseUI, IPoolObject
    {
        // serializeField 값들은 프리펩에서 관리할 것
        [Header("Popup Option")]
        [SerializeField] [Tooltip("Canvas-UI render mode, 오브젝트 풀링 시 컨테이너 위치 결정")] 
        protected Enums.UIRenderType uiRenderType = Enums.UIRenderType.ScreenOverlay;
        [SerializeField] [Tooltip("사용자가 UI를 임의로(exit 버튼, esc 등) 비활성화 허가")]
        protected bool escapable = true;
        [SerializeField] [Tooltip("팝업 호출과 동시에 게임 일시정지")]
        protected bool pauseRequired = false;
        [SerializeField] [Tooltip("UI 영역 바깥을 누르면 UI가 비활성화")]
        protected bool closeOnOuterBackgroundClick = false;
        [SerializeField] [Tooltip("UI 호출 시 포인터(마우스/터치) 위치를 기준으로 호출")]
        protected bool placePointerPosition = false;
        [SerializeField] [Tooltip("동일 타입 중복 팝업UI 호출시 처리 방식")] 
        protected DuplicatedPopupHandle duplicatedPopupHandle;
        
        [Header("Deprecated/Developing")]
        [SerializeField] [Tooltip("미개발 기능. 사용하지 말것")]
        protected bool blurBackground = false; // UI 영역 바깥을 흐리게 처리할지 (구현x. 사용x)
        [SerializeField] [Tooltip("미개발 기능. 사용하지 말것")]
        protected bool anchorWorldObject = false; // 특정 오브젝트에 붙어있어야할지

        [Header("Popup Animation")] 
        [SerializeField] protected bool playEnterAnimation;
        [SerializeField] protected Enums.UIAnimationType enterAnimation;
        [SerializeField] protected bool playExitAnimation;
        [SerializeField] protected Enums.UIAnimationType exitAnimation;
        
        public bool Escapable { get { return escapable; } }
        public bool PauseRequired { get { return pauseRequired; } }
        public bool CloseOnOuterBackgroundClick { get { return closeOnOuterBackgroundClick; } }
        // public bool BlurBackground { get { return blurBackground; } }
        public bool PlacePointerPosition { get { return placePointerPosition; } }
        public Enums.UIRenderType UiRenderType { get { return uiRenderType; } }
        public bool AnchorWorldObject { get { return anchorWorldObject; } }
        public DuplicatedPopupHandle DuplicatedPopupHandling { get { return duplicatedPopupHandle; } }
        
        public RectTransform Rect { get; private set; }
        private RectTransform parentRect;

        private Vector3 cachedPosition;
        private CancellationTokenSource trackingPositionCTS;

        public enum DuplicatedPopupHandle
        {
            Allow, // 복수의 동일 타입 팝업UI 호출 가능 (Show only)
            Toggle, // 기존에 활성화된 동일 타입 팝업UI가 있으면 Close(only), 없으면 Show(only)
            Replace, // 기존에 활성화된 동일 타입 팝업UI를 닫고, 새 팝업UI를 호출 (Close + Show)
        }
        
        public override bool Init()
        {
            if (base.Init() == false)
                return false;
            
            return true;
        }

        public virtual void ClosePopupUI()
        {
            UIManager.Instance.ClosePopupUI(this);
        }

        public virtual void OnPopupClosed() // 팝업이 닫혔을 때에 실행되어야하는 작업 override해서 구현. UIManager에서 실행됨
        {
            
        }

        public virtual async UniTask OnPopupClosedAsync()
        {
            if (playExitAnimation)
            {
                await PlayUIAnimationAsync(exitAnimation);
            }
        }

        #region Deprecated 미사용/개발중

        private async UniTaskVoid TrackPopupPosition()
        {
            if (cachedPosition == Vector3.zero || trackingPositionCTS == null)
            {
                Util.Log($"{name}.PopupUI.TrackPopupPosition(): cachedPosition or trackingPositionCTS is null");
                return;
            }
            
            while (!trackingPositionCTS.IsCancellationRequested)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, trackingPositionCTS.Token);
                UpdatePopupPosition();
            }
        }
        
        private const float snapDistance = 5f; // 이 이상 차이 나면 순간이동
        private const float lerpSpeed = 10f;    // 부드럽게 따라오는 속도 (Lerp 계수)
        private void UpdatePopupPosition()
        {
            
            Vector3 screenPosition = Util.GetWorldScreenPosition(cachedPosition, false);
            Util.GetMouseScreenPosition(parentRect, screenPosition, out var anchoredPos);
            
            float distance = Vector2.Distance(Rect.anchoredPosition, anchoredPos);
            
            if (distance > snapDistance)
            {
                Rect.anchoredPosition = anchoredPos;
            }
            else
            {
                Rect.anchoredPosition = Vector2.Lerp(
                    Rect.anchoredPosition,
                    anchoredPos,
                    Time.deltaTime * lerpSpeed
                );
            }
        }

        #endregion
        
        
        public GameObject Origin { get; set; }
        public void OnCreateFromPool()
        {
            UIManager.Instance.SetCanvas(this);
            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            Rect = GetComponent<RectTransform>();
            
            if (uiRenderType == Enums.UIRenderType.ScreenOverlay && (placePointerPosition || anchorWorldObject))
            {
                parentRect = transform.parent.GetComponent<RectTransform>();
            }
        }

        public virtual void OnGetFromPool()
        {
            UIManager.Instance.SortCanvas(canvas);

            if (placePointerPosition)
            {
                // if (Util.GetMouseScreenPosition(parentRect, InputManager.Instance.PointerPos, out var pointerPosition))
                if (Util.GetMouseScreenPosition(parentRect, Input.mousePosition, out var pointerPosition))
                {
                    Rect.anchoredPosition = pointerPosition;
                }
            }

            if (playEnterAnimation)
            {
                PlayUIAnimation(enterAnimation);
            }
            else
            {
                canvasGroup.alpha = 1f;
            }
            
            // if (anchorWorldObject)
            // {
            //     if (trackingPositionCTS?.IsCancellationRequested ?? true)
            //         trackingPositionCTS = new CancellationTokenSource();
            //     
            //     cachedPosition = Util.GetMouseWorldPosition();
            //     TrackPopupPosition().Forget();
            // }
        }

        public virtual void OnReleaseFromPool()
        {
            if (!(trackingPositionCTS?.IsCancellationRequested ?? true))
            {
                trackingPositionCTS.Cancel();
            }

            if (playExitAnimation && exitAnimation == Enums.UIAnimationType.PopOut)
            {
                // todo: 팝업 애니메이션에 의한 변형 원상복구
                RollBackScale();
            }
        }

        public void OnDestroyFromPool()
        {
            if (!(trackingPositionCTS?.IsCancellationRequested ?? true))
            {
                Util.ClearUniTaskCTS(trackingPositionCTS);
            }
        }

        public void ReleaseSelf()
        {
            UIManager.Instance.ClosePopupUIImmediately(this);
        }
    }
}

