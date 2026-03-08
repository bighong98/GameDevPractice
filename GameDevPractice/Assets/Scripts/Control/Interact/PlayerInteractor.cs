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
            TryHandleInteraction(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryHandleInteraction(other);
        }

        private void TryHandleInteraction(Collider other)
        {
            if (other == null)
                return;

            if (other.TryGetComponent(out IInteractable interactable))
                HandleInteraction(interactable);
        }

        private void HandleInteraction(IInteractable interact)
        {
            switch (interact)
            {
                case IDropItem dropItem:
                    StoreDropItem(dropItem);
                    break;
                // todo: add more interactable cases
                default:
                    break;
            }
        }

        private void StoreDropItem(IDropItem dropItem)
        {
            if (dropItem == null || dropItem.ItemData == null)
                return;

            if (itemBuilder.GetItemFromData(dropItem.ItemData, dropItem.Amount) is not { } item)
                return;

            bool stored = false;
            if (dropItem.UseImmediately)
            {
                stored = playerStorage.TryStoreAndUse(item);
                if (!stored)
                    stored = playerStorage.TryStore(item);
            }
            else
            {
                stored = playerStorage.TryStore(item);
            }

            if (stored)
                dropItem.Interact();
        }
    }
}

