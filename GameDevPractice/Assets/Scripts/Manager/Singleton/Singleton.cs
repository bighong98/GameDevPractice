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
        
        private bool hasInitializedOnce; // 최초 인스턴스 생성 직후에만 초기화 필요한 작업 관리 플래그
        private bool isInitialized; // 씬마다 초기화 필요한 작업 관리 플래그
        
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

        protected virtual void Start()
        {
            if (ServiceLocator.TryGet(out ISceneLoader sceneLoader))
            {
                sceneLoader.OnBeforeSceneChanged += this.Clear;
                sceneLoader.OnSceneChanged += this.OnSceneChanged;
            }
            
            OnSceneChanged(SceneManager.GetActiveScene());
        }
        
        protected abstract void InitOnce(); // 인스턴스 생성 후 최초 1회만 실행, OnSceneChanged에서 실행
        protected abstract void InitOnceAfterPreLoad(); // 인스턴스 생성 후, 초기 리소스 준비 여부 확인하고 최초 1회만 실행, OnSceneChanged에서 실행
        protected abstract void Init(); // 인스턴스 생성 및 씬 로드 직후마다 실행
        protected abstract void InitAfterPreLoad(); // 인스턴스 생성 및 씬 로드 직후마다, 초기 리소스 준비 여부 확인하고 실행

        // 씬 이동마다 필요한 정리 작업
        // 오버라이드해서 사용 및 base.Clear() 호출 필요
        protected virtual UniTask Clear() 
        {
            Logg.Log($"[{GetType().Name}] Clear() invoked", Logg.LoggingMode.Completed);
            isInitialized = false; // 플래그 초기화
            return UniTask.CompletedTask;
        }
        
        // Awake()에서 GameSceneManager에 초기화 작업을 델리게이트로 전달
        // 씬 로드가 완료된 후 싱글톤 초기화가 진행됨
        protected virtual void OnSceneChanged(Scene scene)
        {
            Logg.Log($"[{GetType().Name}] OnSceneChanged invoked in scene '{scene.name}'",Logg.LoggingMode.InProgress);
            
            if (!hasInitializedOnce) // 인스턴스 생성 후 최초 1회만 초기화가 필요한 작업 처리
            {
                Logg.Log($"[{typeof(T).Name}] InitOnce invoked in scene '{scene.name}'", Logg.LoggingMode.Completed);
                InitOnce();
                
                if (_instance is not Singleton<ResourceManager>) // 본인이 ResourceManager면 실행x
                    ResourceManager.Instance.WaitForPreLoadOnlyOnce(() =>
                    {
                        Logg.Log($"[{typeof(T).Name}] InitOnceAfterPreLoad()", Logg.LoggingMode.Completed);
                        InitOnceAfterPreLoad();
                    });

                hasInitializedOnce = true;
            }

            if (!isInitialized)
            {
                Logg.Log($"[{GetType().Name}] Init() in scene '{scene.name}'", Logg.LoggingMode.InProgress);
                Init();

                if (_instance is not Singleton<ResourceManager>) // 본인이 ResourceManager면 실행x
                    ResourceManager.Instance.WaitForPreLoad(InitAfterPreLoad);

                RunReservedOperations();
                isInitialized = true;
            }
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
            // if (singleton is Singleton<TH.SceneManagement.GameSceneManager>) return;
            
            if (singleton.IsInvalidInstance())
            {
                Logg.Log($"[{typeof(T).Name}] fail occured while {{nameof(ReserveOperation)}}. Instance is not valid", Logg.LoggingMode.Completed);
                return;
            }

            if (singleton.isInitialized)
            {
                Logg.Log($"[{typeof(T).Name}] trying to do reserved action: {action.Target}", Logg.LoggingMode.Completed);
                action?.Invoke();
            }
            else
            {
                singleton.reservedOperations.Enqueue(action);
            }
        }
        
        protected virtual void OnDestroy()
        {
            Logg.Log($"[{typeof(T).Name}] OnDestroy Stack: {Environment.StackTrace}", Logg.LoggingMode.Completed);
        }
    }
}

