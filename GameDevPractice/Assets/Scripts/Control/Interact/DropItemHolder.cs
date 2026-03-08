using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Control
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
public class DropItemHolder : ItemTypeHolder, IDropItem
    {
        [Header("Drop")]
        [SerializeField] private bool useImmediately;

        [Header("Scatter Motion")]
        [SerializeField, Min(0.05f)] private float scatterDuration = 0.25f;
        [SerializeField, Min(0f)] private float landingGroundOffset = 0.03f;
        [SerializeField, Min(0.1f)] private float landingRayHeight = 2f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(1)] private int landingRetryCount = 6;
        [SerializeField, Min(0.01f)] private float landingCheckRadius = 0.15f;
        [SerializeField] private LayerMask landingObstacleMask = ~0;

        [Header("Pickup")]
        [SerializeField] private bool lockPickupUntilLanding = true;

        private readonly Collider[] overlapBuffer = new Collider[16];
        private Coroutine scatterRoutine;        private Collider cachedCollider;
        private bool hasLanded = true;

        public ItemTypeSO ItemData => Type;
        public bool UseImmediately => useImmediately;
        public int Amount => GetAmount;
        public Transform Trs => transform;

        private void Awake()
        {
            TryGetComponent(out cachedCollider);
        }

        public override void OnGetFromPool()
        {
            base.OnGetFromPool();

            StopScatterRoutine();
            hasLanded = true;
            SetPickupColliderEnabled(true);
        }

        public void Interact()
        {
            if (lockPickupUntilLanding && !hasLanded)
                return;

            this.Log($"Interact() invoked", Logg.LoggingMode.Completed);
            ReleaseSelf();
        }

        public void ApplyScatterForce(float horizontalForce, float upwardForce)
        {
            var scatterDistance = Mathf.Max(0f, horizontalForce);
            var arcHeight = Mathf.Max(0f, upwardForce);

            var start = transform.position;
            var landingPoint = ResolveLandingPoint(start, scatterDistance);

            StopScatterRoutine();
            scatterRoutine = StartCoroutine(CoMoveScatter(start, landingPoint, arcHeight));
        }

        private System.Collections.IEnumerator CoMoveScatter(Vector3 start, Vector3 end, float arcHeight)
        {
            hasLanded = false;
            if (lockPickupUntilLanding)
                SetPickupColliderEnabled(false);

            var duration = Mathf.Max(0.05f, scatterDuration);
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                var planar = Vector3.Lerp(start, end, t);
                var arc = 4f * arcHeight * t * (1f - t);
                transform.position = planar + Vector3.up * arc;
                yield return null;
            }

            transform.position = end;
            hasLanded = true;
            if (lockPickupUntilLanding)
                SetPickupColliderEnabled(true);

            scatterRoutine = null;
        }

        private Vector3 ResolveLandingPoint(Vector3 start, float horizontalDistance)
        {
            var candidate = start;
            for (var i = 0; i < Mathf.Max(1, landingRetryCount); i++)
            {
                var randomOffset = Random.insideUnitCircle * horizontalDistance;
                var planarCandidate = start + new Vector3(randomOffset.x, 0f, randomOffset.y);
                if (!TrySnapToGround(planarCandidate, out candidate))
                    continue;

                if (!HasLandingObstacle(candidate))
                    return candidate;
            }

            if (TrySnapToGround(start, out var fallback))
                return fallback;

            return start + Vector3.up * landingGroundOffset;
        }

        private bool TrySnapToGround(Vector3 planarPoint, out Vector3 result)
        {
            var rayOrigin = planarPoint + Vector3.up * landingRayHeight;
            var rayDistance = landingRayHeight * 2f + 10f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                result = hit.point + Vector3.up * landingGroundOffset;
                return true;
            }

            result = planarPoint + Vector3.up * landingGroundOffset;
            return false;
        }

        private bool HasLandingObstacle(Vector3 landingPoint)
        {
            var checkCenter = landingPoint + Vector3.up * landingCheckRadius;
            var count = Physics.OverlapSphereNonAlloc(
                checkCenter,
                landingCheckRadius,
                overlapBuffer,
                landingObstacleMask,
                QueryTriggerInteraction.Ignore);

            for (var i = 0; i < count; i++)
            {
                var hitCollider = overlapBuffer[i];
                if (hitCollider == null)
                    continue;

                if (cachedCollider != null && (hitCollider == cachedCollider || hitCollider.transform.IsChildOf(transform)))
                    continue;

                return true;
            }

            return false;
        }

        private void SetPickupColliderEnabled(bool enabled)
        {
            if (!lockPickupUntilLanding)
                return;

            if (cachedCollider == null)
                TryGetComponent(out cachedCollider);

            if (cachedCollider != null)
                cachedCollider.enabled = enabled;
        }

        private void StopScatterRoutine()
        {
            if (scatterRoutine == null)
                return;

            StopCoroutine(scatterRoutine);
            scatterRoutine = null;
        }

        private void OnDisable()
        {
            StopScatterRoutine();
        }
    }
}
