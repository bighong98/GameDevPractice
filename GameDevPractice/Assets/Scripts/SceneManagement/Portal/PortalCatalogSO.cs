using System.Collections.Generic;
using UnityEngine;

namespace TH.SceneManagement
{
    [CreateAssetMenu(fileName = "PortalCatalogSO", menuName = "Scriptable Objects/PortalCatalogSO")]
    public class PortalCatalogSO : ScriptableObject
    {
        public List<PortalInfoSO> portals;
    }
}

