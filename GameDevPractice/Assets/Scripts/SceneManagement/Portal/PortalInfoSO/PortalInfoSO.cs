using TH.SceneManagement;
using UnityEngine;

namespace TH.SceneManagement
{
    [CreateAssetMenu(fileName = "PortalInfoSO", menuName = "Scriptable Objects/PortalInfoSO")]
    public class PortalInfoSO : ScriptableObject
    {
        public AssetReferenceScene destinationScene; // 현재 씬이면 empty
        public PortalInfoSO destinationPortal; // 단순 도착지 목적이면 empty
    }
}

