using TH.Resource;

namespace TH.Item
{
    // 소비형 아이템 사용 규칙 위임 partial
    public sealed partial class PlayerStorage
    {
        #region IConsumableItemStorage

        // 슬롯 참조 기반 소비 요청 위임
        public bool TryConsume(IGameItemSlot slot, int amount)
            => consumableService.TryConsume(slot, amount);

        // 슬롯 인덱스 기반 소비 요청 위임
        public bool TryConsume(int index, int amount)
            => consumableService.TryConsume(index, amount);

        // 아이템 타입 기반 소비 요청 위임
        public bool TryConsume(ItemTypeSO itemData, int amount)
            => consumableService.TryConsume(itemData, amount);

        #endregion
    }
}
