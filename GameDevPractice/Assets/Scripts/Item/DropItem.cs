using System;
using TH.Core.Service;
using UnityEngine;
using TH.Item;
using GameDevTV.Utils;

namespace RPG.Item
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(ItemTypeHolder))]
    public class DropItem : MonoBehaviour
    {
        [SerializeField] private bool useImmediately;
        private ItemTypeHolder itemTypeHolder;
        private LazyValue<IInventorySystem> inventory;
        
        private void Awake()
        {
            if (itemTypeHolder == null)
            {
                itemTypeHolder = GetComponent<ItemTypeHolder>();
            }

            inventory = new LazyValue<IInventorySystem>(ServiceLocator.Get<IInventorySystem>);
        }
        
        public void Set(IInventorySystem inventorySystem) // 추후 오브젝트 풀링 적용시 아이템을 생성한 쪽에서 inventory의 참조를 전달
        {
            inventory.value = inventorySystem;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                //todo: 아이템 습득 애니메이션 추가
                if (itemTypeHolder == null || inventory == null) return;
                if (inventory.value.AddItem(new Item(itemTypeHolder.type), 
                        itemTypeHolder.GetAmount, 
                        checkInstanceType: true, 
                        useImmediately) <= 0) // AddItem()은 인벤토리 아이템 추가 시도 후 잔량을 반환, 잔량이 0이라면
                {
                    itemTypeHolder.ReleaseSelf(); // 현재 드랍 아이템 객체 풀에 반환
                }
                else
                {
                    Util.Log($"[{gameObject.name}.{nameof(DropItem)}] failed to pick up DropItem");
                }
                //todo: else { // 아이템 도로 뱉는? 애니메이션 추가 }
            }
        }
    }
}

