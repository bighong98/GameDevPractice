namespace TH.UI
{
    public readonly struct SaveSlotViewData
    {
        public string SaveFileName { get; }
        public string DisplayName { get; }

        public SaveSlotViewData(string saveFileName, string displayName)
        {
            SaveFileName = saveFileName;
            DisplayName = displayName;
        }
    }
}
