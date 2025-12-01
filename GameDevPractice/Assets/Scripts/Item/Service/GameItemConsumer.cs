using UnityEngine;

namespace TH.Item
{
    public interface IGameItemConsumer
    {
        bool TryConsume(
            IGameItemStorage storage,
            IGameItemSlot slot,
            object user = null,
            int amount = 1);
    }

    public class GameItemConsumer : IGameItemConsumer
    {
        public bool TryConsume(
            IGameItemStorage storage,
            IGameItemSlot slot,
            object user = null,
            int amount = 1)
        {
            // 1) 슬롯 / 아이템 유효성 검사
            if (slot is not { IsAccessible: true, HasItem: true, GetItem: { } item, GetItemInfo: { } info })
                return false;
            if (!info.isUsable)
                return false;

            // 2) 컨텍스트 구성
            int useAmount = Mathf.Max(1, amount);
            var ctx = new ItemUseContext(user, item, storage, slot, useAmount);

            // 3) 연결된 Effect들 실행
            var effects = info.itemUseEffects;
            if (effects == null || effects.Count == 0)
            {
                // 데이터 누락: 로그만 찍고 실패 반환하거나, 그냥 true로 보고 소비만 할지 선택
                Debug.LogWarning($"[GameItemConsumer] {info.name} has no effects.");
                return false;
            }

            foreach (var effect in effects)
            {
                if (effect == null) continue;
                if (!effect.TryApply(ctx))
                {
                    // 하나라도 실패하면 전체 실패로 볼지,
                    // 일부만 성공하도록 둘지는 디자인 선택.
                    return false;
                }
            }

            // 4) 효과 적용 성공 → 실제 소비 처리
            // ConsumeFromStorage(storage, slot, item, useAmount);
            (storage as IConsumableItemStorage)?.TryConsume(slot, useAmount);
            return true;
        }

        private void ConsumeFromStorage(
            IGameItemStorage storage,
            IGameItemSlot slot,
            IGameItem item,
            int amount)
        {
            // Countable이면 개수 감소
            if (item is ICountableItem cItem)
            {
                int current = cItem.GetAmount;
                int consume = Mathf.Clamp(amount, 1, current);
                cItem.SetAmount(current - consume);

                // 0개 이하면 슬롯에서 제거 (스택 소진)
                if (cItem.GetAmount <= 0)
                {
                    storage.TryRemoveItem(slot.Index);
                }
                else
                {
                    // 수량만 바뀌었으니 슬롯 변경 이벤트는 storage 쪽에서 이미 핸들링하거나,
                    // 여기서 따로 Notify를 해줄지 설계에 따라 결정
                }
            }
            else
            {
                // 비-Countable 일회용 아이템인 경우 → 그냥 슬롯에서 제거
                storage.TryRemoveItem(slot.Index);
            }
        }
    }
}
