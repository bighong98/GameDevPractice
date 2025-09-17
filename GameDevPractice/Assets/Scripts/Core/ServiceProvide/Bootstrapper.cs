using TH.Combat;
using UnityEngine;

namespace TH.Core.Service
{
    public static class Bootstrapper
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            ServiceLocator.Register<IDamageCalculator>(new DamageCalculator());
        }
    }
}

