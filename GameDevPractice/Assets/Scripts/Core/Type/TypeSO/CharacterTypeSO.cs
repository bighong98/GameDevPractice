using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using System.Collections.Generic;
using UnityEngine;
using TH.Stats;
using UnityEngine.Serialization;
using TH.Attribute.Stat;

namespace TH.Resource
{
    public abstract class CharacterTypeSO : BaseTypeSO
    {
        [Header("Character")] 
        public CharacterType characterType;
        public BaseStatListSO characterBaseStats;
        public float height;
        public int startingLevel;

        [SerializeField] public List<EquipmentTypeSO> defaultEquipments;
        [NonSerialized] private GameObject hpBarPrefab;
        [SerializeField] private AssetReferenceGameObject hpBarPrefabReference;
        [NonSerialized] private bool initialized;

        public GameObject HpBarPrefab => hpBarPrefab;

        public override async UniTask InitializeAsync(CancellationToken token = default)
        {
            await base.InitializeAsync(token);

            if (initialized)
            {
                return;
            }

            if (hpBarPrefabReference != null && hpBarPrefabReference.RuntimeKeyIsValid())
            {
                var loadedHpBarPrefab = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(hpBarPrefabReference, token);
                if (loadedHpBarPrefab != null)
                {
                    hpBarPrefab = loadedHpBarPrefab;
                }
            }

            initialized = true;
        }
    }
}

