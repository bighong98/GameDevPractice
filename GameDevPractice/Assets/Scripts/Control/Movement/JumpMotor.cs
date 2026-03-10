using System;
using TH.Core;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Movement
{
    public sealed class JumpMotor : MonoBehaviour, IJumpMotor

    {
        [Header("Jump Spec")]
        [SerializeField, Min(0.01f)] private float jumpDuration = 0.4f;
        [SerializeField] private float jumpStartVerticalVelocity = 5f;
        [SerializeField] private float gravity = -20f;

        [Header("Future Tuning")]
        [SerializeField, Min(0f)] private float coyoteTime = 0f;
        [SerializeField, Min(0f)] private float jumpBufferTime = 0f;

        [Header("Jump Stability")]
        [SerializeField] private bool useBallisticDuration = true;

        [Header("NavMesh Jump Sync")]
        [SerializeField] private NavMeshAgent navMeshAgent;
        [SerializeField] private bool alignForwardToJumpDirection = true;
        [SerializeField, Min(0f)] private float fallbackHorizontalSpeed = 0f;
        [SerializeField, Min(0.001f)] private float minDirectionMagnitude = 0.05f;
        [SerializeField, Min(0.05f)] private float navMeshSnapDistance = 1.5f;
        [SerializeField, Min(0f)] private float groundProbeStartHeight = 0.5f;
        [SerializeField, Min(0.05f)] private float groundProbeDistance = 2.5f;
        [SerializeField] private LayerMask groundProbeMask = Physics.DefaultRaycastLayers;
        [SerializeField] private QueryTriggerInteraction groundProbeTriggerInteraction = QueryTriggerInteraction.Ignore;
        [SerializeField, Min(0f)] private float groundSnapOffset = 0.02f;
        [SerializeField] private float groundedVerticalVelocityThreshold = -0.5f;
        [Header("Animation Sync")]
        [SerializeField] private Animator targetAnimator;



        public event Action OnJumpStarted;
        public event Action OnLanded;

        public bool IsGrounded { get; private set; } = true;
        public bool IsJumping { get; private set; }
        public float VerticalVelocity { get; private set; }

        public float CoyoteTime => coyoteTime;
        public float JumpBufferTime => jumpBufferTime;
        public float LastGroundedTime { get; private set; }
        public float LastJumpPressedTime { get; private set; } = float.NegativeInfinity;

        private int lastJumpPressedFrame = -1;
        private float jumpEndTime;
        private float jumpStartTime;
        private float effectiveJumpDuration;
        private Vector3 jumpStartPosition;
        private InputManager inputManager;

        private bool detached;
        private bool cachedUpdatePosition;
        private bool cachedUpdateRotation;
        private bool cachedIsStopped;
        private Vector3 planarVelocity;
        private bool cachedAnimatorApplyRootMotion;
        private bool rootMotionOverridden;

        private readonly RaycastHit[] groundProbeHits = new RaycastHit[8];



        private void Awake()
        {
            LastGroundedTime = Time.time;

            if (navMeshAgent == null)
            {
                TryGetComponent(out navMeshAgent);
            }

            if (targetAnimator == null)
            {
                TryGetComponent(out targetAnimator);
            }
        }

        private void Reset()
        {
            TryGetComponent(out navMeshAgent);
            TryGetComponent(out targetAnimator);
        }

        private void OnEnable()
        {
            inputManager = InputManager.Instance;
            if (inputManager != null)
            {
                inputManager.OnJumped += HandleJumped;
            }

            if (targetAnimator == null)
            {
                TryGetComponent(out targetAnimator);
            }

            if (IsJumping && !detached)
            {
                planarVelocity = ResolvePlanarVelocity();
                DetachAgentForJump();
                OverrideAnimatorRootMotionForJump();
            }
        }

        private void OnDisable()
        {
            if (inputManager != null)
            {
                inputManager.OnJumped -= HandleJumped;
                inputManager = null;
            }

            RestoreAnimatorRootMotionIfNeeded();
            RestoreAgentFlagsOnly();
            planarVelocity = Vector3.zero;
        }

        private void HandleJumped()
        {
            RequestJump();
        }


        private void Update()
        {
            if (!IsJumping)
            {
                return;
            }

            float elapsed = Time.time - jumpStartTime;
            float normalizedElapsed = Mathf.Clamp(elapsed, 0f, effectiveJumpDuration);

            VerticalVelocity = jumpStartVerticalVelocity + (gravity * normalizedElapsed);
            float verticalDisplacement = (jumpStartVerticalVelocity * normalizedElapsed)
                                      + (0.5f * gravity * normalizedElapsed * normalizedElapsed);

            Vector3 nextPosition = jumpStartPosition + (planarVelocity * normalizedElapsed);
            nextPosition.y = jumpStartPosition.y + verticalDisplacement;

            if (TryGetGroundHeight(nextPosition, out float groundHeight))
            {
                float groundLimit = groundHeight + groundSnapOffset;
                if (nextPosition.y <= groundLimit && VerticalVelocity <= groundedVerticalVelocityThreshold)
                {
                    nextPosition.y = groundLimit;
                    transform.position = nextPosition;
                    Land(nextPosition);
                    return;
                }

                if (nextPosition.y < groundLimit)
                {
                    nextPosition.y = groundLimit;
                }
            }

            transform.position = nextPosition;

            if (Time.time >= jumpEndTime)
            {
                Vector3 landingPosition = transform.position;
                if (TryGetGroundHeight(landingPosition, out float landingGroundHeight))
                {
                    landingPosition.y = Mathf.Max(landingPosition.y, landingGroundHeight + groundSnapOffset);
                }

                Land(landingPosition);
            }
        }

        public void RequestJump()
        {
            LastJumpPressedTime = Time.time;
            lastJumpPressedFrame = Time.frameCount;
        }

        public bool HasPendingJumpRequest()
        {
            if (jumpBufferTime > 0f)
            {
                return (Time.time - LastJumpPressedTime) <= jumpBufferTime;
            }

            return lastJumpPressedFrame == Time.frameCount;
        }

        public bool CanStartJump()
        {
            if (IsJumping)
            {
                return false;
            }

            bool hasGroundWindow = IsGrounded;
            if (!hasGroundWindow && coyoteTime > 0f)
            {
                hasGroundWindow = (Time.time - LastGroundedTime) <= coyoteTime;
            }

            return hasGroundWindow && HasPendingJumpRequest();
        }

        public bool TryStartJump()
        {
            if (!CanStartJump())
            {
                return false;
            }

            effectiveJumpDuration = ResolveEffectiveJumpDuration();
            jumpStartTime = Time.time;
            jumpEndTime = jumpStartTime + effectiveJumpDuration;
            jumpStartPosition = transform.position;

            IsGrounded = false;
            IsJumping = true;
            VerticalVelocity = jumpStartVerticalVelocity;

            planarVelocity = ResolvePlanarVelocity();
            if (alignForwardToJumpDirection)
            {
                Vector3 direction = planarVelocity;
                direction.y = 0f;
                if (direction.sqrMagnitude >= minDirectionMagnitude * minDirectionMagnitude)
                {
                    transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            DetachAgentForJump();
            OverrideAnimatorRootMotionForJump();

            lastJumpPressedFrame = -1;
            if (jumpBufferTime > 0f)
            {
                LastJumpPressedTime = float.NegativeInfinity;
            }

            OnJumpStarted?.Invoke();
            return true;
        }

        private void Land(Vector3 landingPosition)
        {
            if (!IsJumping)
            {
                return;
            }

            transform.position = landingPosition;
            RestoreAgentAndSnapToNavMesh(landingPosition);
            IsJumping = false;
            IsGrounded = true;
            VerticalVelocity = 0f;
            LastGroundedTime = Time.time;
            RestoreAnimatorRootMotionIfNeeded();
            OnLanded?.Invoke();
        }

        private float ResolveEffectiveJumpDuration()
        {
            float resolvedDuration = Mathf.Max(0.01f, jumpDuration);
            if (!useBallisticDuration)
            {
                return resolvedDuration;
            }

            if (gravity >= -Mathf.Epsilon || jumpStartVerticalVelocity <= 0f)
            {
                return resolvedDuration;
            }

            float ballisticDuration = (2f * jumpStartVerticalVelocity) / -gravity;
            return Mathf.Max(resolvedDuration, ballisticDuration);
        }

        private Vector3 ResolvePlanarVelocity()
        {
            Vector3 velocity = Vector3.zero;

            if (navMeshAgent != null)
            {
                velocity = navMeshAgent.velocity;
                velocity.y = 0f;

                if (velocity.sqrMagnitude < minDirectionMagnitude * minDirectionMagnitude)
                {
                    velocity = navMeshAgent.desiredVelocity;
                    velocity.y = 0f;
                }
            }

            if (velocity.sqrMagnitude < minDirectionMagnitude * minDirectionMagnitude && fallbackHorizontalSpeed > 0f)
            {
                velocity = transform.forward * fallbackHorizontalSpeed;
                velocity.y = 0f;
            }

            return velocity;
        }

        private bool TryGetGroundHeight(Vector3 worldPosition, out float groundHeight)
        {
            groundHeight = worldPosition.y;

            float castStartHeight = Mathf.Max(0f, groundProbeStartHeight);
            float castDistance = Mathf.Max(0.05f, groundProbeDistance);
            Vector3 rayOrigin = worldPosition + (Vector3.up * castStartHeight);

            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                Vector3.down,
                groundProbeHits,
                castStartHeight + castDistance,
                groundProbeMask,
                groundProbeTriggerInteraction);

            if (hitCount <= 0)
            {
                return false;
            }

            bool found = false;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundProbeHits[i];
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                {
                    continue;
                }

                Transform hitTransform = hitCollider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                {
                    continue;
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                groundHeight = hit.point.y;
                found = true;
            }

            return found;
        }

        private bool TryResolveNavMeshPosition(Vector3 sourcePosition, out Vector3 resolvedPosition)
        {
            float searchDistance = Mathf.Max(0.05f, navMeshSnapDistance);
            for (int i = 0; i < 3; i++)
            {
                if (NavMesh.SamplePosition(sourcePosition, out NavMeshHit hit, searchDistance, NavMesh.AllAreas))
                {
                    resolvedPosition = hit.position;
                    return true;
                }

                searchDistance *= 2f;
            }

            resolvedPosition = sourcePosition;
            return false;
        }

        private void OverrideAnimatorRootMotionForJump()
        {
            if (rootMotionOverridden)
            {
                return;
            }

            if (targetAnimator == null)
            {
                TryGetComponent(out targetAnimator);
            }

            if (targetAnimator == null)
            {
                return;
            }

            cachedAnimatorApplyRootMotion = targetAnimator.applyRootMotion;
            if (!cachedAnimatorApplyRootMotion)
            {
                return;
            }

            targetAnimator.applyRootMotion = false;
            rootMotionOverridden = true;
        }

        private void RestoreAnimatorRootMotionIfNeeded()
        {
            if (!rootMotionOverridden)
            {
                return;
            }

            if (targetAnimator != null)
            {
                targetAnimator.applyRootMotion = cachedAnimatorApplyRootMotion;
            }

            rootMotionOverridden = false;
        }





        private void DetachAgentForJump()
        {
            if (detached)
            {
                return;
            }

            if (navMeshAgent == null)
            {
                TryGetComponent(out navMeshAgent);
            }

            if (navMeshAgent == null)
            {
                return;
            }

            cachedUpdatePosition = navMeshAgent.updatePosition;
            cachedUpdateRotation = navMeshAgent.updateRotation;
            cachedIsStopped = navMeshAgent.isStopped;

            if (CanControlAgent(navMeshAgent))
            {
                navMeshAgent.ResetPath();
                navMeshAgent.isStopped = true;
            }

            navMeshAgent.updatePosition = false;
            navMeshAgent.updateRotation = false;
            detached = true;
        }

        private void RestoreAgentAndSnapToNavMesh(Vector3 desiredPosition)
        {
            if (navMeshAgent == null || !detached)
            {
                return;
            }

            Vector3 snapPosition = desiredPosition;
            if (TryResolveNavMeshPosition(desiredPosition, out Vector3 resolvedPosition))
            {
                snapPosition = resolvedPosition;
            }

            bool warped = false;
            if (navMeshAgent.enabled)
            {
                warped = navMeshAgent.Warp(snapPosition);
            }

            if (!warped)
            {
                transform.position = snapPosition;
            }

            navMeshAgent.updatePosition = cachedUpdatePosition;
            navMeshAgent.updateRotation = cachedUpdateRotation;
            navMeshAgent.isStopped = cachedIsStopped;

            if (navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            {
                navMeshAgent.nextPosition = snapPosition;
            }

            detached = false;
            planarVelocity = Vector3.zero;
        }

        private void RestoreAgentFlagsOnly()
        {
            if (navMeshAgent == null || !detached)
            {
                return;
            }

            navMeshAgent.updatePosition = cachedUpdatePosition;
            navMeshAgent.updateRotation = cachedUpdateRotation;
            navMeshAgent.isStopped = cachedIsStopped;
            detached = false;
        }

        private static bool CanControlAgent(NavMeshAgent agent)
        {
            return agent != null && agent.enabled && agent.isActiveAndEnabled && agent.isOnNavMesh;
        }
    }
}
