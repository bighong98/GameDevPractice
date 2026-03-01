using UnityEngine;
using TH.Utils;

namespace TH.Item
{
    [DisallowMultipleComponent]
    public class AvatarAnchorProvider : MonoBehaviour
    {
        [SerializeField] private Transform bodyRootTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;

        public Transform BodyRootTransform
        {
            get
            {
                EnsureInitialized();
                return bodyRootTransform;
            }
        }

        public Transform RightHandTransform
        {
            get
            {
                EnsureInitialized();
                return rightHandTransform;
            }
        }

        public Transform LeftHandTransform
        {
            get
            {
                EnsureInitialized();
                return leftHandTransform;
            }
        }

        private bool isInitialized = false;

        private const string DefaultRootName = "Root";
        private const string DefaultRightHandContainerName = "hand_holder_r";
        private const string DefaultLeftHandContainerName = "hand_holder_l";
        private const string DefaultRightWeaponContainerName = "weapon_r";
        private const string DefaultLeftWeaponContainerName = "weapon_l";

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (isInitialized) return;
            isInitialized = true;

            ResolveAnchors();
        }

        private void ResolveAnchors()
        {
            if (bodyRootTransform == null)
                bodyRootTransform = Util.FindChild<Transform>(gameObject, DefaultRootName, recursive: true);
            if (bodyRootTransform == null)
            {
                this.LogError("failed to find avatar root");
                return;
            }

            var root = bodyRootTransform.gameObject;

            if (rightHandTransform == null &&
                Util.FindChildContainName<Transform>(root, DefaultRightHandContainerName, true, false) is { } rResult)
            {
                rightHandTransform = EnsureWeaponContainer(rResult, DefaultRightWeaponContainerName);
            }

            if (leftHandTransform == null &&
                Util.FindChildContainName<Transform>(root, DefaultLeftHandContainerName, true, false) is { } lResult)
            {
                leftHandTransform = EnsureWeaponContainer(lResult, DefaultLeftWeaponContainerName);
            }
        }

        private static Transform EnsureWeaponContainer(Transform handTransform, string containerName)
        {
            var existing = Util.FindChild<Transform>(handTransform.gameObject, containerName, recursive: true);
            if (existing != null) return existing;

            var container = new GameObject(containerName).transform;
            container.SetParent(handTransform, worldPositionStays: false);
            return container;
        }
    }
}
