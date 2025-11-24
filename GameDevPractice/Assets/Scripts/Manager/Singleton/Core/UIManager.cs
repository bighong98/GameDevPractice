using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using TH.Core.Pool;
using TH.Resource;
using TH.Core;
using TH.Core.Service;
using TH.Utils;

namespace TH.UI
{
    public enum UICanvas
    {
        Scene, // 씬UI 캔버스
        AnchoredOverlay, // 게임 오브젝트와 함께 움직이는 UI용 캔버스
        Popup, // 팝업UI 캔버스
    }
    public class UIManager : Singleton<UIManager>
    {
        private readonly PopupStack popupStacks = new();
        
        private readonly Dictionary<string, Type> keyTypeDictionary = new();
        private readonly Dictionary<Type, ObjectPool<IPoolObject>> popupPools = new();
        
        [SerializeField] private Transform root;
        [SerializeField] private List<GameObject> canvases;
        private readonly int[] sortOrders = new int[Enum.GetValues(typeof(UICanvas)).Length];
        
        private SceneUI sceneUI;
        
        private const float PopupOpenThreshold = 0.05f;
        private float lastPopupOpenTime;
        
        // Frequently Used UI
        public TooltipUI Tooltip; 
        
        // scriptable objects
        private SceneCatalogSO sceneCatalogSO;
        private SceneUIListSO sceneUIListSO;
        private UICanvasSettingSO uiCanvasSettingSO;
        
        // resource key (Addressables)
        private const string SceneCatalogSOKey = "SceneCatalogSO";
        private const string SceneUIListSOKey = "SceneUIListSO";
        private const string UICanvasSettingSOKey = "UICanvasSettingSO";
        private const string TooltipUIPrefabKey = "TooltipUI.prefab";


        // default value
        private const int DefaultReadyMadePopupCount = 1; // 팝업용 오브젝트 풀 생성 시 초기 생성 개수
        private const int MaxDuplicatePopupCount = 10; // 팝업용 오브젝트 풀에서 생성 가능한 동일 팝업 최대 개수

        #region Singleton

        protected override void InitOnceAfterPreLoad()
        {
            base.InitOnceAfterPreLoad();
            resourceLoader = ServiceLocator.Get<IResourceLoader>();

            if (!resourceLoader.TryLoad(SceneCatalogSOKey, out sceneCatalogSO))
            {
                Logg.LogError($"[UIManager] failed to load sceneCatalogSO");
                return;
            }
            
            if (!resourceLoader.TryLoad(SceneUIListSOKey, out sceneUIListSO))
            {
                Logg.LogError($"[UIManager] failed to load sceneUIListSO");
                return;
            }
            
            if (!resourceLoader.TryLoad(UICanvasSettingSOKey, out uiCanvasSettingSO))
            {
                Logg.LogError($"[UIManager] failed to load sceneUIListSO");
                return;
            }
            
            SetUIContainer();
            SetTooltip();
        }

        protected override void Init()
        {
            base.Init();
            ConnectInputEvents();
        }

        protected override void InitAfterPreLoad()
        {
            SetSceneUIAsync().ContinueWith(() =>
            {
                base.InitAfterPreLoad();
            });
        }

        #endregion

        #region Initialization

        private void ConnectInputEvents()
        {
            InputManager.Instance.OnEscaped -= OnEscapeCalled;
            InputManager.Instance.OnEscaped += OnEscapeCalled;

            InputManager.Instance.OnSingleClicked -= OnPopupOutSideSelected; // 중복 구독 방지
            InputManager.Instance.OnSingleClicked += OnPopupOutSideSelected;
        }

        private const string UIRootName = "UI_Root";
        private void SetUIContainer()
        {
            var rootGo = new GameObject(name: UIRootName);
            DontDestroyOnLoad(rootGo);
            root = rootGo.transform;
            canvases = new();
            
            var t = typeof(UICanvas);
            foreach (var canvasType in (UICanvas[])Enum.GetValues(typeof(UICanvas)))
            {
                ResetCanvasOrder(canvasType);
                
                var go = new GameObject(Enum.GetName(t, canvasType));
                go.transform.SetParent(root);
                SetCanvas(go, canvasType);
                canvases.Add(go);
            }
        }

        private void ResetCanvasOrder(UICanvas canvasType)
        {
            if (uiCanvasSettingSO != null 
                && uiCanvasSettingSO.GetCanvasSetting(canvasType)?.defaultSortingOrder 
                    is { } resultSortingOrder)
                sortOrders[(int)canvasType] = resultSortingOrder;
        }

        #endregion

        private void OnEscapeCalled()
        {
            Logg.Log($"[UIManager]OnEscapeCalled. popupStack.Count: {popupStacks.Count}", Logg.LoggingMode.Completed);
            if (popupStacks.Count != 0)
            {
                ClosePopupUI();
            }
        }

        #region Scene UI Method

        private async UniTask SetSceneUIAsync()
        {
            if (sceneCatalogSO == null || sceneUIListSO == null)
            {
                Logg.LogError($"[UIManager] sceneCatalogSO: {sceneCatalogSO}, sceneUIListSO: {sceneUIListSO}");
                return;
            }
            
            var currentSceneEntry = sceneCatalogSO.GetCurrentSceneEntry();
            if (currentSceneEntry == null) return;
            var targetSceneUIRef = sceneUIListSO.GetSceneUIByScene(currentSceneEntry.sceneRef);
            if (targetSceneUIRef == null) return;
            var loadedSceneUI = await resourceLoader.LoadAsync<GameObject>(targetSceneUIRef); // todo: add token
            if (loadedSceneUI == null) return;
            
            // 현재 SceneUI와 동일한 경우 변경 없이 갱신만 요청
            if (sceneUI != null && loadedSceneUI == sceneUI.Origin)
            {
                sceneUI.RefreshUI();
                return;
            }
            
            if (sceneUI != null)
                PoolManager.Instance.ReleaseFromPool(sceneUI);
            sceneUI = PoolManager.Instance.GetFromPool<SceneUI>(loadedSceneUI, canvases[(int)UICanvas.Scene].transform);
            SetCanvas(sceneUI.gameObject, UICanvas.Scene); 
        }

        #endregion

        #region Overlay UI Method (Not Popup)

        public T GetUIFromPool<T>(GameObject prefab, UICanvas canvasType) where T : BaseUI, IPoolObject
        {
            return PoolManager.Instance.GetFromPool<T>(prefab, canvases[(int)canvasType]?.transform);
        }

        #endregion
        
        #region Common UI Method

        // UI에 일괄적으로 설정 적용 목적
        // sort = true일 경우 자동으로 최상단으로 배치
        // sort = false, sortOrder = {숫자}일 경우 sortOrder값 기준으로 sortingOrder 적용
        // isToast는 현재 미사용 (추후 삭제 혹은 ToastUI 기능 추가 고려)
        // PopupUI 인스턴스의 OnCreateFromPool()에서 호출
        // renderWorldSpace: Canvas의 render mode 결정: true: world space, false: overlay (현재 구현x. 사용x)
        public void SetCanvas(GameObject go, UICanvas canvasType, bool isInteractable = true,  bool renderWorldSpace = false)
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
                cs.referenceResolution = uiCanvasSettingSO.ReferenceResolution;
            }
            
            if (isInteractable)
                go.GetOrAddComponent<GraphicRaycaster>();
            
            SortCanvas(canvas, canvasType);
        }

        public void SetCanvas(PopupUI popup, bool isInteractable = true)
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
                cg.alpha = 0f; // UI 애니메이션, 애니메이션 전처리를 위해 투명화 -> PopupUI.OnGetFromPool()에서 투명도 제거처리
            
            if (isInteractable)
                popup.gameObject.GetOrAddComponent<GraphicRaycaster>();
        }

        public void SortCanvas(Canvas canvas, int sortOrder = 0)
        { 
            // 캔버스 렌더링 순서 정렬
            canvas.sortingOrder = sortOrder;
            canvas.overrideSorting = true;
        }

        public void SortCanvas(Canvas canvas, UICanvas canvasType)
        {
            if (uiCanvasSettingSO == null) return;
            
            int order = sortOrders[(int)canvasType] += 1;
            canvas.sortingOrder = order;
            canvas.overrideSorting = true;
        }
        
        private Transform GetUIParent(UICanvas type)
        {
            if (canvases[(int)type] is { } canvasGo) 
                return canvasGo.transform;
            return null;
        }

        #endregion

        #region Popup UI Method

        public T ShowPopupUI<T>(string uiName = null) where T : PopupUI
        {
            var type = typeof(T);

            if (ShouldScanDuplicate(type) 
                && popupStacks.Count > 0 
                && IsPopupInStack<T>(out var inStackPopup))
            {
                switch (inStackPopup.DuplicatedPopupHandling)
                {
                    case PopupUI.DuplicatedPopupHandle.Replace:
                        Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] Replace mode: close existing popup and create new one", Logg.LoggingMode.Completed);
                        ClosePopupUI(inStackPopup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true);
                        break;
                    case PopupUI.DuplicatedPopupHandle.Toggle:
                        if (ClosePopupUI(inStackPopup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true))
                        {
                            Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] Toggle mode: close existing popup without creating new one", Logg.LoggingMode.Completed);
                            return null;
                        }
                        break;
                    case PopupUI.DuplicatedPopupHandle.Allow:
                    default:
                        break;
                }
            }

            var popup = GetPopupInstance<T>(type, uiName);
            if (popup == null) return null;

            popupStacks.Push(popup);
            
            if (popup.PauseRequired)
                InputManager.Instance.PauseGame();

            lastPopupOpenTime = Time.unscaledTime;
            
            InputManager.Instance.EnableUIActionMap();
            
            Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] new Popup. name: {popup.name} popupStack.Count: {popupStacks.Count}", Logg.LoggingMode.Completed);
            return popup;
        }

        public bool ClosePopupUI(PopupUI popup, bool escapableCheck = true, bool ignoreOpenThreshold = true, bool waitForAnimation = true)
        {
            if (popup == null || popupStacks.Count == 0)
                return false;

            if (escapableCheck && !popup.Escapable)
                return false;

            if (!ignoreOpenThreshold && IsBeforePopupThreshold())
                return false;
            

            if (!popupStacks.Remove(popup, cutTail: true))
            {
                Logg.Log($"[{nameof(UIManager)}.{nameof(ClosePopupUI)}()]: popup not found in stack : {popup.name}", Logg.LoggingMode.Completed);
                return false;
            }

            if (popupPools.TryGetValue(popup.GetType(), out var popupPool))
            {
                ClosePopupInternal(popup, popupPool, waitForAnimation);
            }

            if (popupStacks.Count == 0)
            {
                InputManager.Instance.DisableUIActionMap();
            }

            return true;
        }

        public bool ClosePopupUI(bool loopEnabled = true, bool escapableCheck = true, bool ignoreOpenThreshold = true, bool waitForAnimation = true)
        {
            // Top에서부터 유효한 팝업을 찾을 때까지 반복
            while (popupStacks.TryPeek(out var top))
            {
                // 비활성화된 팝업이면 스택에서 제거만 하고 다음으로 넘어감
                if (!top.gameObject.activeSelf)
                {
                    popupStacks.Pop();
                    if (!loopEnabled)
                        return false;

                    continue;
                }

                // 실제 닫기 로직은 인스턴스 오버로드에 위임
                return ClosePopupUI(top, escapableCheck, ignoreOpenThreshold, waitForAnimation);
            }

            return false;
        }

        private void ClosePopupInternal(PopupUI popup, ObjectPool<IPoolObject> popupPool, bool waitForAnimation)
        {
            if (waitForAnimation)
            {
                popup.OnPopupClosedAsync().ContinueWith(() =>
                {
                    try { HandleTimePauseAndReleasePopup(popup, popupPool); }
                    catch (Exception e) { Logg.LogError($"[{nameof(UIManager)}] Error during popup closing: {e}"); }
                }).Forget();
            }
            else
            {
                popup.OnPopupClosed(); // 팝업 종료 직전 필요한 작업 수행
                HandleTimePauseAndReleasePopup(popup, popupPool);
            }
        }

        private T GetPopupInstance<T>(Type type, string uiName) where T : PopupUI
        {
            if (popupPools.TryGetValue(type, out var pool))
            {
                return pool.Get() as T;
            }

            string key = uiName ?? $"{type.Name}.prefab";
            if (!resourceLoader.TryLoad<GameObject>(key, out var loadedUI))
                return null;
            
            var uiPool = PoolManager.Instance.GetPool(
                loadedUI,
                parent: GetUIParent(UICanvas.Popup),
                capacity: DefaultReadyMadePopupCount, // 1
                maxSize: MaxDuplicatePopupCount, // 10
                registerPool: false
            );
            
            popupPools[type] = uiPool;
            keyTypeDictionary.TryAdd(key, type);
            
            var popup = popupPools[type].Get() as T;
            if (popup == null) return null;

            CacheDuplicatePolicy(type, popup);
            return popup;
        }

        private void HandleTimePauseAndReleasePopup(PopupUI popup, ObjectPool<IPoolObject> popupPool)
        {
            if (!IsPausedRequired()) // 일시정지가 필요한 팝업이 없다면
            {
                InputManager.Instance.ResumeGame(); // 게임 일시정지 해제
            }

            popupPool.Release(popup); // 팝업 닫기 (풀에 반환)
            sortOrders[(int)UICanvas.Popup]--;
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
            while (popupStacks.TryPeek(out var popup))
            {
                ClosePopupUI(popup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true);
            }
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

        private readonly Dictionary<Type, bool> popupDuplicateCheck = new();

        private void CacheDuplicatePolicy(Type type, PopupUI popup)
        {
            // popup null 검사는 호출 측에서 수행
            bool needCheck = popup.DuplicatedPopupHandling != PopupUI.DuplicatedPopupHandle.Allow;
            popupDuplicateCheck[type] = needCheck;
        }

        private bool ShouldScanDuplicate(Type type)
        {
            // 이미 한 번 이상 생성해서 정책을 캐싱해둔 경우
            // DuplicatedPopupHandle.Toggle/Replace -> true, Allow -> false
            if (popupDuplicateCheck.TryGetValue(type, out var needScan))
            {
                return needScan;
            }

            // 처음 보는 타입이면 최초 한 번은 검사 (-> 타입 캐싱)
            return true;
        }

        private bool IsPopupInStack<T>(out T inStackPopup) where T : PopupUI
        {
            foreach (var popup in popupStacks)
            {
                if (popup is not T p) continue;  
                
                inStackPopup = p;
                return true;
            }

            inStackPopup = default;
            return false;
        }


        private void OnPopupOutSideSelected(Vector2 selectedPos)
        {
            if (IsBeforePopupThreshold()) return;

            while (popupStacks.TryPeek(out var peek) && !peek.gameObject.activeSelf)
            {
                popupStacks.Pop();
            }
            
            if (popupStacks.TryPeek(out var peekPopup) && peekPopup is { CloseOnOuterBackgroundClick: true })
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(peekPopup.ContentArea, selectedPos))
                {
                    Logg.Log("Outer background touched. close popup", Logg.LoggingMode.Completed);
                    ClosePopupUI(peekPopup, escapableCheck: true, ignoreOpenThreshold: false, waitForAnimation: true);
                }
            }
        }

        private bool IsBeforePopupThreshold()
        {
            bool rValue = Time.unscaledTime - lastPopupOpenTime < PopupOpenThreshold;
            if (rValue) Logg.Log($"{nameof(IsBeforePopupThreshold)}: ClosePopupUI Guarded");

            return rValue;
        }
        
        #endregion

        #region Frequently Used UI Call

        private void SetTooltip()
        {
            resourceLoader.OnLabelResourcesLoadedAll -= SetTooltip; // 중복 구독 방지
            resourceLoader.OnLabelResourcesLoadedAll += SetTooltip;
        }

        private void SetTooltip(string label)
        {
            if (label != Constants.PreLoadLabel) return;
            
            if (!resourceLoader.TryLoad<GameObject>(TooltipUIPrefabKey, out var loadedPrefab)
                || Instantiate(loadedPrefab, root) is not {} instantiatePrefab
                || !instantiatePrefab.TryGetComponent<TooltipUI>(out var loadedTooltip))
            {
                Logg.LogError($"[UIManager] failed to load tooltip");
                return;
            }

            Tooltip = loadedTooltip;
        }

        public void ShowTooltip(int errorType, bool hideAfterDelay = false, float delayDuration = 2.0f) // 3.0f is magic number
        {
            if (errorType == (int)Enums.TooltipErrorType.Empty) return;
            if (!Tooltip.IsAlive()) return;

            int order = sortOrders[(int)UICanvas.Popup];
            Tooltip.tooltipCanvas.sortingOrder = order + 1; // 언제나 최상단 팝업 UI보다 한단계 더 위로
            Tooltip.Show(errorType, hideAfterDelay, delayDuration);
        }

        public void ShowTooltip(string tooltipString, bool hideAfterDelay = false, float delayDuration = 3.0f)
        {
            if (!Tooltip.IsAlive()) return;
            
            int order = sortOrders[(int)UICanvas.Popup];
            Tooltip.tooltipCanvas.sortingOrder = order + 1; // 언제나 최상단 팝업 UI보다 한단계 더 위로
            Tooltip.Show(tooltipString, hideAfterDelay, delayDuration);
        }

        public void HideTooltip()
        {
            Tooltip.Hide();
        }

        #endregion

        protected override void OnDestroy()
        {
            if (Util.IsQuitting) return;
            base.OnDestroy();
            
            keyTypeDictionary.Clear();

            foreach (var pool in popupPools.Values)
            {
                pool.Clear();
            }
            popupPools.Clear();
            popupStacks.Clear();
            
            ClearValues();
        }

        protected override UniTask Clear()
        {
            base.Clear();

            CloseAllPopupUI();
            DisConnectInputEvents();
            
            return UniTask.CompletedTask;
        }

        private void DisConnectInputEvents()
        {
            if (Util.IsQuitting || InputManager.Instance == null) return;

            InputManager.Instance.OnEscaped -= OnEscapeCalled;
            InputManager.Instance.OnSingleClicked -= OnPopupOutSideSelected;
        }

        private void ClearValues()
        {
            foreach (var canvasType in (UICanvas[])Enum.GetValues(typeof(UICanvas)))
            {
                ResetCanvasOrder(canvasType);
            }
        }
    }
}

