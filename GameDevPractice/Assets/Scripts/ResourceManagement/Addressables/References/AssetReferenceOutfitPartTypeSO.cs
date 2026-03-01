using System;

namespace TH.Resource
{
    [Serializable]
    public class AssetReferenceOutfitPartTypeSO : AssetReferenceGeneric<OutfitPartTypeSO>
    {
#if UNITY_EDITOR
        public AssetReferenceOutfitPartTypeSO() { }

        public AssetReferenceOutfitPartTypeSO(OutfitPartTypeSO obj)
            : base(obj)
        {
        }
#endif
    }
}
