using RPG.Item;
using TH.Combat;
using TH.Item;
using TH.Resource;
using UnityEngine;

namespace TH.Core.Service
{
    // 전역 부트스트랩
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
            ServiceLocator.Register<IDamageCalculator>(new DamageCalculator());
            ServiceLocator.Register<IResourceLoader>(new ResourceLoader());
        }
    }
}

