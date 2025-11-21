using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Utils;

namespace TH.UI
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
        [SerializeField] protected RectTransform contentArea;
        [SerializeField] [Tooltip("UI 호출 시 포인터(마우스/터치) 위치를 기준으로 호출")]
        protected bool placePointerPosition = false;
        [SerializeField] [Tooltip("동일 타입 중복 팝업UI 호출시 처리 방식")] 
        protected DuplicatedPopupHandle duplicatedPopupHandle;
        protected bool IsPooledObject = false; // 오브젝트 풀링 적용 여부
        [SerializeField] [Tooltip("씬에 배치된 오브젝트에 UI를 고정")]
        protected bool anchorWorldObject = false; // 특정 오브젝트에 붙어있어야할지

        [Header("Popup Animation")] 
        [SerializeField] protected bool playEnterAnimation;
        [SerializeField] protected Enums.UIAnimationType enterAnimation;
        [SerializeField] protected bool playExitAnimation;
        [SerializeField] protected Enums.UIAnimationType exitAnimation;
        
        public bool Escapable { get { return escapable; } }
        public bool PauseRequired { get { return pauseRequired; } }
        public bool CloseOnOuterBackgroundClick { get { return closeOnOuterBackgroundClick; } }
        public RectTransform ContentArea { get { return contentArea != null ? contentArea : Rect; } }
        
        public bool PlacePointerPosition { get { return placePointerPosition; } }
        public Enums.UIRenderType UiRenderType { get { return uiRenderType; } }
        public bool AnchorWorldObject { get { return anchorWorldObject; } }
        public DuplicatedPopupHandle DuplicatedPopupHandling { get { return duplicatedPopupHandle; } }
        
        public RectTransform Rect { get; private set; }
        private RectTransform parentRect;

        private Vector3 cachedPosition;
        private CancellationTokenSource trackingPositionCTS;

        protected CancellationTokenSource PopupCTS; // 해당 팝업의 토큰 소스 (다른 팝업에 종속되어있을 때)
        protected CancellationTokenRegistration OwnerCTSRegistration; // 종속된 팝업과의 연결
        protected bool isTokenChained = false; // 현재 다른 팝업에 종속되어있는지 여부
        protected IRaycastHandler raycastHandler;
        
        public enum DuplicatedPopupHandle
        {
            Allow, // 복수의 동일 타입 팝업UI 호출 가능 (Show only)
            Toggle, // 기존에 활성화된 동일 타입 팝업UI가 있으면 Close(only), 없으면 Show(only)
            Replace, // 기존에 활성화된 동일 타입 팝업UI를 닫고, 새 팝업UI를 호출 (Close + Show)
        }

        protected override void Awake()
        {
            base.Awake();
            raycastHandler = ServiceLocator.Get<IRaycastHandler>(); // todo: UIManager에서 주입 고려
        }
        
        public override bool Init()
        {
            if (base.Init() == false)
                return false;
            
            return true;
        }

        public virtual T ShowPopupUI<T>() where T : PopupUI
        {
            if (IsPooledObject && UIManager.Instance.ShowPopupUI<T>() is { } popup)
            {
                return popup;
            }
            
            if (gameObject.activeSelf) return null;

            gameObject.SetActive(true);
            OnGetFromPool();
            return (T)this;
        }

        public virtual void ClosePopupUI()
        {
            if (Util.IsQuitting) return;
            // 오브젝트 풀에서 관리하고, 풀 반환에 성공했다면 개별 비활성화 취소
            if (IsPooledObject && UIManager.Instance.ClosePopupUI(this)) return; 
            if (!gameObject.activeSelf) return; // 이미 비활성화되었다면 취소
            
            if (playExitAnimation)
            {
                OnPopupClosedAsync().ContinueWith(() =>
                {
                    OnReleaseFromPool();
                    gameObject.SetActive(false);
                });
            }
            else
            {
                OnPopupClosed();
                OnReleaseFromPool();
                gameObject.SetActive(false);
            }
        }

        // 팝업이 닫혔을 때에 실행되어야하는 작업 override해서 구현. UIManager에서 실행됨
        public virtual void OnPopupClosed() 
        {
            OwnerCTSRegistration.Dispose();
            CancelPopupCTS();
        }

        public virtual async UniTask OnPopupClosedAsync()
        {
            if (playExitAnimation)
            {
                await PlayUIAnimationAsync(exitAnimation);
            }
            OnPopupClosed();
        }

        #region Chained Popup CTS (CancellationTokenSource)

        public bool IsCTSChainAlive => !(PopupCTS?.Token.IsCancellationRequested ?? true);
        
        // 외부 객체(다른 팝업UI 등)에 현재 PopupUI 객체를 종속
        // ownerToken의 CTS가 .Cancel()이 호출되면 현재 팝업의 CTS도 연쇄적으로 .Cancel이 호출, 팝업이 닫힘
        public void ChainPopupCTS(CancellationToken ownerToken)  
        {
            OwnerCTSRegistration.Dispose(); // 기존 토큰 연결 해제
            if (!ownerToken.CanBeCanceled || ownerToken.IsCancellationRequested)
            {
                CancelPopupCTS();
                Logg.LogError($"[{nameof(GetType)}.{nameof(PopupUI)}.{nameof(ChainPopupCTS)}] InValid ownerToken");
                return;
            }
            
            CancelAndRenewPopupCTS();
            OwnerCTSRegistration = ownerToken.Register(CancelAndClose);
        }
        
        protected void CancelPopupCTS()
        {
            if (PopupCTS == null) return;
            
            if (!PopupCTS.IsCancellationRequested)
                PopupCTS.Cancel();
            PopupCTS.Dispose();
            PopupCTS = null;
        }

        protected void CancelAndRenewPopupCTS()
        {
            CancelPopupCTS();
            PopupCTS = new CancellationTokenSource();
        }

        protected void CancelAndClose()
        {
            CancelPopupCTS();
            ClosePopupUI();
        }

        #endregion
        
        #region AnchorWorldObject

        private async UniTaskVoid TrackPopupPosition()
        {
            if (cachedPosition == Vector3.zero || trackingPositionCTS == null)
            {
                Logg.Log($"{name}.PopupUI.TrackPopupPosition(): cachedPosition or trackingPositionCTS is null");
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
            Vector3 screenPosition = raycastHandler.GetWorldScreenPosition(cachedPosition, false);
            raycastHandler.GetMouseScreenPosition(parentRect, screenPosition, out var anchoredPos);
            
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

        #region IPoolObject (PoolManager)

        public GameObject Origin { get; set; }

        public void OnCreateFromPool()
        {
            IsPooledObject = true;
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
            UIManager.Instance.SortCanvas(canvas, UICanvas.Popup);

            if (placePointerPosition)
            {
                if (raycastHandler.GetMouseScreenPosition(parentRect, Input.mousePosition, out var pointerPosition))
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
            
            if (anchorWorldObject)
            {
                if (trackingPositionCTS?.IsCancellationRequested ?? true)
                    trackingPositionCTS = new CancellationTokenSource();
                
                cachedPosition = raycastHandler.GetMouseWorldPosition();
                TrackPopupPosition().Forget();
            }
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
                Util.ClearCTS(trackingPositionCTS);
            }
        }

        public void ReleaseSelf()
        {
            UIManager.Instance.ClosePopupUIImmediately(this);
        }

        #endregion
        
    }
}

