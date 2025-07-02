using System;
using UnityEngine;

namespace RPG.Item
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class DropItem : MonoBehaviour
    {
        [SerializeField] private ItemTypeHolder itemTypeHolder;
        [SerializeField] private InventorySystem inventory; // serialize for debug
        [SerializeField] private bool useImmediately;
        
        private void Awake()
        {
            if (GetComponent<Collider>() is {} coll)
            {
                coll.isTrigger = true;
            }

            if (GetComponent<Rigidbody>() is { } rigid)
            {
                rigid.useGravity = false;
                rigid.isKinematic = true;
            }
        }

        private void Start()
        {
            itemTypeHolder = GetComponent<ItemTypeHolder>();
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<InventorySystem>();
            }
        }

        public void SetInventoryRef(InventorySystem inventorySystem)
        {
            inventory = inventorySystem;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                //todo: 아이템 습득 애니메이션 추가
                if (itemTypeHolder == null || inventory == null) return;
                if (inventory.AddItem(new Item(itemTypeHolder.type), itemTypeHolder.GetAmount, true, useImmediately) <= 0)
                {
                    itemTypeHolder.ReleaseSelf();
                }
                //todo: else { // 아이템 도로 뱉는? 애니메이션 추가 }
            }
        }
    }
}

