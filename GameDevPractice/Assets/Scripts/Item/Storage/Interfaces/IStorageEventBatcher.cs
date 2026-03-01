namespace TH.Item
{
    public interface IStorageEventBatcher
    {
        void BeginEventBatch();
        void EndEventBatch();
        void NotifySlotChanged(int index);
        void NotifyStorageChanged();
    }
}
