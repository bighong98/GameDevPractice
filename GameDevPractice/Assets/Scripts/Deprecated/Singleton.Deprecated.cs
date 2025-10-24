using System;
using System.Collections.Generic;
using UnityEngine;
using TH.Core.Service;
using TH.SceneManagement;
using TH.Resource;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using TH.Utils;

namespace TH.Deprecated
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
            if (IsInvalidInstance()) return; // 중복 인스턴스일 경우 실행x
            if (Util.IsQuitting) return;
            // if (_instance is Singleton<TH.SceneManagement.GameSceneManager>) return; // 자기 자신이 GameSceneManager일 경우 실행x
            //
            // TH.SceneManagement.GameSceneManager.Instance.RegisterInitializationTask(AfterSceneLoaded);

            if (ServiceLocator.TryGet(out ISceneLoader sceneLoader))
            {
                sceneLoader.OnSceneChanged += this.OnSceneChanged;
            }
        }
        
        protected abstract void InitOnce(); // 인스턴스 생성 후 최초 1회만 실행
        protected abstract void InitOnceAfterPreLoad(bool isLoadCompleted); // 인스턴스 생성 후, 초기 리소스 준비 여부 확인하고 최초 1회만 실행
        protected abstract void Init(); // 인스턴스 생성 및 씬 로드 직후마다 실행
        protected abstract void InitAfterPreLoad(bool isLoadCompleted); // 인스턴스 생성 및 씬 로드 직후마다, 초기 리소스 준비 여부 확인하고 실행

        // 씬 이동마다 필요한 정리 작업
        // 오버라이드해서 사용 및 base.Clear() 호출 필요
        protected virtual UniTask Clear() 
        {
            isInitialized = false; // 플래그 초기화
            return UniTask.CompletedTask;
        }
        
        // Start()에서 GameSceneManager에 초기화 작업을 델리게이트로 전달
        // 씬 로드가 완료된 후 싱글톤 초기화가 진행됨
        private void AfterSceneLoaded(bool isSceneLoadCompleted)
        {
            Logg.Log($"[{typeof(T).Name}] AfterSceneLoaded()", Logg.LoggingMode.Completed);
            if (!isSceneLoadCompleted) return;

            if (!hasInitializedOnce) // 인스턴스 생성 후 최초 1회만 초기화가 필요한 작업 처리
            {
                Logg.Log($"[{typeof(T).Name}] InitOnce()", Logg.LoggingMode.Completed);
                InitOnce();
            
                if (_instance is not Singleton<ResourceManager>) // 본인이 ResourceManager면 실행x
                    ResourceManager.Instance.WaitForPreLoadOnlyOnce((t) =>
                    {
                        Logg.Log($"[{typeof(T).Name}] InitOnceAfterPreLoad()", Logg.LoggingMode.Completed);
                        InitOnceAfterPreLoad(t);
                    });

                hasInitializedOnce = true;
            }

            if (!isInitialized) // 씬 이동마다 초기화가 필요한 작업 처리
            {
                Logg.Log($"[{GetType().Name}] Init()", Logg.LoggingMode.InProgress);
                Init();
            
                if (_instance is not Singleton<ResourceManager>) // 본인이 ResourceManager면 실행x
                    ResourceManager.Instance.WaitForPreLoad(InitAfterPreLoad);

                if (_instance is not Singleton<GameSceneManager>) // 본인이 GameSceneManager면 실행x
                    GameSceneManager.Instance.RegisterCleanupTask(Clear);
            
                RunReservedOperations();
                isInitialized = true;
            }
        }
        
        protected virtual void OnSceneChanged(Scene scene)
        {
            Logg.Log($"[{GetType().Name}] OnSceneChanged invoked");
            Clear();
        }
        
        protected bool IsInvalidInstance()
        {
            return _instance != this;
        }
        
        private void RunReservedOperations()
        {
            isInitialized = true;
            while (reservedOperations.Count > 0)
            {
                try
                {
                    reservedOperations.Dequeue()?.Invoke();
                }
                catch (Exception e)
                {
                    Logg.LogError($"[{typeof(T).Name}] failure occured while running reserved operations. {e}");
                }
            }
        }
        
        public void ReserveOperation(Action action)
        {
            if (_instance is not Singleton<T> singleton) return;
            if (singleton is Singleton<GameSceneManager>) return;
        
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
            // Logg.Log($"[{typeof(T).Name}] OnDestroy Stack: {Environment.StackTrace}");
            if (Util.IsQuitting) return;
            if (_instance == this && _instance is not Singleton<GameSceneManager>)
            {
                GameSceneManager.Instance.UnRegisterInitializationTask(AfterSceneLoaded);
            }
        }
    }
}



