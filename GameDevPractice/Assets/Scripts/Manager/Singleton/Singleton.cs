using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            // if (Util.IsQuitting)
            //     return null;
            
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
            Util.Log($"{typeof(T).Name}: 중복 인스턴스가 존재하여 파괴됩니다.");
            Destroy(gameObject);
        }
    }

    protected virtual void Start()
    {
        if (IsInvalidInstance()) return; // 중복 인스턴스일 경우 실행x
        
        // GameSceneManager.Instance.notifySceneLoaded += AfterSceneLoaded;
        if (_instance is Singleton<GameSceneManager>) return; // 자기 자신이 GameSceneManager일 경우 실행x
        GameSceneManager.Instance.RegisterInitializationTask(AfterSceneLoaded);
    }
    
    // protected abstract void OnSceneLoaded(); // -> Init()으로 대체
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
        if (!isSceneLoadCompleted) return;

        if (!hasInitializedOnce) // 인스턴스 생성 후 최초 1회만 초기화가 필요한 작업 처리
        {
            InitOnce();
            
            if (_instance is not Singleton<ResourceManager>) // 본인이 ResourceManager면 실행x
                ResourceManager.Instance.SubscribePreLoadOnlyOnce(InitOnceAfterPreLoad);

            hasInitializedOnce = true;
        }

        if (!isInitialized) // 씬 이동마다 초기화가 필요한 작업 처리
        {
            Init();
            
            if (_instance is not Singleton<ResourceManager>) // 본인이 ResourceManager면 실행x
                ResourceManager.Instance.SubscribePreLoad(InitAfterPreLoad);

            if (_instance is not Singleton<GameSceneManager>) // 본인이 GameSceneManager면 실행x
                GameSceneManager.Instance.RegisterCleanupTask(Clear);
            
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
        isInitialized = true;
        while (reservedOperations.Count > 0)
        {
            try
            {
                reservedOperations.Dequeue()?.Invoke();
            }
            catch (Exception e)
            {
                Util.LogError($"[{typeof(T).Name}] failure occured while running reserved operations. {e}");
            }
        }
    }

    public void ReserveOperation(Action action)
    {
        if (_instance is not (Singleton<T> singleton and not Singleton<GameSceneManager>)) return; // 올바른 싱글톤 인스턴스가 아니거나, 자기자신이 Singleton<GameSceneManager> 타입인 경우 실행x
        
        if (singleton.IsInvalidInstance())
        {
            Util.Log($"[{typeof(T).Name}] fail occured while {{nameof(ReserveOperation)}}. Instance is not valid");
            return;
        }

        if (singleton.isInitialized)
        {
            action?.Invoke();
        }
        else
        {
            singleton.reservedOperations.Enqueue(action);
        }
    }
    
    protected virtual void OnDestroy()
    {
        if (Util.IsQuitting) return;
        if (_instance == this && _instance is not Singleton<GameSceneManager>)
        {
            // gameSceneManager.notifySceneLoaded -= AfterSceneLoaded;
            GameSceneManager.Instance.UnRegisterInitializationTask(AfterSceneLoaded);
        }
    }
}
