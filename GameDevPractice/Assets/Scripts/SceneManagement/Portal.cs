using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using TH.SceneManagement;
using TH.SaveLoad;

namespace RPG.SceneManagement
{
    public class Portal : MonoBehaviour
    {
        enum DestinationIdentifier
        {
            A, B, C, D, E
        }
        
        [SerializeField] private int sceneToLoad = 0;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private DestinationIdentifier destination;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Transition().Forget();
            }
        }
        
        private async UniTask Transition()
        {
            if (sceneToLoad < 0)
            {
                Debug.LogError("Scene to load not set");
                return;
            }
            
            DontDestroyOnLoad(gameObject);

            Fader fader = FindFirstObjectByType<Fader>();
            fader.FadeOut().Forget();
            // todo: remove player control
            
            SavingWrapper savingWrapper = FindFirstObjectByType<SavingWrapper>();
            await savingWrapper.Save(); // 다음 씬 로드 전 현재 씬 상태 저장
            
            // await SceneManager.LoadSceneAsync(sceneToLoad); // 씬 로드
            await GameSceneManager.Instance.LoadSceneAsync(sceneToLoad);
            // todo: remove player control
            await UniTask.Yield(); // 씬 로드 직후 한 프레임 대기
            await savingWrapper.Load(); // 다음 씬 로드 후 상태 로드

            Portal portal = GetDestinationPortal();
            if (portal != null)
            {
                TeleportPlayer(portal);
                await savingWrapper.Save(); // 씬 로드 후 변동사항 다시 한번 저장
            }
            
            fader.FadeIn().Forget();
            // todo: restore player control
            Destroy(gameObject);
        }

        private void TeleportPlayer(Portal otherPortal)
        {
            GameObject player = GameObject.FindWithTag("Player");
            var navMeshAgent = player.GetComponent<NavMeshAgent>();
            navMeshAgent.enabled = false;
            player.transform.position = otherPortal.spawnPoint.position;
            player.transform.rotation = otherPortal.spawnPoint.rotation;
            navMeshAgent.enabled = true;
        }

        private Portal GetDestinationPortal()
        {
            foreach (Portal portal in FindObjectsByType<Portal>(UnityEngine.FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (portal == this) continue;
                if (portal.destination != destination) continue; // 현재 포탈은 포탈 두개가 같은 Identifier로 결합되어있는 구조임

                return portal;
            }

            return null;
        }

        #region Deprecated

        // private IEnumerator Transition()
        // {
        //     if (sceneToLoad < 0)
        //     {
        //         Debug.LogError("Scene to load not set");
        //         yield break;
        //     }
        //     
        //     DontDestroyOnLoad(gameObject);
        //
        //     Fader fader = FindObjectOfType<Fader>();
        //     yield return fader.FadeOut();
        //     
        //     SavingWrapper savingWrapper = FindObjectOfType<SavingWrapper>();
        //     savingWrapper.Save(); // 다음 씬 로드 전 현재 씬 상태 저장
        //     yield return SceneManager.LoadSceneAsync(sceneToLoad);
        //     savingWrapper.Load(); // 다음 씬 로드 후 상태 로드
        //
        //     Portal portal = GetDestinationPortal();
        //     TeleportPlayer(portal);
        //
        //     savingWrapper.Save(); // 씬 로드 후 변동사항 다시 한번 저장
        //
        //     yield return new WaitForSeconds(0.5f);
        //     yield return fader.FadeIn();
        //     
        //     Destroy(gameObject);
        // }

        #endregion
    }
}

