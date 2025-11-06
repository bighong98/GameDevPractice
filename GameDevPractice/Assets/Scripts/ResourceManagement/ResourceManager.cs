using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using UnityEngine.AddressableAssets;
using Object = UnityEngine.Object;
using TH.Core;
using TH.Utils;

namespace TH.Resource
{
    // 리소스 로딩 관련 기능 접근을 위한 싱글톤 파사드(Facade)
    // ServiceLocator/Bootstrapper 이외 클래스에서는 ResourceManager 사용 권장 (직접 ServiceLocator.Get() x)
    public sealed class ResourceManager : Singleton<ResourceManager>
    {
        private IResourceLoader resourceLoader; // 실제 어드레서블 기반 비동 리소스 로딩 기능을 구현한 서비스 인스턴스
        
        // PreLoad
        public event Action NotifyPreLoad; // 초기 리소스 로딩 완료 이벤트 (from IResourceLoader)
        private readonly Queue<Action> reservedPreLoadTasks = new(); // 리소스 로드 완료 후 처리 필요한 콜백 큐
        
        private bool preLoadState = false;
        public bool PreLoadState => preLoadState;
        
        private const string PreLoadLabel = "PreLoad";

        protected override void Awake()
        {
            base.Awake();
            if (IsInvalidInstance()) return; // 현재 싱글톤 인스턴스가 유효하지 않을 경우 초기화 중단
            resourceLoader = ServiceLocator.Get<IResourceLoader>(); // 리소스로더 인스턴스 받아오기
        }

        #region Initialization (Singleton<T>)

        protected override void InitOnce() { }

        protected override void InitOnceAfterPreLoad() { }

        protected override void Init()
        {
            if (resourceLoader.IsLoadedAll(PreLoadLabel))
            {
                OnPreLoadDone();
            }
            else
            {
                resourceLoader.NotifyResourceLoad -= OnPreLoadDone; // 중복 델리게이트 누적 방지
                resourceLoader.NotifyResourceLoad += OnPreLoadDone;
            }
        }
        protected override void InitAfterPreLoad() { }

        #endregion

        protected override UniTask Clear()
        {
            if (resourceLoader != null)
                resourceLoader.NotifyResourceLoad -= OnPreLoadDone;
            return base.Clear();
        }

        #region PreLoad

        // 초기 리소스 로딩(Preload) 종료 후에 실행할 콜백 전달용 외부 메서드
        // 호출 시점에 로딩이 이미 끝났다면 즉시 콜백 실행
        public void WaitForPreLoad(Action callback)
        {
            if (preLoadState) callback?.Invoke();
            else NotifyPreLoad += callback;
        }

        // PreLoad 이후 한 번만 실행되길 원하는 콜백을 예약 (중복 초기화 방지)
        // 이미 끝났다면 즉시 콜백 실행, 아니면 큐에 보관했다가 완료 시점에 순차 실행
        public void WaitForPreLoadOnlyOnce(Action callback)
        {
            if (preLoadState) callback?.Invoke();
            else reservedPreLoadTasks.Enqueue(callback);
        }

        // 큐에 예약된 일회성 콜백들 실행
        private void RunReserved()
        {
            if (reservedPreLoadTasks.Count == 0) return;
            while (reservedPreLoadTasks.TryDequeue(out var task))
            {
                task?.Invoke();
            }
        }

        private void OnPreLoadDone(string label)
        {
            if (string.Equals(label, PreLoadLabel))
            {
                OnPreLoadDone();
            }
        }

        private void OnPreLoadDone()
        {
            preLoadState = true;
            NotifyPreLoad?.Invoke();
            RunReserved();
        }

        #endregion

        #region Load
        // 이미 어드레서블 로드가 완료된 리소스 참조 반환 (어드레서블 키 사용)
        public T Load<T>(string key) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(key, out var result))
            {
                return result;
            }

            return null;
        }
        // 이미 어드레서블 로드가 완료된 리소스 참조 반환 (어드레서블 키 사용)
        public bool TryLoad<T>(string key, out T resource) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(key, out var result))
            {
                resource = result;
                return true;
            }

            resource = null;
            return false;
        }
        
        // 이미 어드레서블 로드가 완료된 리소스 참조 반환 (AssetReference 사용)
        public bool TryLoad<T>(AssetReference assetRef, out T resource) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(assetRef, out var result))
            {
                resource = result;
                return true;
            }

            resource = null;
            return false;
        }

        // 필요한 리소스가 프리팹인 경우에 사용
        // 리소스 로드 후 즉시 Instantiate() 실행하여 결과 게임오브젝트 반환
        // 오브젝트 풀링 적용x
        public GameObject Instantiate(string key, Transform parent = null)
        {
            if (TryLoad<UnityEngine.Object>(key, out var result) && result is GameObject origin)
            {
                GameObject clone = Object.Instantiate(origin, parent);
                clone.name = origin.name;

                return clone;
            }
            
            Logg.LogError($"{nameof(ResourceManager)}.Instantiate: Failed to load prefab: {key}");
            return null;
        }
        // AssetReference기반 리소스 조회 (비동기 지원)
        // 캐시 미스(=사전 로딩이 되지 않았을 경우) 시 직접 비동기 로드 실행
        public async UniTask<T> ExtractAssetRefAsync<T>(AssetReference assetRef, CancellationToken token = default) where T : UnityEngine.Object
        {
            if (resourceLoader.TryLoad<T>(assetRef, out var result))
            {
                return result;
            }

            return await resourceLoader.LoadAsync<T>(assetRef, token);
        }
        
        #endregion
        
        public void Destroy(GameObject go)
        {
            if (go == null) return;
        
            Object.Destroy(go);
        }
    }
}

