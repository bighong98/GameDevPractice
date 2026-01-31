using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Pool;
using TH.Resource;
using TH.UI;
using TH.UI.Service;
using TH.Utils;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace TH.Core.Service
{
    /// <summary>
    /// UI 시스템 전체를 관리하는 싱글톤 매니저 클래스.
    /// Canvas 계층 구조 관리, 팝업/씬UI 제어, UI 오브젝트 풀링을 담당.
    /// partial class로 Canvas, Input, Popup, SceneUI 기능이 분리되어 있음.
    /// </summary>
    [Preserve]
    public partial class UIManager : Singleton<UIManager>, ISingleton
    {
        /// <summary>열린 팝업들을 스택으로 관리 (LIFO 방식 닫기 지원)</summary>
        private readonly PopupStack popupStacks = new();

        /// <summary>UI 프리팹 기반 오브젝트 풀</summary>
        // 전제: 동일 프리팹은 항상 동일한 캔버스 타입에서만 사용된다.
        private readonly Dictionary<GameObject, ObjectPool<IPoolObject>> uiPools = new();
        /// <summary>UI 풀용 프리팹별 컨테이너 (캔버스 아래 프리팹별 그룹)</summary>
        private readonly Dictionary<GameObject, Transform> uiPoolContainers = new();

        /// <summary>현재 활성화된 UI를 키로 추적 (중복 표시 방지)</summary>
        private readonly Dictionary<string, IPoolObject> activeUIByKey = new();

        /// <summary>UI 계층 구조의 최상위 Root Transform (DontDestroyOnLoad)</summary>
        private Transform root;
        /// <summary>각 UICanvas 타입별 캔버스 GameObject 목록</summary>
        private readonly List<GameObject> canvases = new();
        /// <summary>각 캔버스 타입별 현재 sortOrder 값 (동적 정렬 용도)</summary>
        private readonly int[] sortOrders = new int[Enum.GetValues(typeof(UICanvas)).Length];


        #region ScriptableObject 데이터 레퍼런스
        /// <summary>씬 카탈로그 설정 데이터</summary>
        private SceneCatalogSO sceneCatalogSO;
        /// <summary>각 씬별 자동 로드할 UI 목록 데이터</summary>
        private SceneUIListSO sceneUIListSO;
        /// <summary>캔버스 타입별 설정 (풀 사이즈, sortOrder 등)</summary>
        private UICanvasSettingSO uiCanvasSettingSO;
        #endregion

        #region Addressables 리소스 키
        private const string SceneCatalogSOKey = "SceneCatalogSO";
        private const string SceneUIListSOKey = "SceneUIListSO";
        private const string UICanvasSettingSOKey = "UICanvasSettingSO";
        #endregion

        #region 기본값 상수
        /// <summary>팝업 풀 초기 생성 개수</summary>
        private const int DefaultReadyMadePopupCount = 1; // 팝업용 오브젝트 풀 생성 시 초기 생성 개수
        /// <summary>동일 팝업 최대 생성 가능 개수</summary>
        private const int MaxDuplicatePopupCount = 10;
        #endregion // 팝업용 오브젝트 풀에서 생성 가능한 동일 팝업 최대 개수

        #region 외부 서비스 의존성
        /// <summary>Addressables 리소스 로더 서비스</summary>
        private readonly IResourceLoader resourceLoader;
        #endregion

        /// <summary>
        /// 생성자: 리소스 로더를 가져오고 PreLoad 완료 후 초기화 작업 예약.
        /// ServiceLocator를 통해 IResourceLoader를 주입받음.
        /// </summary>
        private UIManager()
        {
            resourceLoader = ServiceLocator.Get<IResourceLoader>();
            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, TaskAfterPreLoad);
        }

        #region Initialization

        /// <summary>
        /// PreLoad 완료 후 실행되는 콜백.
        /// ScriptableObject 데이터 로드 및 UI 컨테이너 설정.
        /// </summary>
        private void TaskAfterPreLoad()
        {
            LoadData();
            SetUIContainer();
            PrepareFrequentlyUsedUIs();
            PrewarmInventoryUI();
            // SetTooltip();
        }

        /// <summary>UI Root GameObject의 이름</summary>
        private const string UIRootName = "UIs";
        // UI 컨테이너 초기 설정 (UI 오브젝트 풀 루트 컨테이너(UIs) 생성 및 캔버스 계층 구조 생성)
        // UICanvas enum 타입별로 캔버스 생성 (Scene, AnchoredOverlay, Popup, etc)
        /// <summary>
        /// UI 컨테이너 초기 설정.
        /// DontDestroyOnLoad로 UI_Root 생성 및 UICanvas enum의 모든 타입에 대해 캔버스 계층 구조 생성.
        /// Scene, HUD, Popup, Feedback, FullScreen 5가지 캔버스 생성.
        /// </summary>
        private void SetUIContainer()
        {
            // UI_Root GameObject 생성 및 씬 전환 시 파괴되지 않도록 설정
            var rootGo = new GameObject(name: UIRootName);
            UnityEngine.Object.DontDestroyOnLoad(rootGo);
            root = rootGo.transform;

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

        /// <summary>
        /// Addressables로 ScriptableObject 데이터 로드.
        /// SceneCatalogSO, SceneUIListSO, UICanvasSettingSO를 로드.
        /// </summary>
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

        /// <summary>
        /// 씬 전환 전 호출되는 ISingleton 콜백.
        /// 모든 팝업 닫기 및 입력 이벤트 연결 해제.
        /// </summary>
        /// <param name="externalToken">취소 토큰</param>
        public UniTask BeforeSceneLoad(CancellationToken externalToken)
        {
            if (externalToken.IsCancellationRequested) return UniTask.CompletedTask;

            CloseAllPopupUI();
            DisConnectInputEvents();

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 씬 전환 후 호출되는 ISingleton 콜백.
        /// 입력 이벤트 연결 및 해당 씬의 SceneUI 비동기 설정.
        /// </summary>
        /// <param name="externalToken">취소 토큰</param>
        public async UniTask AfterSceneLoad(CancellationToken externalToken)
        {
            ConnectInputEvents();
            await SetSceneUIAsync(SceneManager.GetActiveScene()).AttachExternalCancellation(externalToken);
        }

        #endregion

        #region UI Object Pool
        /// <summary>
        /// UI 풀을 가져오거나 새로 생성.
        /// 리소스 키로 Addressables에서 프리팩을 로드하여 풀 생성.
        /// </summary>
        /// <param name="key">Addressables 리소스 키</param>
        /// <param name="canvasType">대상 캔버스 타입</param>
        /// <param name="pool">출력: 생성/찾은 오브젝트 풀</param>

        /// <returns>성공 여부</returns>
        private bool TryGetOrCreateUIPool(string key, UICanvas canvasType, out ObjectPool<IPoolObject> pool)
        {
            if (ResourceManager.Instance.Load<UnityEngine.Object>(key) is not GameObject loadedUI)
            {
                pool = null;
                return false;
            }

            return TryGetOrCreateUIPool(loadedUI, canvasType, out pool);
        }

        /// <summary>
        /// UI 풀을 가져오거나 새로 생성 (프리팩 직접 전달 버전).
        /// 이미 로드된 프리팩을 사용하여 풀 생성.
        /// </summary>
        /// <param name="prefab">UI 프리팹</param>
        /// <param name="canvasType">대상 캔버스 타입</param>
        /// <param name="pool">출력: 생성/찾은 오브젝트 풀</param>

        /// <returns>성공 여부</returns>
        private bool TryGetOrCreateUIPool(GameObject prefab, UICanvas canvasType, out ObjectPool<IPoolObject> pool)
        {
            if (prefab == null)
            {
                pool = null;
                return false;
            }

            if (uiPools.TryGetValue(prefab, out pool))
                return true;

            return CreateUIPool(canvasType, prefab, out pool);
        }


        /// <summary>
        /// 새 UI 오브젝트 풀 생성.
        /// UICanvasSettingSO에서 풀 용량 설정을 가져오고 PoolManager를 통해 풀 생성.
        /// </summary>
        /// <param name="canvasType">대상 캔버스 타입</param>
        /// <param name="prefab">UI 프리팹</param>
        /// <param name="pool">출력: 생성된 오브젝트 풀</param>
        /// <returns>성공 여부</returns>
        private bool CreateUIPool(UICanvas canvasType, GameObject prefab, out ObjectPool<IPoolObject> pool)

        {
            pool = null;
            if (uiCanvasSettingSO.IsNull() || 
                uiCanvasSettingSO.GetCanvasSetting(canvasType) is not {} setting) 
                return false;
            
            // var setting = uiCanvasSettingSO?.GetCanvasSetting(canvasType);
            int capacity = DefaultReadyMadePopupCount;
            int maxSize = MaxDuplicatePopupCount;
            if (setting != null)
            {
                if (setting.PoolCapacity > 0) capacity = setting.PoolCapacity;
                if (setting.PoolMaxSize > 0) maxSize = setting.PoolMaxSize;
            }

            var cullingSystem = ServiceLocator.Get<IHUDCullingSystem>();
            Action<IPoolObject> createAction = obj =>
            {
                if (obj is Component comp)
                    SetCanvas(comp.gameObject, canvasType);
                if (obj is IHUDCullingBindable bindable)
                    bindable.ConfigureCulling(view => cullingSystem.Register(view), handle => cullingSystem.Unregister(handle));
            };

            pool = PoolManager.Instance.GetPool(
                prefab,
                parent: GetOrCreateUIPoolContainer(canvasType, prefab),

                createAction: createAction,
                capacity: capacity,
                maxSize: maxSize,
                registerPool: false
            );

            uiPools[prefab] = pool;

            return true;
        }

        #endregion


        #region Overlay UI Method (팝업이 아닌 일반 UI)

        // 오브젝트 풀에서 UI를 가져오는 제네릭 메서드
        // PopupUI가 아닌 일반 UI에 사용 (예: AnchoredOverlay UI)
        /// <summary>
        /// 오브젝트 풀에서 UI를 가져오는 제네릭 메서드.
        /// PopupUI가 아닌 일반 UI에 사용 (예: HUD, AnchoredOverlay UI).
        /// </summary>
        /// <typeparam name="T">BaseUI를 상속하고 IPoolObject를 구현한 UI 타입</typeparam>
        /// <param name="prefab">UI 프리팩</param>
        /// <param name="canvasType">표시할 캔버스 타입</param>
        /// <returns>풀에서 가져온 UI 인스턴스</returns>
        public T GetUIFromPool<T>(GameObject prefab, UICanvas canvasType) where T : BaseUI, IPoolObject
        {
            if (prefab == null) return null;

            if (!TryGetOrCreateUIPool(prefab, canvasType, out var pool))
                return null;
;

            return pool.Get() as T;
        }

        /// <summary>
        /// 키로 UI를 표시하거나 이미 활성화된 UI 반환.
        /// 중복 표시 방지 - 같은 키의 UI가 이미 활성화되어 있으면 기존 UI 반환.
        /// </summary>
        /// <typeparam name="T">BaseUI를 상속하고 IPoolObject를 구현한 UI 타입</typeparam>
        /// <param name="key">Addressables 리소스 키</param>
        /// <param name="canvasType">표시할 캔버스 타입</param>
        /// <returns>UI 인스턴스 (실패 시 null)</returns>
        public T ShowUI<T>(string key, UICanvas canvasType) where T : BaseUI, IPoolObject
        {
            if (string.IsNullOrWhiteSpace(key)) 
                return null;
            // 호출하려는 UI가 PopupUI인 경우 PopupUI 전용 호출 메서드로 연결 (리플렉션 사용)
            // 팝업은 가급적이면 ShowPopupUI<T>(string)으로 호출할 것
            if (canvasType == UICanvas.Popup && typeof(PopupUI).IsAssignableFrom(typeof(T)))
                return ShowPopupUIByType(typeof(T), key) as T;

            if (activeUIByKey.TryGetValue(key, out var existing)
                && existing is Component existingComp
                && existingComp.gameObject.activeSelf)
            {
                return existing as T;
            }

            if (ResourceManager.Instance.Load<UnityEngine.Object>(key) is not GameObject prefab)
                return null;

            if (!TryGetOrCreateUIPool(prefab, canvasType, out var pool))

                return null;

            var ui = pool.Get() as T;
            if (ui == null) return null;

            activeUIByKey[key] = ui;
            return ui;
        }

        /// <summary>
        /// 키로 UI를 풀에 반환.
        /// activeUIByKey에서 제거하고 풀에 반환하여 재사용 가능하게 함.
        /// </summary>
        /// <param name="key">Addressables 리소스 키</param>
        public void ReleaseUI(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!activeUIByKey.TryGetValue(key, out var ui)) return;

            ReleaseUI(ui);
            activeUIByKey.Remove(key);
        }

        /// <summary>
        /// UI 오브젝트를 풀에 직접 반환.
        /// uiPools에 등록된 풀이 있으면 해당 풀에, 없으면 PoolManager로 반환.
        /// activeUIByKey에서도 제거.
        /// </summary>
        /// <param name="ui">반환할 UI 오브젝트</param>
        public void ReleaseUI(IPoolObject ui)
        {
            if (ui == null) return;

            if (TryGetUIPool(ui, out var pool))
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

        private bool TryGetUIPool(IPoolObject ui, out ObjectPool<IPoolObject> pool)
        {
            pool = null;
            if (ui?.Origin == null)
                return false;

            return uiPools.TryGetValue(ui.Origin, out pool);
        }

        #endregion

        #region DeInitialization

        /// <summary>
        /// UIManager 상태 초기화.
        /// 모든 캔버스의 sortOrder를 기본값으로 리셋.
        /// </summary>
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
    /// <summary>
    /// 용도별 UI 캔버스 타입 enum
    /// 타입별로 별도의 Canvas GameObject로 분리되어 관리
    /// SetCanvas() 에서 캔버스 설정 일괄 적용 (sortOrder, render mode, etc) 
    /// - CanvasSettingSO 데이터 사용 (새로운 CanvasUI 타입 추가시 반드시 타입별 값 재확인할 것)
    /// </summary>
    public enum UICanvas
    { 
        Scene, /// <summary>씬 전용 UI (항상 표시, HUD 등)</summary>
        HUD, /// <summary>게임 오브젝트에 고정되어야하는 UI (체력바, 네임택 등)</summary>
        Popup, /// <summary>팝업 UI (모달 창, 확인 대화상자 등)</summary>
        FeedbackOverlay, /// <summary>피드백 UI - RenderMode: Overlay (툴팁, 터치 이펙트, 토스트 등)</summary>
        FeedbackCamera, /// <summary>피드백 UI - RenderMode: Camera (툴팁, 터치 이펙트, 토스트 등)</summary>
        FullScreen, /// <summary>화면 전체 마스킹 (로딩, 전환 페이드 등)</summary>
    }
}

