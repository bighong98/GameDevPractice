using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Item;
using TH.Utils;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "ItemTypeSO", menuName = "Scriptable Objects/Type/Item/ItemTypeSO")]
    public class ItemTypeSO : BaseTypeSO
    {
        [Header("Item Info")] 
        public Enums.ItemType itemType;
        public int maxAmount = 1; // default: 1
        public string desc;
        public bool isUsable => itemType == Enums.ItemType.Equipment || itemUseEffects.Count > 0; // Usable = Consumable(소비 가능) + Equipable(장착 가능)
        public List<ItemEffectBase> itemUseEffects;

        [SerializeField] protected AssetReferenceAudioClip itemUseSFXReference;

        [field: NonSerialized]public bool HasItemUseSfx { get; protected set; } = false;
        [field: NonSerialized]public AudioClip ItemUseSfx { get; protected set; }

        public override async UniTask InitializeAsync(CancellationToken token = default)
        {
            await base.InitializeAsync(token);
            ItemUseSfx = await GetStateFromAssetReference(itemUseSFXReference, token);
            Logg.Log($"[{GetType().Name}, {nameString}] InitializeAsync() - HasItemUseSFX: {HasItemUseSfx}, ItemUseSFX: {ItemUseSfx}", Logg.LoggingMode.Completed); 
        }

        public override void RefreshStates()
        {
            base.RefreshStates();
            HasItemUseSfx = ItemUseSfx.IsAlive() || IsAssetRefAssigned(itemUseSFXReference);
            Logg.Log($"[{GetType().Name}, {nameString}] RefreshStates() - HasItemUseSFX: {HasItemUseSfx}", Logg.LoggingMode.Completed);
        }
    }
}

