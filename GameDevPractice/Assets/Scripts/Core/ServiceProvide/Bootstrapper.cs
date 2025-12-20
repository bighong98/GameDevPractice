using System;
using TH.Combat;
using TH.Item;
using TH.Resource;
using TH.SaveLoad;
using TH.SceneManagement;
using TH.UI;
using TH.Utils;
using UnityEngine;
using TH.Item.Storage;
using Cysharp.Threading.Tasks;

namespace TH.Core.Service
{
    // 전역 부트스트래퍼
    // IServiceProvider에 등록하여 사용할 시스템 인스턴스를 생성
    // 추후 Scene 개별 ServiceProvider 도입 시 확장 및 수정 필요
    public static class Bootstrapper
    {
        // RuntimeInitializeOnLoadMethod()로 씬 로드 전 실행을 보장 + 메인 스레드 실행 보장
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static async void Init()
        {
            try
            {
                RegisterServices();
                await InitializeSingletons();
                await InitializeAsync();
            }
            catch (Exception e) {Debug.LogError(e);}
        }
        
        // 씬 로드 전 초기화가 필요한 서비스 등록
        // 등록 시 반드시 인터페이스 타입으로 등록할 것
        // MonoBehaviour 상속 클래스는 자신의 Awake()에서 개별 등록 필요 (인터페이스 사용 제약은 동일)
        // 1개 이상의 파라미터를 갖는 생성자는 지연 생성됨 (-> 즉시 초기화가 필요할 경우 추가 호출 필요)
        private static void RegisterServices()
        {
            ServiceLocator.Register<IResourceLoader>(new ResourceLoader());
            ServiceLocator.Register<ISceneLoader>( sp => 
                new SceneLoader(sp.Get<IResourceLoader>()));
            ServiceLocator.Register<ISaveSystem>(sp => 
                new SaveSystem(
                    sp.Get<ISceneLoader>(),
                    sp.Get<IResourceLoader>()));
            ServiceLocator.Register<IRaycastHandler>( sp =>
                new RaycastHandler(sp.Get<ISceneLoader>()));
            ServiceLocator.Register<IDamageCalculator>(new DamageCalculator());
            ServiceLocator.Register<ICombatSystem>( sp => 
                new CombatSystem(
                    sp.Get<IResourceLoader>(),
                    sp.Get<IDamageCalculator>()));
            ServiceLocator.Register<IPlayerStorage>(sp => 
                new PlayerStorage(
                    sp.Get<IResourceLoader>(),
                    sp.Get<ISaveSystem>()));
            ServiceLocator.Register<IQuickStorage>(new PlayerQuickStorage());
            ServiceLocator.Register<IGameItemTransfer>(new GameItemTransfer());
            ServiceLocator.Register<IGameItemConsumer>(new GameItemConsumer());
            ServiceLocator.Register<IFloatingTextSpawner>(sp => 
                new FloatingTextSpawner(sp.Get<IResourceLoader>()));
            ServiceLocator.Register<IPlayerHolder>(new PlayerHolder());
        }

        private static async UniTask InitializeAsync()
        {
            // SceneLoader 인스턴스 생성 지연 방지
            var sceneLoader = ServiceLocator.Get<ISceneLoader>();
            // 첫 씬은 무조건 로딩 씬으로 강제
            await sceneLoader.LoadLoadingSceneAsync(); 
            // 리소스 일괄 로드 시작
            await ServiceLocator.Get<IResourceLoader>().PreLoadAsync();
        }

        private static async UniTask InitializeSingletons()
        {
            // 싱글톤 인스턴스 초기화
            // 메인 쓰레드 환경 보장 (RuntimeInitializeOnLoadMethod으로 보장되나 추후 기능 확장을 고려해 미리 방어)
            await UniTask.SwitchToMainThread();
            _ = ResourceManager.Instance;
            _ = PoolManager.Instance;
            _ = UIManager.Instance;
            _ = InputManager.Instance;
            _ = SoundManager.Instance;
            _ = GameSceneManager.Instance;
        }
    }
}

