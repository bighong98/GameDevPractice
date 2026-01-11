using UnityEngine;
using Unity.Cinemachine;

public sealed class CinemachineCameraActivationResetter : MonoBehaviour
{
    private void OnEnable()
    {
        CinemachineCore.CameraActivatedEvent.AddListener(HandleCameraActivated);
    }

    private void OnDisable()
    {
        CinemachineCore.CameraActivatedEvent.RemoveListener(HandleCameraActivated);
    }

    private static void HandleCameraActivated(ICinemachineCamera.ActivationEventParams evt)
    {
        if (evt.IncomingCamera is CinemachineCamera vcam
            && vcam.TryGetComponent(out CinemachineOrbitalFollow orbital))
        {
            orbital.HorizontalAxis.Value = orbital.HorizontalAxis.Center;
            orbital.VerticalAxis.Value = orbital.VerticalAxis.Center;
            orbital.RadialAxis.Value = orbital.RadialAxis.Center;
        }
    }
}
