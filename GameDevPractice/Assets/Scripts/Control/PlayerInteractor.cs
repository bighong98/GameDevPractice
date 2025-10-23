using System;
using TH.Control;
using TH.Core.Service;
using TH.Item;
using TH.Utils;
using UnityEngine;

namespace TH.Control
{
    [RequireComponent(typeof(Collider))]
    public class PlayerInteractor : MonoBehaviour
    {
        private IEquipmentHolder playerEquip;
        private IPlayerInventory playerStorage;
        private IItemUsageHandler itemUsageHandler;
        private readonly IItemBuilder itemBuilder = new ItemBuilder();

        [SerializeField] private LayerMask interactableMask;

        private void Awake()
        {
            transform.parent.TryGetComponent(out playerEquip);
            playerStorage = ServiceLocator.Require<IPlayerInventory>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out IInteractable i))
            {
                HandleInteraction(i);
            }
        }

        private void HandleInteraction(IInteractable interact)
        {
            switch (interact)
            {
                case IDropItem dropItem:
                    if (dropItem.UseImmediately)
                    {
                        if (dropItem.ItemData is EquipmentTypeSO equipmentData)
                        {
                            TryEquip(dropItem, equipmentData);
                        }
                        // todo: 장비 외 즉시 사용 설정된 아이템 사용 처리
                    }
                    else if (playerStorage.TryStore(itemBuilder.GetItemFromData(dropItem.ItemData, dropItem.Amount)))
                    {
                        dropItem.Interact();
                    }
                    break;
            
                default:
                    break;
            }
        }

        private void TryEquip(IDropItem dropItem, EquipmentTypeSO equipmentData)
        {
            if (playerEquip == null)
            {
                Logg.LogError($"[PlayerInteractor] player's EquipmentHolder reference is missing");
                return;
            }
            
            if (playerEquip.TryStore(itemBuilder.GetItemFromData(equipmentData)))
            {
                dropItem.Interact();
            }
        }

        #region deprecated

        // private Transform eye => transform;
        // private const float radius = 2.5f;
        // private readonly Collider[] _hits = new Collider[32];

        // private void Update()
        // {
        //     if (Physics.OverlapSphereNonAlloc(
        //             transform.position, radius, _hits, interactableMask,
        //             QueryTriggerInteraction.Collide) is not ({ } count and > 0)) return;
        //
        //     for (int i = 0; i < count; i++)
        //     {
        //         var col = _hits[i];
        //         if (col == null || !col.gameObject.activeSelf | !col.TryGetComponent(out IInteractable it)) continue;
        //
        //         HandleInteraction(it);
        //     }
        // }

        #endregion
    }
}

