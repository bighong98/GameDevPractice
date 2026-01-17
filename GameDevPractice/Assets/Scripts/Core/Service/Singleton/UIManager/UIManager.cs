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
        private readonly Dictionary<Type, ObjectPool<IPoolObject>> uiPools = new();
        private readonly Dictionary<string, IPoolObject> activeUIByKey = new();

        private Transform root;
        private List<GameObject> canvases;
        private readonly int[] sortOrders = new int[Enum.GetValues(typeof(UICanvas)).Length];


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

        #region UI Object Pool
        private bool TryGetOrCreateUIPool(Type type, string key, UICanvas canvasType, out ObjectPool<IPoolObject> pool)
        {
            if (uiPools.TryGetValue(type, out pool))
                return true;

            if (ResourceManager.Instance.Load<UnityEngine.Object>(key) is not GameObject loadedUI)
            {
                pool = null;
                return false;
            }

            return CreateUIPool(type, key, canvasType, loadedUI, out pool);
        }

        private bool TryGetOrCreateUIPool(Type type, string key, UICanvas canvasType, GameObject prefab, out ObjectPool<IPoolObject> pool)
        {
            if (uiPools.TryGetValue(type, out pool))
                return true;

            if (prefab == null)
            {
                pool = null;
                return false;
            }

            return CreateUIPool(type, key, canvasType, prefab, out pool);
        }

        private bool CreateUIPool(Type type, string key, UICanvas canvasType, GameObject prefab, out ObjectPool<IPoolObject> pool)
        {
            var setting = uiCanvasSettingSO?.GetCanvasSetting(canvasType);
            int capacity = DefaultReadyMadePopupCount;
            int maxSize = MaxDuplicatePopupCount;
            if (setting != null)
            {
                if (setting.PoolCapacity > 0) capacity = setting.PoolCapacity;
                if (setting.PoolMaxSize > 0) maxSize = setting.PoolMaxSize;
            }

            Action<IPoolObject> createAction = obj =>
            {
                if (obj is Component comp)
                    SetCanvas(comp.gameObject, canvasType);
            };

            pool = PoolManager.Instance.GetPool(
                prefab,
                parent: GetUIParent(canvasType),
                createAction: createAction,
                capacity: capacity,
                maxSize: maxSize,
                registerPool: false
            );

            uiPools[type] = pool;
            keyTypeDictionary.TryAdd(key, type);
            return true;
        }

        #endregion


        #region Overlay UI Method (Not Popup)

        // 오브젝트 풀에서 UI를 가져오는 제네릭 메서드
        // PopupUI가 아닌 일반 UI에 사용 (예: AnchoredOverlay UI)
        public T GetUIFromPool<T>(GameObject prefab, UICanvas canvasType) where T : BaseUI, IPoolObject
        {
            if (prefab == null) return null;

            string key = $"{prefab.name}.prefab";
            if (!TryGetOrCreateUIPool(typeof(T), key, canvasType, prefab, out var pool))
                return null;

            return pool.Get() as T;
        }

        public T ShowUI<T>(string key, UICanvas canvasType) where T : BaseUI, IPoolObject
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            if (activeUIByKey.TryGetValue(key, out var existing)
                && existing is Component existingComp
                && existingComp.gameObject.activeSelf)
            {
                return existing as T;
            }

            if (ResourceManager.Instance.Load<UnityEngine.Object>(key) is not GameObject prefab)
                return null;

            if (!TryGetOrCreateUIPool(typeof(T), key, canvasType, prefab, out var pool))
                return null;

            var ui = pool.Get() as T;
            if (ui == null) return null;

            activeUIByKey[key] = ui;
            return ui;
        }

        public void ReleaseUI(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!activeUIByKey.TryGetValue(key, out var ui)) return;

            ReleaseUI(ui);
            activeUIByKey.Remove(key);
        }

        public void ReleaseUI(IPoolObject ui)
        {
            if (ui == null) return;

            if (uiPools.TryGetValue(ui.GetType(), out var pool))
                pool.Release(ui);
            else
                PoolManager.Instance.ReleaseFromPool(ui);

            string removeKey = null;
            foreach (var (key, value) in activeUIByKey)
            {
                if (ReferenceEquals(value, ui))
                {
                    removeKey = key;
                    break;
                }
            }
            if (removeKey != null)
                activeUIByKey.Remove(removeKey);
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

