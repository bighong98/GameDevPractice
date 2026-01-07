using System;
using UnityEngine;
using TH.Item;
using TH.Core.Pool;
using TH.Utils;

namespace TH.Resource
{
    // 아이템의 데이터 SO와 개수를 포함하는 MonoBehaviour 클래스
    // 필드 드랍 아이템 등에 사용
    public class ItemTypeHolder : TypeHolder<ItemTypeSO>
    {
        [Header("Item")] [Tooltip("Only Countable Item type can have amount of more than one. Amount number larger than 1 will be ignored for other ItemType")]
        [SerializeField] private int amount;
        [SerializeField] private bool toDropItem; // 드랍 아이템으로 생성할지 여부
        public int GetAmount => amount;

        public override void OnGetFromPool()
        {
            base.OnGetFromPool();
            if (toDropItem)
                MakeDropItem();
        }

        public TH.Control.DropItem MakeDropItem(int itemAmount = 0)
        {
            var dropItem = gameObject.GetOrAddComponent<TH.Control.DropItem>();
            if (itemAmount > 0)
                amount = itemAmount;
            return dropItem;
        }
    }
}

