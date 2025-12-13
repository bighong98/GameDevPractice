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
        private OptionMenuUI optionMenu;
        
        // scriptable objects
        private SceneCatalogSO sceneCatalogSO;
        private SceneUIListSO sceneUIListSO;
        private UICanvasSettingSO uiCanvasSettingSO;
        
        // resource key (Addressables)
        private const string SceneCatalogSOKey = "SceneCatalogSO";
        private const string SceneUIListSOKey = "SceneUIListSO";
        private const string UICanvasSettingSOKey = "UICanvasSettingSO";
        private const string TooltipUIPrefabKey = "TooltipUI.prefab";
        private const string OptionMenuUIKey = "OptionMenuUI";


        // default value
        private const int DefaultReadyMadePopupCount = 1; // 팝업용 오브젝트 풀 생성 시 초기 생성 개수
        private const int MaxDuplicatePopupCount = 10; // 팝업용 오브젝트 풀에서 생성 가능한 동일 팝업 최대 개수

        #region Singleton

        protected override void InitOnceAfterPreLoad()
        {
            base.InitOnceAfterPreLoad();
            resourceLoader = ServiceLocator.Get<IResourceLoader>();

            LoadData();
            SetUIContainer();
            SetTooltip();
            // SetOptionMenu();
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
        // UI 컨테이너 초기 설정 (UI_Root 생성 및 캔버스 계층 구조 생성)
        // Scene, AnchoredOverlay, Popup 3가지 타입의 캔버스를 생성
        private void SetUIContainer()
        {
            // UI_Root GameObject 생성 및 씬 전환 시 파괴되지 않도록 설정
            var rootGo = new GameObject(name: UIRootName);
            DontDestroyOnLoad(rootGo);
            root = rootGo.transform;
            canvases = new();
            
            var t = typeof(UICanvas);
            // UICanvas enum의 모든 타입에 대해 캔버스 생성
            foreach (var canvasType in (UICanvas[])Enum.GetValues(typeof(UICanvas)))
            {
                ResetCanvasOrder(canvasType);
                
                var go = new GameObject(Enum.GetName(t, canvasType));
                go.transform.SetParent(root);
                SetCanvas(go, canvasType);
                canvases.Add(go);
            }
        }

        // 캔버스의 정렬 순서 초기화 (ScriptableObject에서 기본값 가져오기)
        private void ResetCanvasOrder(UICanvas canvasType)
        {
            if (uiCanvasSettingSO != null 
                && uiCanvasSettingSO.GetCanvasSetting(canvasType)?.defaultSortingOrder 
                    is { } resultSortingOrder)
                sortOrders[(int)canvasType] = resultSortingOrder;
        }
        
        private void LoadData()
        {
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
        }

        #endregion

        private void OnEscapeCalled()
        {
            Logg.Log($"[UIManager]OnEscapeCalled. popupStack.Count: {popupStacks.Count}", 
                Logg.LoggingMode.Completed);
            if (popupStacks.Count != 0)
            {
                ClosePopupUI();
                return;
            }
            
            ShowOptionMenu();
        }

        #region Common UI Method

        // UI Canavas 설정 일괄 적용 
        public void SetCanvas(GameObject go, UICanvas canvasType, bool isInteractable = true,  bool renderWorldSpace = false)
        {
            // Canvas 컴포넌트 추가 또는 가져오기
            Canvas canvas = go.GetOrAddComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = renderWorldSpace ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
            }
            
            // CanvasScaler 컴포넌트 설정 (1920x1080 기준 스케일링)
            CanvasScaler cs = go.GetOrAddComponent<CanvasScaler>();
            if (cs != null)
            {
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
            }
            
            // 상호작용 가능하면 GraphicRaycaster 추가
            if (isInteractable)
                go.GetOrAddComponent<GraphicRaycaster>();
            
            SortCanvas(canvas, canvasType);
        }

        // PopupUI에 Canvas 설정 (팝업 전용)
        // RenderType에 따라 Canvas 모드 설정
        // CanvasGroup으로 투명도 제어 (애니메이션용)
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

        // Canvas의 sortingOrder 수동 설정
        public void SortCanvas(Canvas canvas, int sortOrder = 0)
        { 
            canvas.sortingOrder = sortOrder;
            canvas.overrideSorting = true;
        }

        // Canvas sortingOrder 자동 정렬 (캔버스 타입별 구분)
        public void SortCanvas(Canvas canvas, UICanvas canvasType)
        {
            if (uiCanvasSettingSO == null) return;
            
            int order = sortOrders[(int)canvasType] += 1;
            canvas.sortingOrder = order;
            canvas.overrideSorting = true;
        }
        
        // 특정 타입의 UI 부모 Transform을 반환
        private Transform GetUIParent(UICanvas type)
        {
            if (canvases[(int)type] is { } canvasGo) 
                return canvasGo.transform;
            return null;
        }

        #endregion

        #region Scene UI Method

        // 현재 씬에 맞는 SceneUI를 비동기로 로드하고 설정
        // 동일한 SceneUI가 이미 있으면 갱신만 수행, 다르면 교체
        // Singleton.InitAfterPreLoad()에서 실행
        private async UniTask SetSceneUIAsync()
        {
            if (sceneCatalogSO == null || sceneUIListSO == null)
            {
                Logg.LogError($"[UIManager] sceneCatalogSO: {sceneCatalogSO}, sceneUIListSO: {sceneUIListSO}");
                return;
            }
            
            // 현재 씬 정보 가져오기
            var currentSceneEntry = sceneCatalogSO.GetCurrentSceneEntry();
            if (currentSceneEntry == null) return;
            var targetSceneUIRef = sceneUIListSO.GetSceneUIByScene(currentSceneEntry.sceneRef);
            if (targetSceneUIRef == null) return;
            var loadedSceneUI = await resourceLoader.LoadAsync<GameObject>(targetSceneUIRef);
            if (loadedSceneUI == null) return;
            
            // // 현재 SceneUI와 동일한 경우 변경 없이 갱신만 요청
            // if (sceneUI != null && loadedSceneUI == sceneUI.Origin)
            // {
            //     sceneUI.RefreshUI();
            //     return;
            // }

            if (sceneUI != null)
            {
                // 현재 SceneUI와 동일한 경우 변경 없이 갱신만 요청, 종료
                if (loadedSceneUI == sceneUI.Origin)
                {
                    sceneUI.RefreshUI();
                    return;
                }
                // 기존 SceneUI가 있으면 풀에 반환
                else PoolManager.Instance.ReleaseFromPool(sceneUI);
            }
            
            // // 기존 SceneUI가 있으면 풀에 반환
            // if (sceneUI != null)
            //     PoolManager.Instance.ReleaseFromPool(sceneUI);
            // 새로운 SceneUI를 풀에서 가져오기
            sceneUI = PoolManager.Instance.GetFromPool<SceneUI>(loadedSceneUI, canvases[(int)UICanvas.Scene].transform);
            SetCanvas(sceneUI.gameObject, UICanvas.Scene);
            sceneUI.RefreshUI();
        }

        public bool TryGetSceneUI(out SceneUI ui)
        {
            if (!sceneUI.IsAlive())
            {
                ui = default;
                return false;
            }

            ui = sceneUI;
            return true;
        }

        #endregion

        #region Overlay UI Method (Not Popup)

        // 오브젝트 풀에서 UI를 가져오는 제네릭 메서드
        // PopupUI가 아닌 일반 UI에 사용 (예: AnchoredOverlay UI)
        public T GetUIFromPool<T>(GameObject prefab, UICanvas canvasType) where T : BaseUI, IPoolObject
        {
            return PoolManager.Instance.GetFromPool<T>(prefab, canvases[(int)canvasType]?.transform);
        }

        #endregion

        #region Popup UI Method

        // 팝업 UI 생성/활성화 메서드
        // 오브젝트 풀링, 중복 팝업 처리 (Allow/Toggle/Replace), 팝업 스택 관리, 일시정지 처리 등 포함
        public T ShowPopupUI<T>(string uiName = null) where T : PopupUI
        {
            var type = typeof(T);

            // 중복 검사가 필요한 팝업인지 확인 및 스택에 동일 타입 팝업이 있는지 검사
            if (ShouldScanDuplicate(type) 
                && popupStacks.Count > 0 
                && IsPopupInStack<T>(out var inStackPopup))
            {
                // 중복 처리 정책에 따라 분기
                switch (inStackPopup.DuplicatedPopupHandling)
                {
                    case PopupUI.DuplicatedPopupHandle.Replace: // 기존 닫고 새로 열기
                        Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] Replace mode: close existing popup and create new one", 
                                Logg.LoggingMode.Completed);
                        ClosePopupUI(inStackPopup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true);
                        break;
                    case PopupUI.DuplicatedPopupHandle.Toggle: // 기존 닫기만 수행
                        if (ClosePopupUI(inStackPopup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true))
                        {
                            Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] Toggle mode: close existing popup without creating new one", 
                                    Logg.LoggingMode.Completed);
                            return null;
                        }
                        break;
                    case PopupUI.DuplicatedPopupHandle.Allow: // 중복 허용
                    default:
                        break;
                }
            }

            // 팝업 인스턴스 가져오기 (풀에서 또는 새로 생성)
            var popup = GetPopupInstance<T>(type, uiName);
            if (popup == null) return null;

            // 팝업 스택에 추가
            popupStacks.Push(popup);
            
            // 일시정지 필요 시 게임 일시정지
            if (popup.PauseRequired)
                InputManager.Instance.PauseGame();

            // 팝업 열림 시간 기록 (빠른 닫기 방지용)
            lastPopupOpenTime = Time.unscaledTime;
            
            // UI 액션맵 활성화 (ESC 등의 입력 받기)
            InputManager.Instance.EnableUIActionMap();
            
            Logg.Log($"[{nameof(UIManager)}.{nameof(ShowPopupUI)}()] new Popup. name: {popup.name} popupStack.Count: {popupStacks.Count}", 
                    Logg.LoggingMode.Completed);
            return popup;
        }


        // 특정 팝업 UI 닫기
        // escapableCheck: Escapable 속성 확인 여부
        // ignoreOpenThreshold: 열림 throttling 무시 여부
        // waitForAnimation: 닫힘 애니메이션 대기 여부
        public bool ClosePopupUI(PopupUI popup, bool escapableCheck = true, bool ignoreOpenThreshold = true, bool waitForAnimation = true)
        {
            if (popup == null || popupStacks.Count == 0)
                return false;

            // Escapable 체크: 닫기 불가능한 팝업이면 취소
            if (escapableCheck && !popup.Escapable)
                return false;

            // 빠른 닫기 방지: 열린지 0.05초 이내면 취소
            if (!ignoreOpenThreshold && IsBeforePopupThreshold())
                return false;
            
            // 스택에서 팝업 제거
            if (!popupStacks.Remove(popup))
            {
                Logg.Log($"[{nameof(UIManager)}.{nameof(ClosePopupUI)}()]: popup not found in stack : {popup.name}", Logg.LoggingMode.Completed);
                return false;
            }

            // 팝업 풀에서 닫기 처리
            if (popupPools.TryGetValue(popup.GetType(), out var popupPool))
            {
                ClosePopupInternal(popup, popupPool, waitForAnimation);
            }

            // 모든 팝업이 닫혔으면 UI 액션맵 비활성화
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

        // 팝업 닫기 내부 처리 (애니메이션 처리 후 풀 반환)
        private void ClosePopupInternal(PopupUI popup, ObjectPool<IPoolObject> popupPool, bool waitForAnimation)
        {
            if (waitForAnimation)
            {
                // 비동기로 종료 애니메이션 재생 후 정리
                popup.OnPopupClosedAsync().ContinueWith(() =>
                {
                    try { HandleTimePauseAndReleasePopup(popup, popupPool); }
                    catch (Exception e) { Logg.LogError($"[{nameof(UIManager)}] Error during popup closing: {e}"); }
                }).Forget();
            }
            else
            {
                // 즉시 정리 및 풀 반환
                popup.OnPopupClosed();
                HandleTimePauseAndReleasePopup(popup, popupPool);
            }
        }

        // 팝업 인스턴스 가져오기 (오브젝트 풀 사용)
        // 풀이 없으면 새로 생성
        // 중복 정책 캐싱
        private T GetPopupInstance<T>(Type type, string uiName) where T : PopupUI
        {
            // 이미 생성된 풀이 있으면 재사용
            if (popupPools.TryGetValue(type, out var pool))
            {
                return pool.Get() as T;
            }

            // 풀이 없으면 리소스 로드 후 풀 생성
            string key = uiName ?? $"{type.Name}.prefab";
            if (ResourceManager.Instance.Load<UnityEngine.Object>(key) is not GameObject loadedUI)
                return null;
            
            // 오브젝트 풀 생성 (초기 1개, 최대 10개)
            var uiPool = PoolManager.Instance.GetPool(
                loadedUI,
                parent: GetUIParent(UICanvas.Popup),
                capacity: DefaultReadyMadePopupCount, // 1
                maxSize: MaxDuplicatePopupCount, // 10
                registerPool: false
            );
            
            // 타입별 풀 등록
            popupPools[type] = uiPool;
            keyTypeDictionary.TryAdd(key, type);
            
            // 풀에서 인스턴스 가져오기
            var popup = popupPools[type].Get() as T;
            if (popup == null) return null;

            // 중복 정책 캐싱 (최초 1회)
            CacheDuplicatePolicy(type, popup);
            return popup;
        }

        // 현재 활성화된 모든 팝업UI 비활성화 (최상단부터 순서대로)
        // escapableCheck 옵션을 무시함 -> 추후 escapable: false 인 팝업은 남겨두고 싶다면 메서드 추가 필요
        public void CloseAllPopupUI()
        {
            while (popupStacks.TryPeek(out var popup))
            {
                ClosePopupUI(popup, escapableCheck: false, ignoreOpenThreshold: true, waitForAnimation: true);
            }
        }

        // 일시정지 상태 확인 및 팝업을 풀에 반환
        private void HandleTimePauseAndReleasePopup(PopupUI popup, ObjectPool<IPoolObject> popupPool)
        {
            // 일시정지가 필요한 팝업이 없다면 게임 일시정지 해제
            if (!IsPausedRequired())
            {
                InputManager.Instance.ResumeGame();
            }

            // 팝업을 풀에 반환 및 Canvas sorting order 감소
            popupPool.Release(popup);
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

        public int GetPopupCount() => popupStacks.Count;
        
        #endregion

        #region PopupUI Helper Method

        // 현재 활성화된 팝업 중 일시정지가 필요한 팝업이 있는지 확인
        private bool IsPausedRequired()
        {
            if (popupStacks.Count == 0) return false;

            // 팝업 중 하나라도 일시정지를 요구하면 true 반환
            foreach (var popup in popupStacks)
            {
                if (popup.PauseRequired) return true;
            }

            return false;
        }

        private readonly Dictionary<Type, bool> popupDuplicateCheck = new();

        // 팝업의 중복 처리 정책을 캐싱하여 반복 검사 방지
        private void CacheDuplicatePolicy(Type type, PopupUI popup)
        {
            // Allow가 아니면 중복 검사 필요
            bool needCheck = popup.DuplicatedPopupHandling != PopupUI.DuplicatedPopupHandle.Allow;
            popupDuplicateCheck[type] = needCheck;
        }

        // 해당 타입의 팝업이 중복 검사가 필요한지 확인
        private bool ShouldScanDuplicate(Type type)
        {
            // 이미 한 번 이상 생성해서 정책을 캐싱해둔 경우
            if (popupDuplicateCheck.TryGetValue(type, out var needScan))
            {
                return needScan; // Toggle/Replace -> true, Allow -> false
            }

            // 처음 보는 타입이면 최초 한 번은 검사 (타입 캐싱 위해)
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

        // 팝업이 열린 지 일정 시간(0.05초) 이내인지 확인 (의도치 않은 팝업 동시다발적 비활성화 방지)
        private bool IsBeforePopupThreshold()
        {
            bool rValue = Time.unscaledTime - lastPopupOpenTime < PopupOpenThreshold;
            if (rValue) Logg.Log($"{nameof(IsBeforePopupThreshold)}: ClosePopupUI Guarded");

            return rValue;
        }

        #endregion

        #region Frequently Used UI Call
        
        // Tooltip (현재 미사용)
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
        
        // Option Menu

        private void SetOptionMenu()
        {
            if(!resourceLoader.TryLoad<GameObject>(OptionMenuUIKey, out var result)
               || !result.TryGetComponent(out optionMenu))
            {
                Logg.LogError($"[UIManager] failed to load option menu");
            }
        }

        private void ShowOptionMenu()
        {
            ShowPopupUI<OptionMenuUI>(OptionMenuUIKey);
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

        #region DeInitialization

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

        #endregion
    
    }
}

