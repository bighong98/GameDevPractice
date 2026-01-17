using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Pool;
using TH.Resource;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace TH.Core.Service
{
    [Preserve]
    public partial class UIManager : Singleton<UIManager>, ISingleton
    {
        private readonly PopupStack popupStacks = new();

        private readonly Dictionary<string, Type> keyTypeDictionary = new();
        private readonly Dictionary<Type, ObjectPool<IPoolObject>> popupPools = new();

        private Transform root;
        private List<GameObject> canvases;
        private readonly int[] sortOrders = new int[Enum.GetValues(typeof(UICanvas)).Length];

        private SceneUI sceneUI;

        private const float PopupOpenThreshold = 0.05f;
        private float lastPopupOpenTime;

        private readonly Dictionary<Type, bool> popupDuplicateCheck = new();

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

        private const string TooltipUIPrefabKey = "TooltipUI.prefab";
        private const string OptionMenuUIKey = "OptionMenuUI";
        private const string InventoryUIKey = "InventoryUI.prefab";

        // outer service
        private readonly IResourceLoader resourceLoader;

        private UIManager()
        {
            resourceLoader = ServiceLocator.Get<IResourceLoader>();
            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, TaskAfterPreLoad);
        }

        #region Initialization

        private void TaskAfterPreLoad()
        {
            LoadData();
            SetUIContainer();
            // SetTooltip();
        }

        private const string UIRootName = "UIs";
        // UI 컨테이너 초기 설정 (UI_Root 생성 및 캔버스 계층 구조 생성)
        // Scene, AnchoredOverlay, Popup 3가지 타입의 캔버스를 생성
        private void SetUIContainer()
        {
            // UI_Root GameObject 생성 및 씬 전환 시 파괴되지 않도록 설정
            var rootGo = new GameObject(name: UIRootName);
            UnityEngine.Object.DontDestroyOnLoad(rootGo);
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

        #region ISingleton

        public UniTask BeforeSceneLoad(CancellationToken externalToken)
        {
            if (externalToken.IsCancellationRequested) return UniTask.CompletedTask;

            CloseAllPopupUI();
            DisConnectInputEvents();

            return UniTask.CompletedTask;
        }

        public async UniTask AfterSceneLoad(CancellationToken externalToken)
        {
            ConnectInputEvents();
            await SetSceneUIAsync(SceneManager.GetActiveScene()).AttachExternalCancellation(externalToken);
        }

        #endregion

        #region Scene UI Method

        // 현재 씬에 맞는 SceneUI를 비동기로 로드하고 설정
        // 동일한 SceneUI가 이미 있으면 갱신만 수행, 다르면 교체
        // Singleton.InitAfterPreLoad()에서 실행
        private async UniTask SetSceneUIAsync(Scene scene)
        {
            if (sceneCatalogSO == null || sceneUIListSO == null)
            {
                Logg.LogError($"[UIManager] sceneCatalogSO: {sceneCatalogSO}, sceneUIListSO: {sceneUIListSO}");
                return;
            }

            if (!sceneCatalogSO.TryGetSceneEntry(scene, out var currentSceneEntry))
            {
                Logg.LogWarning($"[UIManager] currentSceneEntry is null from (scene: {scene.name})");
                return;
            }

            var targetSceneUIRef = sceneUIListSO.GetSceneUIByScene(currentSceneEntry?.sceneRef);
            if (targetSceneUIRef == null)
            {
                Logg.LogWarning($"[UIManager] targetSceneUIRef is null");
                return;
            }

            var loadedSceneUI = await resourceLoader.LoadAsync<GameObject>(targetSceneUIRef);
            if (loadedSceneUI == null)
            {
                Logg.LogWarning($"[UIManager] loadedSceneUIis null");
                return;
            }

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

            // 새로운 SceneUI를 풀에서 가져오기
            sceneUI = PoolManager.Instance.GetFromPool<SceneUI>(loadedSceneUI, canvases[(int)UICanvas.Scene].transform);
            SetCanvas(sceneUI.gameObject, UICanvas.Scene);
            sceneUI.RefreshUI();
        }

        public bool TryGetSceneUI(out SceneUI ui)
        {
            if (!sceneUI.IsNotNull())
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

        #region DeInitialization

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

namespace TH.UI
{
    public enum UICanvas
    {
        Scene, // 씬UI
        HUD, // 게임 오브젝트와 함께 움직이는 UI
        Popup, // 팝업UI
        Feedback, // 툴팁, 화면 터치 이펙트, 토스트UI 등 포함 
        FullScreen, // 화면 전체 마스킹 용도
    }
}

