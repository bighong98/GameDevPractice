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
        private readonly Stack<PopupUI> popupStacks = new();
        
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
            Logg.Log($"[UIManager]OnEscapeCalled. popupStack.Count: {popupStacks?.Count}", Logg.LoggingMode.Completed);
            if (popupStacks?.Count != 0)
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
                cs.referenceResolution = new Vector2(1920, 1080); // (1920, 1080) is magic number
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
        
        // 팝업UI 호출 기능 함수
        // uiName: key값으로 사용해 어드레서블에서 팝업 프리팹을 불러오고, 풀링 적용하여 화면에 띄움
        // allowDuplicatePopup: 중복 팝업 허용 여부
        // uiName 입력하지 않으면 "클래스명.prefab"으로 탐색함 -> 팝업 프리팹 어드레서블 key값을 클래스명과 동일하게 설정하는 것을 권장
        // public T ShowPopupUI<T>(string uiName = null) where T : PopupUI
        // {
        //     Type type = typeof(T);
            
        //     // Toggle 모드인 경우, pool.Get() 전에 먼저 중복 확인
        //     if (popupPools.TryGetValue(type, out var existingPool))
        //     {
        //         // 이미 풀이 존재한다면, 중복 팝업이 있는지 먼저 확인
        //         if (IsPopupInStack<T>())
        //         {
        //             // Toggle: 기존 팝업만 닫고 새로 생성하지 않음
        //             if (CloseDuplicatePopup<T>())
        //             {
        //                 Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] Toggle mode: closed existing popup without creating new one", Logg.LoggingMode.Completed);
        //                 return null;
        //             }
        //         }
        //     }
            
        //     T popup;
            
        //     if (popupPools.TryGetValue(type, out var pool))
        //     {
        //         popup = pool.Get() as T;
        //     }
        //     else
        //     {
        //         string key = uiName ?? $"{type.Name}.prefab";
        //         if (ResourceManager.Instance.Load<UnityEngine.Object>(key) is not GameObject loadedUI)
        //             return null;
                
        //         var uiPool = PoolManager.Instance.GetPool(
        //             loadedUI,
        //             parent: GetUIParent(UICanvas.Popup),
        //             capacity: 1, maxSize: 10, registerPool: false
        //         );
                
        //         popupPools[type] = uiPool;
        //         popup = popupPools[type].Get() as T;
        //         keyTypeDictionary.TryAdd(key, type);
        //     }

        //     if (popup == null) return null;

        //     switch (popup.DuplicatedPopupHandling)
        //     {
        //         case PopupUI.DuplicatedPopupHandle.Replace:
        //             CloseDuplicatePopup<T>();
        //             break;
        //         case PopupUI.DuplicatedPopupHandle.Toggle:
        //             if (CloseDuplicatePopup<T>()) {
        //                 popup.ReleaseSelf();
        //                 return null;
        //             }
        //             break;
        //         default:
        //             break;
        //     }
            
        //     popupStacks.Push(popup);
            
        //     if (popup.PauseRequired)
        //         InputManager.Instance.PauseGame();

        //     lastPopupOpenTime = Time.unscaledTime;
            
        //     InputManager.Instance.EnableUIActionMap();
            
        //     Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] new Popup. name: {popup.name} popupStack.Count: {popupStacks.Count}", Logg.LoggingMode.Completed);
        //     return popup;
        // }

        public T ShowPopupUI<T>(string uiName = null) where T : PopupUI
        {
            var type = typeof(T);

            if (popupStacks.Count > 0 && IsPopupInStack<T>(out var inStackPopup))
            {
                switch (inStackPopup.DuplicatedPopupHandling)
                {
                    case PopupUI.DuplicatedPopupHandle.Replace:
                        Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] Replace mode: close existing popup and creat new one", Logg.LoggingMode.InProgress);
                        CloseDuplicatePopup<T>();
                        break;
                    case PopupUI.DuplicatedPopupHandle.Toggle:
                        if (CloseDuplicatePopup<T>())
                        {
                            Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] Toggle mode: close existing popup without creating new one", Logg.LoggingMode.InProgress);
                            return null;
                        }
                        break;
                    case PopupUI.DuplicatedPopupHandle.Allow:
                    default:
                        break;
                }
            }

            // if (IsPopupInStack<T>(out var inStackPopup) 
            //     && inStackPopup.DuplicatedPopupHandling == PopupUI.DuplicatedPopupHandle.Toggle
            //     && CloseDuplicatePopup<T>())
            // {
            //     Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] Toggle mode: closed existing popup without creating new one", Logg.LoggingMode.InProgress);
            //     return null;
            // }

            var popup = GetPopupInstance<T>(type, uiName);
            if (popup == null) return null;

            popupStacks.Push(popup);
            
            if (popup.PauseRequired)
                InputManager.Instance.PauseGame();

            lastPopupOpenTime = Time.unscaledTime;
            
            InputManager.Instance.EnableUIActionMap();
            
            Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] new Popup. name: {popup.name} popupStack.Count: {popupStacks.Count}", Logg.LoggingMode.InProgress);
            return popup;
        }

        private T GetPopupInstance<T>(Type type, string uiName) where T : PopupUI
        {
            if (popupPools.TryGetValue(type, out var pool))
            {
                return pool.Get() as T;
            }

            string key = uiName ?? $"{type.Name}.prefab";
            if (ResourceManager.Instance.Load<UnityEngine.Object>(key) is not GameObject loadedUI)
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
            return popupPools[type].Get() as T;
        }

        public bool ClosePopupUI(PopupUI popup, bool escapableCheck = true, bool ignoreOpenThreshold = true, bool waitForAnimation = true)
        {
            if (popupStacks.Count == 0 || (escapableCheck && !popupStacks.Peek().Escapable))
                return false;
            
            if (popupStacks.Peek() != popup) // 
            {
                Logg.Log($"[{nameof(UIManager)}.{nameof(ClosePopupUI)}()]: failed to close popup : {popup.name}", Logg.LoggingMode.Completed);
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
                Logg.Log($"{nameof(UIManager)}.{nameof(ClosePopupUI)}: popupStacks.Peek is empty or already closed. trying to close next popup", Logg.LoggingMode.Completed);
                return ClosePopupUI(); // 다음 순서 팝업 닫기
            }
            
            if (popupPools.TryGetValue(popup.GetType(), out var popupPool))
            {
                if (waitForAnimation)
                {
                    popup.OnPopupClosedAsync().ContinueWith(() =>
                    {
                        try { HandleTimePauseAndReleasePopup(); }
                        catch (Exception e) { Logg.LogError($"[{nameof(UIManager)}] Error during popup closing: {e}"); }
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
                InputManager.Instance.DisableUIActionMap();
            }
            
            return true; // separator for local method HandleTimePauseAndReleasePopup()
            
            void HandleTimePauseAndReleasePopup()
            {
                if (!IsPausedRequired()) // 일시정지가 필요한 팝업이 없다면
                {
                    InputManager.Instance.ResumeGame(); // 게임 일시정지 해제
                }

                popupPool.Release(popup); // 팝업 닫기 (풀에 반환)
                sortOrders[(int)UICanvas.Popup]--;
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
            ResourceManager.Instance.ReserveOperation(() => {
                if (ResourceManager.Instance.Instantiate("TooltipUI.prefab", root) is { } loadedPrefab
                    && loadedPrefab.GetComponent<TooltipUI>() is { } loadedTooltip)
                {
                    Tooltip = loadedTooltip;
                }
                else { Logg.Log($"Tooltip is null"); }
            });
        }

        public void ShowTooltip(int errorType, bool hideAfterDelay = false, float delayDuration = 2.0f) // 3.0f is magic number
        {
            if (errorType == (int)Enums.TooltipErrorType.Empty) return;
            int order = sortOrders[(int)UICanvas.Popup];
            Tooltip.tooltipCanvas.sortingOrder = order; // 언제나 최상단 팝업 UI보다 한단계 더 위로
            Tooltip.Show(errorType, hideAfterDelay, delayDuration);
        }

        public void ShowTooltip(string tooltipString, bool hideAfterDelay = false, float delayDuration = 3.0f)
        {
            int order = sortOrders[(int)UICanvas.Popup];
            Tooltip.tooltipCanvas.sortingOrder = order + 1; // 언제나 최상단 팝업 UI보다 한단계 더 위로
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
                Logg.Log($"duplicate popup closed: {duplicatePopup}", Logg.LoggingMode.InProgress);
                return true;
            }
            else
            {
                Logg.Log("There is no duplicate popup", Logg.LoggingMode.InProgress);
                return false;
            }
        }

        private bool IsPopupInStack<T>()
        {
            foreach (var popup in popupStacks)
            {
                if (popup is T) return true;
            }
            return false;
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
            InputManager.Instance.OnSingleClicked -= OnPopupOutSideSelected; // 중복 구독 방지
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

