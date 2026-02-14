using TH.Item;
using TH.Resource;

namespace TH.UI
{
    public readonly struct TooltipTokenContext
    {
        public TooltipTokenContext(ItemTypeSO itemInfo, IGameItem runtimeItem, TooltipDetailLevel detailLevel)
        {
            ItemInfo = itemInfo;
            RuntimeItem = runtimeItem;
            DetailLevel = detailLevel;
        }

        public ItemTypeSO ItemInfo { get; }
        public IGameItem RuntimeItem { get; }
        public TooltipDetailLevel DetailLevel { get; }
    }
}
