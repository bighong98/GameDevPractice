using System;
using TH.Core.Service;
using TH.Item;
using UnityEngine;
using TH.Item.Storage;

namespace TH.Control
{
    [RequireComponent(typeof(Collider))]
    public class PlayerInteractor : MonoBehaviour
    {
        private IPlayerStorage playerStorage;
        private IGameItemTransfer itemTransfer;
        private readonly IItemBuilder itemBuilder = new ItemBuilder();

        [SerializeField] private LayerMask interactableMask;

        private void Awake()
        {
            playerStorage = ServiceLocator.Get<IPlayerStorage>();
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
                    StoreDropItem(dropItem);
                    break;
                //todo: 필요한 상호작용 추가
                default:
                    break;
            }
        }

        private void StoreDropItem(IDropItem dropItem)
        {
            if (itemBuilder.GetItemFromData(dropItem.ItemData, dropItem.Amount) is not { } item) return;
            if ((dropItem.UseImmediately && playerStorage.TryStoreAndUse(item))
                || (!dropItem.UseImmediately && playerStorage.TryStore(item)))
            {
                dropItem.Interact();
            }
        }
    }
}

