using System;
using UnityEngine;

namespace RPG.Item
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class DropItem : MonoBehaviour
    {
        [SerializeField] private ItemTypeHolder itemTypeHolder;
        [SerializeField] private bool useImmediately;
        private InventorySystem inventory;
        
        private void Awake()
        {
            itemTypeHolder = GetComponent<ItemTypeHolder>();
        }

        private void OnEnable()
        {
            if (inventory == null)
            {
                inventory = FindFirstObjectByType<InventorySystem>();
            }
        }

        public void SetInventoryRef(InventorySystem inventorySystem) // 추후 오브젝트 풀링 적용시 아이템을 생성한 쪽에서 inventory의 참조를 전달
        {
            inventory = inventorySystem;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                //todo: 아이템 습득 애니메이션 추가
                if (itemTypeHolder == null || inventory == null) return;
                if (inventory.AddItem(new Item(itemTypeHolder.type), itemTypeHolder.GetAmount, true, useImmediately) <= 0) // 플레이어 인벤토리 아이템 추가에 성공했다면 
                {
                    itemTypeHolder.ReleaseSelf(); // 현재 드랍 아이템 객체 풀에 반환
                }
                //todo: else { // 아이템 도로 뱉는? 애니메이션 추가 }
            }
        }
    }
}

