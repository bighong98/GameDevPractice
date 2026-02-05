using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "OutfitKey", menuName = "Scriptable Objects/Outfit/OutfitKey")]
    public sealed class OutfitKeySO : ScriptableObject
    {
        public OutfitPartTypeSO partType;
        public string id;
    }
}
