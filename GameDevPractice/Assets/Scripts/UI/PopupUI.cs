using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Utils;

namespace TH.UI
{
    public abstract class PopupUI : BaseUI, IPoolObject
    {
        #region Popup Options
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

        #endregion

        #region Popup Animation Options

        [Header("Popup Animation")] 
        [SerializeField] protected bool playEnterAnimation;
        [SerializeField] protected Enums.UIAnimationType enterAnimation;
        [SerializeField] protected bool playExitAnimation;
        [SerializeField] protected Enums.UIAnimationType exitAnimation;

        #endregion
        
        #region Popup Option Properties

        public bool Escapable { get { return escapable; } }
        public bool PauseRequired { get { return pauseRequired; } }
        public bool CloseOnOuterBackgroundClick { get { return closeOnOuterBackgroundClick; } }
        public RectTransform ContentArea { get { return contentArea != null ? contentArea : Rect; } }
        
        public bool PlacePointerPosition { get { return placePointerPosition; } }
        public Enums.UIRenderType UiRenderType { get { return uiRenderType; } }
        public bool AnchorWorldObject { get { return anchorWorldObject; } }
        public DuplicatedPopupHandle DuplicatedPopupHandling { get { return duplicatedPopupHandle; } }
        
        #endregion

        
        public RectTransform Rect { get; private set; }
        private RectTransform parentRect;

        private Vector3 cachedPosition;
        private CancellationTokenSource trackingPositionCTS;

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

        // 팝업 UI 표시 메서드
        // IsPooledObject가 true면 UIManager를 통해 풀에서 가져오고, false면 직접 활성화
        public virtual T ShowPopupUI<T>() where T : PopupUI
        {
            // 오브젝트 풀링이 활성화된 경우 UIManager를 통해 팝업 표시
            if (IsPooledObject && UIManager.Instance.ShowPopupUI<T>() is { } popup)
            {
                return popup;
            }
            
            // 이미 활성화되어 있으면 null 반환
            if (gameObject.activeSelf) return null;

            // 직접 활성화 (풀링 미사용 시)
            gameObject.SetActive(true);
            OnGetFromPool();
            return (T)this;
        }

        // 팝업 UI 닫기
        // 오브젝트 풀링 적용 시 UIManager로 반환
        // 애니메이션 종료 후 비활성화
        public virtual void ClosePopupUI()
        {
            if (Util.IsQuitting) return;
            
            // 오브젝트 풀에서 관리하고, 풀 반환에 성공했다면 개별 비활성화 취소
            if (IsPooledObject && UIManager.Instance.ClosePopupUI(this)) return; 
            
            // 이미 비활성화되었다면 취소
            if (!gameObject.activeSelf) return; 
            
            // 종료 애니메이션 재생 여부에 따라 처리
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
            OwnerCTSRegistration.Dispose();// 종속된 팝업과의 연결 해제
            CancelPopupCTS(); // 현재 팝업의 CancellationTokenSource 취소
        }

        // 비동기로 팝업 닫기 (애니메이션 대기 포함)
        public virtual async UniTask OnPopupClosedAsync()
        {
            // 종료 애니메이션이 설정되어 있으면 재생
            if (playExitAnimation)
            {
                await PlayUIAnimationAsync(exitAnimation);
            }
            // 정리 작업 수행
            OnPopupClosed();
        }

        #region Chained Popup CTS (CancellationTokenSource)

        protected CancellationTokenSource PopupCTS; // 해당 팝업의 토큰 소스 (다른 팝업에 종속되어있을 때)
        protected CancellationTokenRegistration OwnerCTSRegistration; // 종속된 팝업과의 연결
        protected bool isTokenChained = false; // 현재 다른 팝업에 종속되어있는지 여부
        public bool IsCTSChainAlive => !(PopupCTS?.Token.IsCancellationRequested ?? true);
        

        // 외부 객체(다른 팝업)의 CTS에 현재 팝업 종속
        // owner가 취소되면 현재 팝업도 자동으로 닫힘
        public void ChainPopupCTS(CancellationToken ownerToken)  
        {
            // 기존 토큰 연결 해제
            OwnerCTSRegistration.Dispose();
            
            // 유효하지 않은 토큰이면 현재 팝업 취소 후 종료
            if (!ownerToken.CanBeCanceled || ownerToken.IsCancellationRequested)
            {
                CancelPopupCTS();
                Logg.LogError($"[{nameof(GetType)}.{nameof(PopupUI)}.{nameof(ChainPopupCTS)}] InValid ownerToken");
                return;
            }
            
            CancelAndRenewPopupCTS(); // 새로운 CTS 생성
            OwnerCTSRegistration = ownerToken.Register(CancelAndClose); // owner 토큰이 취소되면 현재 팝업도 취소되도록 등록
        }
        
        // CancellationTokenSource 취소 및 정리
        protected void CancelPopupCTS()
        {
            if (PopupCTS == null) return;
            
            // 아직 취소되지 않았으면 취소
            if (!PopupCTS.IsCancellationRequested)
                PopupCTS.Cancel();
            // 리소스 해제
            PopupCTS.Dispose();
            PopupCTS = null;
        }

        // 기존 CTS를 취소하고 새로운 CTS 생성
        protected void CancelAndRenewPopupCTS()
        {
            CancelPopupCTS();
            PopupCTS = new CancellationTokenSource();
        }

        // CTS 취소 후 팝업 닫기 (체인 토큰에서 호출됨)
        protected void CancelAndClose()
        {
            CancelPopupCTS();
            ClosePopupUI();
        }

        #endregion
        
        #region AnchorWorldObject

        // 월드 오브젝트를 추적하여 팝업 위치를 업데이트하는 비동기 루프
        // anchorWorldObject가 true일 때 활성화됨
        private async UniTaskVoid TrackPopupPosition()
        {
            if (cachedPosition == Vector3.zero || trackingPositionCTS == null)
            {
                Logg.Log($"{name}.PopupUI.TrackPopupPosition(): cachedPosition or trackingPositionCTS is null");
                return;
            }
            
            // 취소 토큰이 요청될 때까지 매 프레임 위치 업데이트
            while (!trackingPositionCTS.IsCancellationRequested)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate, trackingPositionCTS.Token);
                UpdatePopupPosition();
            }
        }
        
        private const float snapDistance = 5f; // 이 이상 차이 나면 순간이동
        private const float lerpSpeed = 10f;    // 부드럽게 따라오는 속도 (Lerp 계수)
        // 월드 오브젝트의 스크린 위치를 계산하여 팝업 위치 업데이트
        // 거리가 멀면 순간이동, 가까우면 부드럽게 이동 (Lerp)
        private void UpdatePopupPosition()
        {
            // 월드 좌표를 스크린 좌표로 변환
            Vector3 screenPosition = raycastHandler.GetWorldScreenPosition(cachedPosition, false);
            // 스크린 좌표를 UI 앵커 좌표로 변환
            raycastHandler.GetMouseScreenPosition(parentRect, screenPosition, out var anchoredPos);
            
            float distance = Vector2.Distance(Rect.anchoredPosition, anchoredPos);
            
            // 일정 거리 이상 차이나면 즉시 이동 (순간이동)
            if (distance > snapDistance)
            {
                Rect.anchoredPosition = anchoredPos;
            }
            else // 가까우면 부드럽게 이동 (Lerp)
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

        // IPoolObject.OnCreateFromPool() 구현
        // 오브젝트 풀에서 처음 생성될 때 호출 (초기화)
        // Canvas 설정 및 컴포넌트 캐싱
        public virtual void OnCreateFromPool()
        {
            IsPooledObject = true;
            // UIManager를 통해 Canvas 설정 (render mode, sorting 등)
            
            // 컴포넌트 캐싱
            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            Rect = GetComponent<RectTransform>();
            
            // 포인터 위치 배치 또는 월드 오브젝트 앵커 사용 시 부모 RectTransform 캐싱
            if (uiRenderType == Enums.UIRenderType.ScreenOverlay && (placePointerPosition || anchorWorldObject))
            {
                parentRect = transform.parent.GetComponent<RectTransform>();
            }
        }

        // IPoolObject.OnGetFromPool() 구현
        // 오브젝트 풀에서 꺼내질 때마다 호출 (재사용 시)
        // Canvas 정렬, 위치 설정, 애니메이션 재생, 월드 오브젝트 추적 시작
        public virtual void OnGetFromPool()
        {
            Logg.Log($"[PopupUI.OnGetFromPool] START - Current scale: {Rect.localScale}");
            
            // Canvas sorting order를 최상단으로 설정
            UIManager.Instance.SortCanvas(canvas, UICanvas.Popup);
            
            Logg.Log($"[PopupUI.OnGetFromPool] After SortCanvas - Current scale: {Rect.localScale}");

            // 포인터 위치에 팝업 배치
            if (placePointerPosition)
            {
                if (raycastHandler.GetMouseScreenPosition(parentRect, Input.mousePosition, out var pointerPosition))
                {
                    Rect.anchoredPosition = pointerPosition;
                }
            }

            // 입장 애니메이션 재생
            if (playEnterAnimation)
            {
                PlayUIAnimation(enterAnimation);
            }
            else
            {
                canvasGroup.alpha = 1f; // 애니메이션 없으면 즉시 표시
            }
            
            Logg.Log($"[PopupUI.OnGetFromPool] After animation - Current scale: {Rect.localScale}");
            
            // 월드 오브젝트 추적 시작
            if (anchorWorldObject)
            {
                // CancellationTokenSource 초기화 또는 생성
                if (trackingPositionCTS?.IsCancellationRequested ?? true)
                    trackingPositionCTS = new CancellationTokenSource();
                
                // 현재 마우스가 가리키는 월드 좌표 캐싱
                cachedPosition = raycastHandler.GetMouseWorldPosition();
                TrackPopupPosition().Forget(); // 비동기 추적 루프 시작
            }
            
            Logg.Log($"[PopupUI.OnGetFromPool] END - Current scale: {Rect.localScale}");
        }

        // IPoolObject.OnReleaseFromPool() 구현
        // 오브젝트 풀에 반환될 때 호출 (정리 작업)
        // 추적 중지, 애니메이션 원상복구
        public virtual void OnReleaseFromPool()
        {
            // 월드 오브젝트 추적 중지
            if (!(trackingPositionCTS?.IsCancellationRequested ?? true))
            {
                trackingPositionCTS.Cancel();
            }

            // PopOut 애니메이션으로 스케일이 변경되었으면 원상복구
            if (playExitAnimation && exitAnimation == Enums.UIAnimationType.PopOut)
            {
                RollBackScale();
            }
        }

        // 오브젝트 풀에서 완전히 제거될 때 호출 (파괴 시)
        // CancellationTokenSource 완전 정리
        public void OnDestroyFromPool()
        {
            if (!(trackingPositionCTS?.IsCancellationRequested ?? true))
            {
                Util.ClearCTS(trackingPositionCTS);
            }
        }

        // UIManager를 통해 자기 자신을 즉시 풀에 반환
        public void ReleaseSelf()
        {
            UIManager.Instance.ClosePopupUIImmediately(this);
        }

        #endregion
        
    }
}

