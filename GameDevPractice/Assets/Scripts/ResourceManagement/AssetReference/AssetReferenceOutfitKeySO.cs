using System;

namespace TH.Resource
{
    [Serializable]
    public class AssetReferenceOutfitKeySO : AssetReferenceGeneric<OutfitKeySO>
    {
#if UNITY_EDITOR
        public AssetReferenceOutfitKeySO() { }

        public AssetReferenceOutfitKeySO(OutfitKeySO obj)
            : base(obj)
        {
        }
#endif
    }
}
