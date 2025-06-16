// using System;
// using System.Collections;
// using System.Collections.Generic;
// using Redcode.Pools;
// using UnityEngine;
// using Object = System.Object;
//
// public class DropItemController : DropController
// {
//     public Collider2D Coll { get; set; }
//     [SerializeReference] public BaseItem item;
//     // public int poolIndex;
//     
//     public override bool Init()
//     {
//         base.Init();
//         Coll = Util.GetOrAddComponent<CircleCollider2D>(gameObject);
//         Coll.isTrigger = true;
//         gameObject.layer = LayerMask.NameToLayer("Drop");
//         
//         return true;
//     }
//
//     private void Awake()
//     {
//         Init();
//     }
//
//     public DropItemController SetItem(BaseItem item) // 게임에서 처음으로 해당 종류 아이템을 생성한 경우에만 사용
//     {
//         if (item == null)
//         {
//             Util.Log("DropItemController: item is null");
//             return null;
//         }
//         // 아이템 데이터에 맞게 스프라이트, 콜라이더 설정
//         this.item = item;
//         Sprite.sprite = Managers.Resource.Load<Sprite>(item.DropSpriteName);
//         if (Coll is CircleCollider2D cirCleColl)
//             cirCleColl.radius = Mathf.Max(Sprite.bounds.size.x, Sprite.bounds.size.y)  / 2f;
//
//         return this;
//     }
//
//     private void OnTriggerEnter2D(Collider2D other)
//     {
//         if (other.CompareTag("Player"))
//         {
//             OnAcquired();
//         }
//     }
//
//     public override void OnAcquired()
//     {
//         base.OnAcquired();
//         InGameManager.Instance.inventorySystem.AddItem(item);
//         Despawn();
//     }
//
//     public DropItemController Spawn<T>(T newItemData) where T: BaseItem
//     {
//         if (item.ItemId != newItemData.ItemId)
//         {
//             item = newItemData;
//             Recycle();
//         }
//         else if ((item is CountableItem cItem) && (newItemData is CountableItem cItemNew))
//         {
//             cItem.SetAmount(cItemNew.Amount);
//         }
//
//         return this;
//     }
//
//     private void Recycle()
//     {
//         Sprite.sprite = Managers.Resource.Load<Sprite>(item.ItemName);
//     }
//     
//     private void Despawn()
//     {
//         Managers.Pool.ReturnPool(poolIndex, this);
//     }
// }
