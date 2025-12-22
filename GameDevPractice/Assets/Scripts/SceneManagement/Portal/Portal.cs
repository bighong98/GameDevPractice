using Cysharp.Threading.Tasks;
using UnityEngine;
using TH.SaveLoad;
using TH.Core.Service;

namespace TH.SceneManagement
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class Portal : MonoBehaviour
    {
        [SerializeField] private PortalInfoSO info;
        [SerializeField] private Transform spawnPoint;

        private void Awake()
        {
            if (spawnPoint == null)
            {
                spawnPoint = Util.FindChild<Transform>(gameObject, "SpawnPoint");
            }

            if (NotTransitionWithoutDestination()) return;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (NotTransitionWithoutDestination()) return;
            if (other.CompareTag("Player"))
            {
                Transition().Forget();
            }
        }
        
        private async UniTask Transition()
        {
            if (NotTransitionWithoutDestination()) return;
            
            DontDestroyOnLoad(gameObject);
            
            await GameSceneManager.Instance.LoadSceneAsync(info.destinationScene);

            Portal portal = GetDestinationPortal();
            if (portal != null)
            {
                TeleportPlayer(portal);
            }
            
            Destroy(gameObject);
        }
        
        private Portal GetDestinationPortal()
        {
            if (info is not { destinationPortal: { } destPortal }) return null;
            foreach (Portal portal in FindObjectsByType<Portal>(UnityEngine.FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (portal == this) continue;
                // if (portal.destination != destination) continue; // 현재 포탈은 포탈 두개가 같은 Identifier로 결합되어있는 구조임
                if (portal.info != destPortal) continue;
                
                return portal;
            }

            return null;
        }
        
        private void TeleportPlayer(Portal otherPortal)
        {
            GameObject player = GameObject.FindWithTag("Player");
            var navMeshAgent = player.GetComponent<UnityEngine.AI.NavMeshAgent>();
            navMeshAgent.enabled = false;
            player.transform.position = otherPortal.spawnPoint.position;
            player.transform.rotation = otherPortal.spawnPoint.rotation;
            navMeshAgent.enabled = true;
        }

        private bool NotTransitionWithoutDestination()
        {
            if (info is not { destinationScene: { } dest, destinationPortal: {} destPortalData } 
                || !dest.IsAlive()
                || !destPortalData.IsAlive())
            {
                if (TryGetComponent(out Collider coll))
                    coll.enabled = false;

                return true;
            }

            return false;
        }
    }
}

