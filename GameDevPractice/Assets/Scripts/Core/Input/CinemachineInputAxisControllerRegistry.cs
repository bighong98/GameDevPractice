using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace TH.Core.Input
{
    [RequireComponent(typeof(CinemachineInputAxisController))]
    public sealed class CinemachineInputAxisControllerRegistry : MonoBehaviour
    {
        private static readonly HashSet<CinemachineInputAxisController> ControllersInternal = new();

        public static IReadOnlyCollection<CinemachineInputAxisController> Controllers => ControllersInternal;

        private CinemachineInputAxisController controller;

        private void Awake()
        {
            controller = GetComponent<CinemachineInputAxisController>();
        }

        private void OnEnable()
        {
            if (controller != null)
                ControllersInternal.Add(controller);
        }

        private void OnDisable()
        {
            if (controller != null)
                ControllersInternal.Remove(controller);
        }

        private void OnDestroy()
        {
            if (controller != null)
                ControllersInternal.Remove(controller);
        }
    }
}
