using TH.Utils;

namespace TH.Item
{
    // 슬롯 접근/용량 조절 보조 유틸리티 partial
    public sealed partial class PlayerStorage
    {
        #region Slot Helper Methods

        // 인덱스 입력 기반 슬롯 변경 알림 래퍼
        private void NotifySlotChanged(int index)
        {
            if (!IsValidSlotIdx(index)) return;
            if (slots[index] is not { } slot) return;
            
            NotifySlotChanged(slot);
        }

        // 슬롯 입력 기반 변경 알림 발행 래퍼
        private void NotifySlotChanged(IGameItemSlot slot)
        {
            Logg.Log($"[PlayerStorage] NotifySlotChanged({slot} - {slot.Index})", Logg.LoggingMode.Completed);
            eventBatcher?.NotifySlotChanged(slot.Index);
        }

        // 유효 인덱스 슬롯 참조 조회
        private IGameItemSlot GetSlot(int index)
        {
            if (!IsValidSlotIdx(index)) return null;
            return slots[index];
        }

        // 시작 인덱스 이후 첫 빈 슬롯 탐색
        private bool FindEmptySlot(int start, out IGameItemSlot found)
        {
            int end = GetEndIdx;
            for (int i = start; i <= end; i++)
            {
                switch (slots[i])
                {
                    case null:
                        found = slots[i] = MakeEmptySlot(index: i); // Create slot instance if needed
                        return true;
                    case { IsAccessible: true, HasItem: false }:
                        found = slots[i];
                        return true;
                }
            }

            found = null;
            return false;
        }

        // 인벤토리 용량 변경 진입 메서드
        public bool SetCapacity(int capa, bool byForce = false)
        {
            if (capa > maxCapacity || capa == capacity) return false; // Max exceeded or no change

            if (capa > capacity) ExpandCapacity(capa);
            else ShrinkCapacity(capa);
            
            capacity = capa;
            OnCapacityChanged?.Invoke(capa);
            return true;
        }

        // 용량 축소 구간 슬롯 비활성화
        private void ShrinkCapacity(int capa)
        {
            for (int i = capa; i < capacity; i++)
            {
                if (GetSlot(i) is not { } slot) continue;
                slot.SetVisibility(false);
                slot.SetAccessibility(false);
            }
        }

        // 용량 확장 구간 슬롯 생성/활성화
        private void ExpandCapacity(int capa)
        {
            for (int i = capacity; i < capa; i++)
            {
                if (GetSlot(i) is not { } slot)
                {
                    var newSlot = MakeEmptySlot(i);
                    slots.Add(newSlot);
                    slot = newSlot;
                }
                
                slot.SetVisibility(true);
                slot.SetAccessibility(true);
            }
        }

        #endregion
    }
}

