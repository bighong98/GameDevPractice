using Unity.Cinemachine;
using UnityEngine;
using System;

namespace TH.Cinematic.Service
{
    public interface ICameraHolder
    {
        Camera GetMainCamera { get; }
        event Action<Camera> OnCameraInstanceUpdated;
        bool TryGetMainCamera(out Camera camera);
        void SetCinemachineBrain(CinemachineBrain brain);
    }
}

