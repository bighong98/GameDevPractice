using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.UI.Data
{
    [CreateAssetMenu(fileName = "InventorySFXCatalogSO", menuName = "Scriptable Objects/TypeList/InventorySFXCatalogSO")]
    public class InventorySFXCatalogSO : KeyValueListSO<InventorySFX, AssetReferenceAudioClip>
    {
        
    }

    public enum InventorySFX
    {
        SortSFX,
    }

}
