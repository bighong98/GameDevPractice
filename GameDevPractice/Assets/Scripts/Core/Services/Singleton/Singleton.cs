using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.SceneManagement;

namespace TH.Core.Service
{
    // 제네릭 + 리플렉션 + Lazy 싱글톤
    // 상속 클래스는 반드시 [Preserve] 어트리뷰트 적용 필요 (IL2CPP 스트리핑 대응)
    // using UnityEngine.Scripting;
    // [Preserve]
    public abstract class Singleton<T> where T : class, ISingleton
    {
        // Lazy + ExecutionAndPublication 기반 지연 초기화 + 멀티 스레드 세이프 보장
        private static readonly Lazy<T> _instance = new Lazy<T>(CreateInstance, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        // 외부 접근 프로퍼티
        public static T Instance => _instance.Value;

        private static T CreateInstance()
        {
            // 리플렉션 기반 생성자 정보 생성
            var ctor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            
            if (ctor == null)
                throw new InvalidOperationException($"[Singleton] {typeof(T).Name} private 생성자가 없습니다.");
            
            // 생성자 실행 (인스턴스 생성 및 콜백 전달)
            var inst = (T)ctor.Invoke(null);
            GlobalSingletonRouter.Instance.Register(inst);
        
            return inst;
        }
    }
    
    public sealed class SingletonCallbackRouter
    {
        public SingletonCallbackRouter(ISceneLoader sceneLoader)
        {
            _sceneLoader = sceneLoader;
        }
        
        private readonly HashSet<ISingleton> _registered = new();
        private readonly ISceneLoader _sceneLoader;

        public void Register(ISingleton s)
        {
            if (!_registered.Add(s)) return; // 중복 방지
            _sceneLoader.OnBeforeSceneChanged += s.BeforeSceneLoad;
            _sceneLoader.OnAfterSceneChanged  += s.AfterSceneLoad;
        }
        
        public void Unregister(ISingleton s)
        {
            if (!_registered.Remove(s)) return;
            _sceneLoader.OnBeforeSceneChanged -= s.BeforeSceneLoad;
            _sceneLoader.OnAfterSceneChanged  -= s.AfterSceneLoad;
        }
    }
    
    public static class GlobalSingletonRouter
    {
        private static readonly Lazy<SingletonCallbackRouter> _instance =
            new(() => new SingletonCallbackRouter(ServiceLocator.Get<ISceneLoader>()));

        public static SingletonCallbackRouter Instance => _instance.Value;
    }

    public interface ISingleton
    {
        UniTask BeforeSceneLoad(CancellationToken externalToken);
        UniTask AfterSceneLoad(CancellationToken externalToken);
    }
}
