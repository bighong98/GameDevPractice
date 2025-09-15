using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace RPG.UI
{
    public enum UICanvas
    {
        Scene, // 씬UI 캔버스, Sorting Order: 0~99
        AnchoredOverlay, // 게임 오브젝트와 함께 움직이는 UI요소, Sorting Order: 100~199 
        Popup, // 팝업UI 최상단 캔버스, Sorting Order: 200~
    }
    public class UIManager : Singleton<UIManager>
    {
        private int _order = 10; // 10 is magic number
        private readonly Stack<PopupUI> popupStacks = new Stack<PopupUI>();
        private readonly Dictionary<string, Type> keyTypeDictionary = new Dictionary<string, Type>();
        private readonly Dictionary<Type, ObjectPool<PopupUI>> popupPools = new Dictionary<Type, ObjectPool<PopupUI>>();
        
        [SerializeField] private Transform root;
        [SerializeField] private List<GameObject> canvases;

        private BaseUI sceneUI;
        private GraphicRaycaster sceneUIGraphicRaycaster;
        public GraphicRaycaster SceneUIGraphicRaycaster { get { return sceneUIGraphicRaycaster; } }
        // public event Action<int> OnTimeScaleChanged; // 현재 미사용
        
        private const float popupOpenThreshold = 0.05f;
        private float lastPopupOpenTime;
        
        #region Frequently Used UI
        
        public TooltipUI Tooltip; 
        
        #endregion
        
        protected override void InitOnce()
        {
            // GameSceneManager.Instance.RegisterCleanupTask(async () =>
            // {
            //     await Clear();
            // });
        }

        protected override void InitOnceAfterPreLoad(bool done)
        {
            
        }

        protected override void Init()
        {
            // Util.SetMainCameraForUtilClass();
            
            root = new GameObject("UI_Root").transform;
            canvases = new();
            
            var t = typeof(UICanvas);
            foreach (var canvasType in Enum.GetValues(t))
            {
                var go = new GameObject(Enum.GetName(t, canvasType));
                go.transform.SetParent(root);
                // todo: 타입별 캔버스 세팅 설정 로직 추가 (Canvas 데이터 관리용 Scriptable Object 사용 고려) 
                SetCanvas(go);
                canvases.Add(go);
            }
            
            InputManager.Instance.OnEscaped += OnEscapeCalled;
            
            InputManager.Instance.OnSingleClicked -= OnPopupOutSideSelected; // 중복 구독 방지
            InputManager.Instance.OnSingleClicked += OnPopupOutSideSelected;
        }
        
        protected override void InitAfterPreLoad(bool done)
        {
            if (!done) return;
            
            SetSceneUI();
            SetTooltip();
        }

        private void OnEscapeCalled()
        {
            Util.Log($"[UIManager]OnEscapeCalled. popupStack.Count: {popupStacks?.Count}", Util.LoggingMode.Completed);
            if (popupStacks?.Count != 0)
            {
                ClosePopupUI();
            }
        }

        #region Scene UI Method

        private void SetSceneUI()
        {
            //todo: 현재 활성화된 씬 타입 받아서 씬에 적합한 씬UI 호출
            ResourceManager.Instance.ReserveOperation(() =>
            {
                if (ResourceManager.Instance.Instantiate("GameSceneUI.prefab", GetUIParent(UICanvas.Scene)) is { } loadedPrefab
                    && loadedPrefab.GetComponent<GameSceneUI>() is { } loadedSceneUI)
                {
                    SetCanvas(loadedPrefab, isInteractable: true);
                    sceneUI = loadedSceneUI;
                }
            });
        }

        #endregion

        #region Overlay UI Method (Not Popup)

        public T GetUIFromPool<T>(GameObject prefab, UICanvas canvasType) where T : BaseUI, IPoolObject
        {
            return PoolingManager.Instance.GetFromPool<T>(prefab, canvases[(int)canvasType]?.transform);
        }

        #endregion
        
        #region Common UI Method

        // UI에 일괄적으로 설정 적용 목적
        // sort = true일 경우 자동으로 최상단으로 배치
        // sort = false, sortOrder = {숫자}일 경우 sortOrder값 기준으로 sortingOrder 적용
        // isToast는 현재 미사용 (추후 삭제 혹은 ToastUI 기능 추가 고려)
        // PopupUI 인스턴스의 OnCreateFromPool()에서 호출
        // renderWorldSpace: Canvas의 render mode 결정: true: world space, false: overlay (현재 구현x. 사용x)
        public void SetCanvas(GameObject go, bool sort = true, int sortOrder = 0, bool isInteractable = true,  bool renderWorldSpace = false)
        { 
            Canvas canvas = go.GetOrAddComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = renderWorldSpace ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
            }
            
            CanvasScaler cs = go.GetOrAddComponent<CanvasScaler>();
            if (cs != null)
            {
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080); // (1920, 1080) is magic number
            }
            
            if (isInteractable)
                go.GetOrAddComponent<GraphicRaycaster>();
            
            SortCanvas(canvas, sort, sortOrder);
        }

        public void SetCanvas(PopupUI popup, bool sort = true, int sortOrder = 0, bool isInteractable = true)
        {
            Canvas canvas = popup.gameObject.GetOrAddComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = popup.UiRenderType switch
                {
                    Enums.UIRenderType.WorldSpace => RenderMode.WorldSpace,
                    Enums.UIRenderType.ScreenCamera => RenderMode.ScreenSpaceCamera,
                    _ => RenderMode.ScreenSpaceOverlay
                };
                canvas.overrideSorting = true;
            }

            CanvasGroup cg = popup.gameObject.GetOrAddComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 0f; // UI 애니메이션, 애니메이션 전처리를 위해 투명화 -> PopupUI.OnGetFromPool()에서 투명도 제거처리
            }
            
            // CanvasScaler cs = popup.gameObject.GetOrAddComponent<CanvasScaler>();
            // if (cs != null)
            // {
            //     cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // }
            
            if (isInteractable)
                popup.gameObject.GetOrAddComponent<GraphicRaycaster>();
            
            SortCanvas(canvas, sort, sortOrder);
        }

        public void SortCanvas(Canvas canvas, bool autoSort = true, int sortOrder = 0)
        { // 캔버스 렌더링 순서 정렬
            if (autoSort)
            {
                canvas.sortingOrder = _order;
                canvas.overrideSorting = true;
                _order++;
            }
            else
            {
                canvas.sortingOrder = sortOrder;
            }
        }

        private Transform GetUIParent(UICanvas type)
        {
            if (canvases[(int)type] is { } canvasGo) 
                return canvasGo.transform;
            return null;
        }

        #endregion

        #region Popup UI Method
        
        // 팝업UI 호출 기능 함수
        // uiName: key값으로 사용해 어드레서블에서 팝업 프리팹을 불러오고, 풀링 적용하여 화면에 띄움
        // allowDuplicatePopup: 중복 팝업 허용 여부
        // uiName 입력하지 않으면 "클래스명.prefab"으로 탐색함 -> 팝업 프리팹 어드레서블 key값을 클래스명과 동일하게 설정하는 것을 권장
        public T ShowPopupUI<T>(string uiName = null) where T : PopupUI
        {
            T popup;
            Type type = typeof(T);
            
            if (popupPools.TryGetValue(type, out var pool))
            {
                popup = pool.Get() as T;
            }
            else
            {
                string key = uiName ?? $"{type.Name}.prefab"; // 어드레서블 key를 임의로 지정하지 않으면 "팝업UI 클래스명}.prefab" 자동 사용
                if (ResourceManager.Instance.Load<UnityEngine.Object>(key) is not GameObject loadedUI)
                    return null;
                
                // var uiPool = PoolingManager.Instance.GetPool<PopupUI>(
                //         loadedUI, GetUIContainer(loadedUI.GetComponent<PopupUI>().UiRenderType), capacity: 2, maxSize: 10, registerPool: false); // 2, 10 is magic number
                var uiPool = PoolingManager.Instance.GetPool<PopupUI>(
                    loadedUI,
                    parent: GetUIParent(UICanvas.Popup),
                    capacity: 2, maxSize: 10, registerPool: false
                    ); // 2, 10 is magic number
                
                popupPools[type] = uiPool;
                popup = popupPools[type].Get() as T;
                keyTypeDictionary.TryAdd(key, type); // 임시로 key-type 매칭용 딕셔너리에 저장 (현재 미사용. 추후 제거 고려)
            }

            if (popup == null) return null;

            switch (popup.DuplicatedPopupHandling)
            {
                case PopupUI.DuplicatedPopupHandle.Replace:
                    CloseDuplicatePopup<T>(); // 중복 팝업 닫기 시도
                    break;
                case PopupUI.DuplicatedPopupHandle.Toggle:
                    if (CloseDuplicatePopup<T>()) {
                        popup.ReleaseSelf(); // 띄우려 시도한 팝업을 즉시 풀에 반환(팝업 닫기 연출x)
                        return null; // 중복 팝업이 있다면 닫고 즉시 함수 호출 종료 (null 리턴)
                    }
                    break;
                default:
                    break;
            }
            
            popupStacks.Push(popup);
            
            if (popup.PauseRequired)
            {
                // GameManager.Instance.PauseGame();
                InputManager.Instance.PauseGame();
            }

            lastPopupOpenTime = Time.unscaledTime; // 팝업 닫기 지연 시간
            
            InputManager.Instance.EnableUIActionMap();
            
            Util.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] new Popup. name: {popup.name} popupStack.Count: {popupStacks.Count}", Util.LoggingMode.Completed);
            return popup;
        }

        public bool ClosePopupUI(PopupUI popup, bool escapableCheck = true, bool ignoreOpenThreshold = true, bool waitForAnimation = true)
        {
            if (popupStacks.Count == 0 || (escapableCheck && !popupStacks.Peek().Escapable))
                return false;
            
            if (popupStacks.Peek() != popup) // 
            {
                Util.Log($"[{nameof(UIManager)}.{nameof(ClosePopupUI)}()]: failed to close popup : {popup.name}", Util.LoggingMode.Completed);
                return false;
            }
            
            return ClosePopupUI(loopEnabled: false, escapableCheck, ignoreOpenThreshold, waitForAnimation); // loop disabled 
        }

        public bool ClosePopupUI(bool loopEnabled = true, bool escapableCheck = true, bool ignoreOpenThreshold = true, bool waitForAnimation = true)
        {
            if (popupStacks.Count == 0 || (escapableCheck && !popupStacks.Peek().Escapable))
                return false;

            if (!ignoreOpenThreshold && IsBeforePopupThreshold()) 
                return false;

            PopupUI popup = popupStacks.Pop();
            if ((popup == null || !popup.gameObject.activeSelf) && loopEnabled)
            {
                Util.Log($"{nameof(UIManager)}.{nameof(ClosePopupUI)}: popupStacks.Peek is empty or already closed. trying to close next popup", Util.LoggingMode.Completed);
                return ClosePopupUI(); // 다음 순서 팝업 닫기
            }
            
            if (popupPools.TryGetValue(popup.GetType(), out var popupPool))
            {
                if (waitForAnimation)
                {
                    popup.OnPopupClosedAsync().ContinueWith(() =>
                    {
                        try
                        {
                            HandleTimePauseAndReleasePopup();
                        }
                        catch (Exception e)
                        {
                            Util.LogError($"[{nameof(UIManager)}] Error during popup closing: {e}");
                        }
                    }).Forget();
                }
                else
                {
                    popup.OnPopupClosed(); // 팝업 종료 직전 필요한 작업 수행
                    HandleTimePauseAndReleasePopup();
                }
            }

            if (popupStacks.Count == 0)
            {
                // if (Util.IsQuitting) return;
                InputManager.Instance.DisableUIActionMap();
            }
            
            return false; // separator for local method HandleTimePauseAndReleasePopup()
            
            void HandleTimePauseAndReleasePopup()
            {
                if (!IsPausedRequired()) // 일시정지가 필요한 팝업이 없다면
                {
                    // GameManager.Instance.ResumeGame(); // 게임 일시정지 해제
                    InputManager.Instance.ResumeGame(); // 게임 일시정지 해제
                }

                popupPool.Release(popup); // 팝업 닫기 (풀에 반환)
                _order--;
            }
        }

        public void ClosePopupUIImmediately<T>(T popup) where T : PopupUI
        {
            Type type = popup.GetType();
            if (popupPools.TryGetValue(type, out var pool))
            {
                pool.Release(popup);
            }
        }

        public void CloseAllPopupUI()
        {
            while (popupStacks.Count > 0)
                ClosePopupUI();
        }

        public int GetPopupCount() => popupStacks.Count;

        private bool IsPausedRequired()
        {
            if (popupStacks.Count == 0) return false;

            foreach (var popup in popupStacks)
            {
                if (popup.PauseRequired) return true; // 팝업 중 하나라도 일시정지를 요구한다면 true 반환
            }

            return false;
        }

        private Transform GetUIContainer(Enums.UIRenderType type)
        {
            return type switch
            {
                Enums.UIRenderType.ScreenOverlay => root,
                Enums.UIRenderType.ScreenCamera => root,
                Enums.UIRenderType.WorldSpace => root,
                _ =>  null
            };
        }

        private void OnPopupOutSideSelected(Vector2 selectedPos)
        {
            if (IsBeforePopupThreshold()) return;

            while (popupStacks.TryPeek(out var peek) && (peek == null || !peek.gameObject.activeSelf))
            {
                popupStacks.Pop();
            }
            
            if (popupStacks.TryPeek(out var peekPopup) && peekPopup is { CloseOnOuterBackgroundClick: true })
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(peekPopup.ContentArea, selectedPos))
                {
                    Util.Log("Outer background touched. close popup", Util.LoggingMode.Completed);
                    ClosePopupUI(peekPopup, escapableCheck: true, ignoreOpenThreshold: false, waitForAnimation: true);
                }
            }
        }

        private bool IsBeforePopupThreshold()
        {
            bool rValue = Time.unscaledTime - lastPopupOpenTime < popupOpenThreshold;
            if (rValue)
            {
                Util.Log($"{nameof(IsBeforePopupThreshold)}: ClosePopupUI Guarded");
                return rValue;
            }

            return false;
        }
        
        #endregion

        #region Frequently Used UI Call

        private void SetTooltip()
        {
            ResourceManager.Instance.ReserveOperation(() => {
                if (ResourceManager.Instance.Instantiate("TooltipUI.prefab", root) is { } loadedPrefab
                    && loadedPrefab.GetComponent<TooltipUI>() is { } loadedTooltip)
                {
                    Tooltip = loadedTooltip;
                }
                else
                {
                    Util.Log($"Tooltip is null");
                }
            });
        }

        public void ShowTooltip(int errorType, bool hideAfterDelay = false, float delayDuration = 2.0f) // 3.0f is magic number
        {
            if (errorType == (int)Enums.TooltipErrorType.Empty) return;
            Tooltip.tooltipCanvas.sortingOrder = _order; // 언제나 최상단 팝업 UI보다 한단계 더 위로
            Tooltip.Show(errorType, hideAfterDelay, delayDuration);
        }

        public void ShowTooltip(string tooltipString, bool hideAfterDelay = false, float delayDuration = 3.0f)
        {
            Tooltip.tooltipCanvas.sortingOrder = _order + 1; // 언제나 최상단 팝업 UI보다 한단계 더 위로
            Tooltip.Show(tooltipString, hideAfterDelay, delayDuration);
        }

        public void HideTooltip()
        {
            Tooltip.Hide();
        }

        private bool IsAlreadyDuplicatePopup<T>()
        {
            if (popupStacks.Count == 0) return false;

            foreach (var popup in popupStacks)
            {
                if (popup is T duplicatePopup)
                {
                    return true;
                }
            }

            return false;
        }
        private bool CloseDuplicatePopup<T>()
        {
            if (popupStacks.Count != 0 && popupStacks.Peek() is T duplicatePopup)
            {
                ClosePopupUI(duplicatePopup as PopupUI);
                Util.Log($"duplicate popup closed: {duplicatePopup}", Util.LoggingMode.Completed);
                return true;
            }
            else
            {
                Util.Log("There is no duplicate popup", Util.LoggingMode.Completed);
                return false;
            }
        }

        #endregion

        protected override UniTask Clear()
        {
            base.Clear();
            keyTypeDictionary.Clear();

            foreach (var pool in popupPools.Values)
            {
                pool.Clear();
            }
            popupPools.Clear();
            popupStacks.Clear();
            
            ClearValue();
            return UniTask.CompletedTask;
        }

        private void ClearValue()
        {
            _order = 10; // 10 is magic number
        }
    }
}

