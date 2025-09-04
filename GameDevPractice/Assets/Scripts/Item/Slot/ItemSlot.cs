using UnityEngine;

namespace RPG.Item
{
    public class ItemSlot
    {
        [SerializeField] protected Item Item;
        public int Index = -1; // Default: -1 (not initialized)

        protected bool Accessible; // 슬롯 및 슬롯 내부 아이템 접근 가능 여부
        protected bool Visible; // 슬롯 가시화 여부 (false일 경우 해당 슬롯UI가 비활성화)
        protected Enums.ItemType[] ValidItemTypes;
        
        // 생성자 (Index는 따로 설정할 것)
        public ItemSlot(Item item, int index = -1, Enums.ItemType[] validTypes = null, bool accessible = true, bool visible = true)
        {
            this.Item = item;
            this.Index = index;
            this.ValidItemTypes = validTypes;
            this.Accessible = accessible;
            this.Visible = visible;
        }

        public Item GetItem => this.Item;
        public ItemTypeSO GetItemInfo => Item?.GetItemInfo;
        public int GetAmount => Item?.GetAmount ?? 0;
        public bool IsAccessible => Accessible;
        public bool IsValid => Item != null && Index >= 0; // 아이템 데이터가 존재하고, Index 초기화가 된 경우
        public bool HasItem => this.Item is { GetAmount: > 0 };
        
        public void SetAccessibility(bool state)
        {
            Accessible = state;
        }

        public void SetVisibility(bool state)
        {
            Visible = state;
        }
        
        public virtual bool CanStore(ItemTypeSO itemData) // 슬롯에 저장 가능한 아이템 타입 확인
        {
            if (!Accessible) return false; // 접근 제한된 슬롯이면 실패처리
            if (ValidItemTypes == null || ValidItemTypes.Length == 0) return false; // 유효 아이템 타입이 없거나 타입 배열이 초기화되지 않았다면 실패처리
            
            var type = itemData.itemType;
            foreach (var expectedType in ValidItemTypes)
            {
                if (type == expectedType) return true;
            }
            
            return false;
        }

        public bool Store(Item item, out Item prevItem, bool byForce = false) // 새 아이템을 슬롯에 저장, 슬롯에 저장되어있던 아이템이 있다면 prev 아이템을 통해 배출
        {
            // if (!CanStore(item.GetItemInfo))
            // {
            //     prevItem = null; // 저장에 실패했으므로 내부 아이템 반환 x
            //     return false; // 저장 실패 반환
            // }
            //
            // prevItem = this.Item; // 기존 슬롯 내부 아이템 인스턴스 out 키워드로 반환
            // this.Item = item; // 슬롯 내부에 새 아이템 인스턴스 저장
            // return true; // 저장 성공 반환

            prevItem = this.Item;
            return Store(item, byForce);
        }

        public virtual bool Store(Item item, bool byForce = false) // 새 아이템을 슬롯에 저장
        {
            if (!byForce && !CanStore(item.GetItemInfo)) return false;

            this.Item = item;
            return true;
        }

        public virtual bool Clear() // 슬롯에 있는 아이템 제거
        {
            this.Item = null;
            return true;
        }
    }
}

