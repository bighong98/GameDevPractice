using TH.Utils;

namespace TH.Item
{
    public sealed partial class PlayerStorage
    {
        #region Slot Helper Methods

        private void NotifySlotChanged(int index)
        {
            if (!IsValidSlotIdx(index)) return;
            if (slots[index] is not { } slot) return;
            
            NotifySlotChanged(slot);
        }

        private void NotifySlotChanged(IGameItemSlot slot)
        {
            Logg.Log($"[PlayerStorage] NotifySlotChanged({slot} - {slot.Index})", Logg.LoggingMode.Completed);
            slot.SetVisibility(IsVisibleByFilter(slot, CurrentFilter));
            OnSlotChanged?.Invoke(slot);
        }

        private IGameItemSlot GetSlot(int index)
        {
            if (!IsValidSlotIdx(index)) return null;
            return slots[index];
        }

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

        public bool SetCapacity(int capa, bool byForce = false)
        {
            if (capa > maxCapacity || capa == capacity) return false; // Max exceeded or no change

            if (capa > capacity) ExpandCapacity(capa);
            else ShrinkCapacity(capa);
            
            capacity = capa;
            OnCapacityChanged?.Invoke(capa);
            return true;
        }

        private void ShrinkCapacity(int capa)
        {
            for (int i = capa; i < capacity; i++)
            {
                if (GetSlot(i) is not { } slot) continue;
                slot.SetVisibility(false);
                slot.SetAccessibility(false);
            }
        }

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

