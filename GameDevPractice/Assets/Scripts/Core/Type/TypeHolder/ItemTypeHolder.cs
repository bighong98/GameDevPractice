using UnityEngine;

namespace TH.Resource
{
    // 아이템의 데이터 SO와 개수를 포함하는 MonoBehaviour 클래스
    // 필드 드랍 아이템 등에 사용
    public class ItemTypeHolder : TypeHolder<ItemTypeSO>, IRuntimeTypeInjectable<ItemTypeSO>
    {
        [Header("Item")] [Tooltip("Only Countable Item type can have amount of more than one. Amount number larger than 1 will be ignored for other ItemType")]
        [SerializeField] private int amount;
        [SerializeField] private bool allowRuntimeTypeInjection = false;

        public int GetAmount => amount;
        public bool AllowRuntimeTypeInjection => allowRuntimeTypeInjection;

        protected override bool CanSkipTypeReferenceValidation => allowRuntimeTypeInjection;

        public bool TryForceInjectType(ItemTypeSO runtimeType, bool notifyDependents = true)
        {
            if (!allowRuntimeTypeInjection)
                return false;

            return TryForceInjectTypeInternal(runtimeType, notifyDependents);
        }

        public void SetAmount(int itemAmount)
        {
            if (itemAmount > 0)
                amount = itemAmount;
        }
    }
}

