using System;
using UnityEditor;

#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.AddressableAssets;
#endif

namespace TH.SceneManagement
{
    public sealed class AssetReferencePortal : AssetReferenceT<Portal>, IEquatable<AssetReferencePortal>
    {
        public AssetReferencePortal(string guid) : base(guid) { }
        
        public bool Equals(AssetReferencePortal other)
        {
            if (other == null) return false;

            return (m_AssetGUID == other.m_AssetGUID); // GUID로 동일성 판단
        }
    }
}

