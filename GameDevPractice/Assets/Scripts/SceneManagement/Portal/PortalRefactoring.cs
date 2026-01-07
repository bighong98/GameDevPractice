using TH.Core.Service;
using UnityEngine;

namespace TH.SceneManagement
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class PortalRefactoring : MonoBehaviour, IPortal
    {
        [SerializeField] private PortalInfoSO info;
        [SerializeField] private Transform spawnPoint;
        
        public PortalInfoSO PortalInfo => info;
        public Transform SpawnPoint => spawnPoint;
        
        private IPortalManager portalManager;
        
        private void Awake()
        {
            portalManager = ServiceLocator.Get<IPortalManager>();
            portalManager.RegisterPortal(this);
            
            if (spawnPoint == null)
            {
                spawnPoint = Util.FindChild<Transform>(gameObject, "SpawnPoint");
            }

            ValidatePortal();
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                portalManager.OperatePortal(info);
            }
        }
        
        // 도착지가 없는 (단순 출구 역할) 포탈의 경우 Collider 비활성화
        private void ValidatePortal()
        {
            if (info is { destinationScene: { } dest, destinationPortal: { } destPortalData }
                && dest.IsNotNull()
                && destPortalData.IsNotNull()) return;
            
            if (TryGetComponent(out Collider coll))
                coll.enabled = false;
        }
    }
}

