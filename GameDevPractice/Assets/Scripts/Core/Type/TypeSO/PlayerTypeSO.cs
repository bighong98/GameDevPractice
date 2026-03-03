using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "PlayerTypeSO", menuName = "Scriptable Objects/Type/Character/PlayerTypeSO")]
    public class PlayerTypeSO : CharacterTypeSO
    {
        [Header("Player")] 
        [NonSerialized] public GameObject levelUpEffect;
        [SerializeField] private AssetReferenceGameObject levelUpEffectReference;
        [NonSerialized] private bool initialized;

        public override async UniTask InitializeAsync(CancellationToken token = default)
        {
            await base.InitializeAsync(token);

            if (initialized)
            {
                return;
            }

            if (levelUpEffectReference != null && levelUpEffectReference.RuntimeKeyIsValid())
            {
                var loadedLevelUpEffect = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(levelUpEffectReference, token);
                if (loadedLevelUpEffect != null)
                {
                    levelUpEffect = loadedLevelUpEffect;
                }
            }

            initialized = true;
        }
    
    }
}

