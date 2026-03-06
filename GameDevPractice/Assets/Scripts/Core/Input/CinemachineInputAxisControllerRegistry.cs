using System.Collections.Generic;
using TH.Utils;
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
            LogRegistryDebug(
                $"Awake controller={(controller != null ? controller.name : "null")}, gameObject={name}, registeredCount={ControllersInternal.Count}");
        }

        private void OnEnable()
        {
            if (controller != null)
                ControllersInternal.Add(controller);

            LogRegistryDebug(
                $"OnEnable controller={(controller != null ? controller.name : "null")}, gameObject={name}, registeredCount={ControllersInternal.Count}");
        }

        private void OnDisable()
        {
            if (controller != null)
                ControllersInternal.Remove(controller);

            LogRegistryDebug(
                $"OnDisable controller={(controller != null ? controller.name : "null")}, gameObject={name}, registeredCount={ControllersInternal.Count}");
        }

        private void OnDestroy()
        {
            if (controller != null)
                ControllersInternal.Remove(controller);

            LogRegistryDebug(
                $"OnDestroy controller={(controller != null ? controller.name : "null")}, gameObject={name}, registeredCount={ControllersInternal.Count}");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogRegistryDebug(string message)
        {
            Logg.Log($"[CamDebug][Registry] {message}", Logg.LoggingMode.Completed);
        }
#else
        private static void LogRegistryDebug(string message) { }
#endif
    }
}
