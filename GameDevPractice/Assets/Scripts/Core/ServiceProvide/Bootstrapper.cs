using TH.Combat;
using TH.Item;
using TH.Resource;
using TH.SaveLoad;
using TH.SceneManagement;
using TH.UI;
using TH.Utils;
using UnityEngine;

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
            ServiceLocator.Register<ISceneLoader>(new SceneLoader());
            ServiceLocator.Register<ISaveSystem>(new SaveSystem());
            ServiceLocator.Register<IRaycastHandler>(new RaycastHandler());
            ServiceLocator.Register<IDamageCalculator>(new DamageCalculator());
            ServiceLocator.Register<ICombatSystem>(new CombatSystem());
            ServiceLocator.Register<IPlayerStorage>(new PlayerStorage());
            ServiceLocator.Register<IGameItemTransfer>(new GameItemTransfer());
            ServiceLocator.Register<IFloatingTextSpawner>(new FloatingTextSpawner());
        }
    }
}

