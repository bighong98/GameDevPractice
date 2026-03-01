using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace TH.SceneManagement
{
    public class PortalManager : IPortalManager
    {
        private readonly ISceneLoader sceneLoader;
        private readonly IPlayerHolder playerHolder;
        
        private readonly List<IPortal> portals = new ();

        private bool pendingTeleport;
        private PortalInfoSO destinationPortalInfo;
        
        public PortalManager(ISceneLoader sceneLoader, IPlayerHolder playerHolder)
        {
            this.sceneLoader = sceneLoader;
            this.playerHolder = playerHolder;

            sceneLoader.OnBeforeSceneChanged += OnBeforeSceneChanged;
            sceneLoader.OnSceneChanged += OnSceneChanged;
        }
        
        public void RegisterPortal(IPortal portal)
        {
            if (!portal.IsNotNull())
            {
                Logg.LogWarning("[PortalManager] RegisterPortal - invalid portal");
                return;
            }
            
            Logg.Log($"[PortalManager] RegisterPortal({portal.PortalInfo})", Logg.LoggingMode.Completed, portal as UnityEngine.Object);
            portals.Add(portal);
        }

        public void OperatePortal(PortalInfoSO portalInfo)
        {
            Logg.Log($"[PortalManager] OperatePortal ({portalInfo.name} -> {portalInfo.destinationPortal.name})", Logg.LoggingMode.Completed , context:portalInfo);
            //todo: destination.destinationScene 유효성 검사 추가
            if (portalInfo == null || portalInfo.destinationPortal == null)
            {
                Logg.LogWarning("[PortalManager] TeleportPlayer - invalid portal");
                return;
            }

            pendingTeleport = true;
            destinationPortalInfo = portalInfo.destinationPortal;

            //todo: 같은 씬으로의 이동 처리 분기 추가
            sceneLoader.LoadSceneAsync(portalInfo.destinationScene);
        }

        private UniTask OnBeforeSceneChanged(CancellationToken externalToken)
        {
            try
            {
                externalToken.ThrowIfCancellationRequested();
                ClearPortalRegistry();
            }
            catch (Exception e) { Logg.LogError(e); }

            return UniTask.CompletedTask;
        }

        private void ClearPortalRegistry()
        {
            portals.Clear();
        }

        private void OnSceneChanged(Scene scene)
        {
            if (!pendingTeleport) return;
            if (!TryFindDestinationPortal(out var destinationPortal))
            {
                Logg.LogWarning($"[PortalManager] OnSceneChange - failed to find Destination Portal", context: destinationPortalInfo);
                return;
            }
            
            TeleportPlayer(destinationPortal);
        }

        private bool TryFindDestinationPortal(out Transform destination)
        {
            destination = null;
            if (portals.Count == 0)
            {
                Logg.LogWarning($"[PortalManager] TryFindDestinationPortal " +
                                $"- there is no portal in current scene");
                return false;
            }

            foreach (var portal in portals)
            {
                if (portal == null || portal.SpawnPoint == null)
                {
                    Logg.LogWarning($"[PortalManager] TryFindDestinationPortal " +
                                    $"- invalid portal exists in portal registry", context: portal as UnityEngine.Object);
                    continue;
                }

                if (!ReferenceEquals(portal.PortalInfo, destinationPortalInfo)) continue;

                destination = portal.SpawnPoint;
                return true;
            }

            return false;
        }

        private void TeleportPlayer(Transform destination)
        {
            var player = playerHolder.GetPlayerInstance as Component;
            if (!player.IsNotNull() || 
                !player.TryGetComponent(out NavMeshAgent navMeshAgent))
            {
                Logg.LogWarning("[PortalManager] TeleportPlayer - invalid player");
                return;
            }
            
            navMeshAgent.enabled = false;
            player.transform.position = destination.position;
            player.transform.rotation = destination.rotation;
            navMeshAgent.enabled = true;
            
            Logg.Log($"[PortalManager] TeleportPlayer({destination.position}, {destination.rotation})", Logg.LoggingMode.Completed);

            pendingTeleport = false;
            destinationPortalInfo = null;
        }
    }
}
