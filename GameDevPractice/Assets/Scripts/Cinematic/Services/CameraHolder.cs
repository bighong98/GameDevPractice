using System;
using TH.Utils;
using Unity.Cinemachine;
using UnityEngine;

namespace TH.Cinematic.Service
{
    public class CameraHolder : ICameraHolder
    {
        private CinemachineBrain cinemachineBrain;
        private Camera mainCamera;
        private bool init;

        public event Action<Camera> OnCameraInstanceUpdated;

        public Camera GetMainCamera => mainCamera;

        public bool TryGetMainCamera(out Camera camera)
        {
            if (!init || mainCamera == null)
            {
                camera = null;
                return false;
            }

            camera = mainCamera;
            return true;
        }

        public void SetCinemachineBrain(CinemachineBrain brain)
        {
            if (cinemachineBrain != null) 
            {
                this.LogWarning("SetCinemachineBrain() - duplicate SetCinemachineBrain called");
                return;
            }

            if (brain == null)
            {
                this.LogWarning("SetCinemachineBrain() - invalid CinemachineBrain component");
                return;
            }

            cinemachineBrain = brain;
            init = true;
            Logg.Log($"Cinemachine Brain is set", Logg.LoggingMode.Completed, context: cinemachineBrain);
            
            if (!cinemachineBrain.TryGetComponent(out mainCamera))
            {
                this.LogWarning("CinemachineBrain component is not locataed where Main Camera attached. Automatically AddComponent<Camera>()");
                mainCamera = cinemachineBrain.gameObject.AddComponent<Camera>();
            }

            if (mainCamera != null)
                Logg.Log($"Main Camera is set", Logg.LoggingMode.Completed, context: cinemachineBrain);

            OnCameraInstanceUpdated?.Invoke(mainCamera);
        }


    }
}

