using System;
using System.Collections.Generic;

namespace TH.Item
{
    public sealed class StorageEventBatcher : IStorageEventBatcher
    {
        private readonly Action<int> onSlotChanged;
        private readonly Action onStorageChanged;
        private readonly HashSet<int> pendingSlotIndices = new HashSet<int>();
        private int batchDepth;
        private bool pendingStorageChanged;

        public StorageEventBatcher(Action<int> onSlotChanged, Action onStorageChanged)
        {
            this.onSlotChanged = onSlotChanged;
            this.onStorageChanged = onStorageChanged;
        }

        public void BeginEventBatch()
        {
            batchDepth++;
        }

        public void EndEventBatch()
        {
            if (batchDepth <= 0) return;
            batchDepth--;
            if (batchDepth == 0)
                Flush();
        }

        public void NotifySlotChanged(int index)
        {
            if (batchDepth > 0)
            {
                pendingSlotIndices.Add(index);
                return;
            }

            onSlotChanged?.Invoke(index);
        }

        public void NotifyStorageChanged()
        {
            if (batchDepth > 0)
            {
                pendingStorageChanged = true;
                return;
            }

            onStorageChanged?.Invoke();
        }

        private void Flush()
        {
            foreach (var index in pendingSlotIndices)
            {
                onSlotChanged?.Invoke(index);
            }

            pendingSlotIndices.Clear();

            if (pendingStorageChanged)
            {
                pendingStorageChanged = false;
                onStorageChanged?.Invoke();
            }
        }
    }
}
