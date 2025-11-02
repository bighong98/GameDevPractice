using TH.Combat;
using TH.Item;
using TH.Resource;
using TH.SaveLoad;
using TH.SceneManagement;
using TH.UI;
using TH.Utils;
using UnityEngine;
using TH.Item.Storage;

namespace TH.Core.Service
{
    // 전역 부트스트래퍼
    // IServiceProvider에 등록하여 사용할 시스템 인스턴스를 생성
    // 추후 Scene 개별 ServiceProvider 도입 시 확장 및 수정 필요
    public static class Bootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            RegisterServices();
        }

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
            ServiceLocator.Register<IGameItemTransfer>(new GameItemTransfer());
            ServiceLocator.Register<IFloatingTextSpawner>(sp => 
                new FloatingTextSpawner(sp.Get<IResourceLoader>()));
        }
    }
}

