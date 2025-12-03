using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using UnityEngine;
using TH.Resource;
using TH.SceneManagement;
using TH.Utils;
using UnityEngine.SceneManagement;

namespace TH.Core
{
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                if (Util.IsQuitting)
                {
                    Logg.LogError($"[{typeof(T).Name}] 파괴 이후에 Instance에 접근 시도 발생. {Environment.StackTrace}");
                }
                
                _instance = FindFirstObjectByType<T>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    _instance = obj.AddComponent<T>();
                    DontDestroyOnLoad(obj);
                }

                return _instance;
            }
        }

        protected ISceneLoader sceneLoader;
        protected IResourceLoader resourceLoader;
        
        private readonly Queue<Action> reservedOperations = new(); // 초기화 전 외부에서 예약된 작업 목록

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Logg.Log($"{typeof(T).Name}: 중복 인스턴스가 존재하여 파괴됩니다.");
                Destroy(gameObject);
            }
        }

        protected virtual void OnEnable()
        {
            if (IsInvalidInstance()) return;
            if (IsInitOnce) return;

            resourceLoader = ServiceLocator.Get<IResourceLoader>();
            sceneLoader = ServiceLocator.Get<ISceneLoader>();

            sceneLoader.OnBeforeSceneChanged += this.Clear;
            sceneLoader.OnSceneChanged += this.OnSceneChanged;
            
            OnSceneChanged(default);
        }

        protected virtual void Start() {}

        public bool IsInitOnce { get; private set; }
        public bool IsInitOnceAfterPreLoad { get; private set; }
        public bool IsInit { get; private set; }
        public bool IsInitAfterPreLoad { get; private set; }

        // 인스턴스 생성 후 최초 1회만 실행, OnSceneChanged에서 실행
        protected virtual void InitOnce()
        {
            IsInitOnce = true;
            Logg.Log($"[{GetType().Name}] InitOnce() invoked", Logg.LoggingMode.Completed);
        }
        
        // 인스턴스 생성 후, 초기 리소스 준비 여부 확인하고 최초 1회만 실행, OnSceneChanged에서 실행
        protected virtual void InitOnceAfterPreLoad()
        {
            IsInitOnceAfterPreLoad = true;
            Logg.Log($"[{GetType().Name}] InitOnceAfterPreLoad() invoked", Logg.LoggingMode.Completed);
        }
        // 인스턴스 생성 및 씬 로드 직후마다 실행
        protected virtual void Init()
        {
            IsInit = true;
            Logg.Log($"[{GetType().Name}] Init() invoked", Logg.LoggingMode.Completed);
        } 
        protected virtual void InitAfterPreLoad()// 인스턴스 생성 및 씬 로드 직후마다, 초기 리소스 준비 여부 확인하고 실행
        {
            Logg.Log($"[{GetType().Name}] InitAfterPreLoad() invoked", Logg.LoggingMode.Completed);
            RunReservedOperations();
            IsInitAfterPreLoad = true;
        } 

        // 씬 이동마다 필요한 정리 작업
        // 오버라이드해서 사용 및 base.Clear() 호출 필요
        protected virtual UniTask Clear() 
        {
            Logg.Log($"[{GetType().Name}] Clear() invoked", Logg.LoggingMode.Completed);
            // 플래그 초기화
            IsInit = false;
            IsInitAfterPreLoad = false;
            return UniTask.CompletedTask;
        }
        
        // Awake()에서 GameSceneManager에 초기화 작업을 델리게이트로 전달
        // 씬 로드가 완료된 후 싱글톤 초기화가 진행됨
        protected virtual void OnSceneChanged(Scene scene)
        {
            Logg.Log($"[{GetType().Name}] OnSceneChanged invoked in scene '{scene.name}'",Logg.LoggingMode.Completed);

            if (!IsInitOnce) InitOnce();
            if (!IsInit) Init();
            if (!IsInitOnceAfterPreLoad)
                resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, InitOnceAfterPreLoad);
            if (!IsInitAfterPreLoad)
                resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, InitAfterPreLoad);
        }
        
        protected bool IsInvalidInstance()
        {
            return _instance != this;
        }

        private void RunReservedOperations()
        {
            while (reservedOperations.Count > 0)
            {
                try { reservedOperations.Dequeue()?.Invoke(); }
                catch (Exception e) {
                    Logg.LogError($"[{typeof(T).Name}] failure occured while running reserved operations. {e}");
                }
            }
        }

        public void ReserveOperation(Action action)
        {
            if (_instance is not Singleton<T> singleton) return;
            
            if (singleton.IsInvalidInstance())
            {
                Logg.Log($"[{typeof(T).Name}] fail occured while {{nameof(ReserveOperation)}}. Instance is not valid", Logg.LoggingMode.Completed);
                return;
            }

            if (singleton.IsInitAfterPreLoad) action?.Invoke();
            else singleton.reservedOperations.Enqueue(action);
        }
        
        protected virtual void OnDestroy()
        {
            Logg.Log($"[{typeof(T).Name}] OnDestroy Stack: {Environment.StackTrace}", Logg.LoggingMode.Completed);
        }
    }
}

