using UnityEngine;

namespace TH.SceneManagement
{
    public interface IPortalManager
    {
        void RegisterPortal(IPortal portal);
        void OperatePortal(PortalInfoSO portalInfo);
    }
}

