using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using TH.SaveLoad;
using TH.Utils;

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
            
            SavingWrapper savingWrapper = FindFirstObjectByType<SavingWrapper>();
            await savingWrapper.Save(); // 다음 씬 로드 전 현재 씬 상태 저장
            
            await GameSceneManager.Instance.LoadSceneAsync(info.destinationScene);
            
            await UniTask.Yield(); // 씬 로드 직후 한 프레임 대기
            await savingWrapper.Load(); // 다음 씬 로드 후 상태 로드

            Portal portal = GetDestinationPortal();
            if (portal != null)
            {
                TeleportPlayer(portal);
                await savingWrapper.Save(); // 씬 로드 후 변동사항 다시 한번 저장
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

