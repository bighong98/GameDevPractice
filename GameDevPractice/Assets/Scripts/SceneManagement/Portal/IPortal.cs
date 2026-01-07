using UnityEngine;

namespace TH.SceneManagement
{
    public interface IPortal
    {
        public PortalInfoSO PortalInfo { get; }
        public Transform SpawnPoint { get; }
    }
}
