using TH.Editor;
using UnityEngine;
using UnityEngine.AI;

namespace TH.Control.Movement
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterFootIK : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private NavMeshAgent navMeshAgent;

        [Header("Ground Raycast")]
        [SerializeField, Min(0.01f)] private float raycastStartHeight = 0.6f;
        [SerializeField, Min(0.05f)] private float raycastDistance = 1.5f;
        [SerializeField, Layer] private int environmentLayer;
        

        [Header("Editor Gizmo")]
        [SerializeField] private bool drawFootRaycastGizmo = true;
        [SerializeField] private Color leftFootRaycastGizmoColor = new Color(0.2f, 0.8f, 1f, 0.9f);
        [SerializeField] private Color rightFootRaycastGizmoColor = new Color(1f, 0.6f, 0.2f, 0.9f);
        [SerializeField, Min(0.005f)] private float gizmoSphereRadius = 0.03f;
        [SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Foot Placement")]
        [SerializeField] private bool applyOnlyWhenMoving = false;
        [SerializeField, Min(0f)] private float movingThreshold = 0.05f;
        [SerializeField] private float footHeightOffset = 0.01f;

        [Header("IK Weight")]
        [SerializeField, Range(0f, 1f)] private float positionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 1f;
        [SerializeField, Min(0.01f)] private float weightBlendSpeed = 12f;
        [SerializeField, Min(0.01f)] private float positionLerpSpeed = 18f;
        [SerializeField, Min(0.01f)] private float rotationLerpSpeed = 18f;

        [Header("Walk Weight")]
        [SerializeField, Min(0.01f)] private float maxReferenceSpeed = 4f;
        [SerializeField] private AnimationCurve speedToIkWeight =
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 1f),
                new Keyframe(0.6f, 1f),
                new Keyframe(1f, 0.2f));

        [Header("Idle IK")]
        [SerializeField, Min(0f)] private float idleSpeedThreshold = 0.08f;
        [SerializeField, Range(0f, 1f)] private float idleIkWeightFloor = 1f;

        [Header("Animation Clip Curve Weight")]
        [SerializeField] private bool useAnimationClipCurveWeight = true;
        [SerializeField] private string footIkCurveParameter = "FootIKWeight";
        [SerializeField, Range(0f, 1f)] private float missingCurveWeightFallback = 1f;

        [Header("Foot Plant Parameters")]
        [SerializeField] private bool useFootPlantParameters;
        [SerializeField] private string leftFootPlantParameter = "LeftFootPlant";
        [SerializeField] private string rightFootPlantParameter = "RightFootPlant";

        [Header("Foot Plant Threshold")]
        [SerializeField, Range(0f, 1f)] private float plantEnterThreshold = 0.65f;
        [SerializeField, Range(0f, 1f)] private float plantExitThreshold = 0.45f;

        [Header("Foot Plant Lock")]
        [SerializeField] private bool lockHorizontalWhenPlanted = true;

        private Vector3 leftFootTargetPosition;
        private Vector3 rightFootTargetPosition;
        private Quaternion leftFootTargetRotation = Quaternion.identity;
        private Quaternion rightFootTargetRotation = Quaternion.identity;

        private float leftFootCurrentWeight;
        private float rightFootCurrentWeight;

        private bool leftFootInitialized;
        private bool rightFootInitialized;

        private int leftFootPlantHash;
        private int rightFootPlantHash;
        private int footIkCurveHash;
        private bool hasLeftFootPlantParameter;
        private bool hasRightFootPlantParameter;
        private bool hasFootIkCurveParameter;

        private bool isLeftFootPlanted;
        private bool isRightFootPlanted;
        private bool isLeftFootXZLocked;
        private bool isRightFootXZLocked;
        private Vector2 leftFootLockedXZ;
        private Vector2 rightFootLockedXZ;


        private void Awake()
        {
            if (animator == null)
            {
                TryGetComponent(out animator);
            }

            if (navMeshAgent == null)
            {
                TryGetComponent(out navMeshAgent);
            }

            CacheAnimatorParameters();
        }

        private void OnEnable()
        {
            CacheAnimatorParameters();
            isLeftFootPlanted = false;
            isRightFootPlanted = false;
            isLeftFootXZLocked = false;
            isRightFootXZLocked = false;
        }

        private void Reset()
        {
            TryGetComponent(out animator);
            TryGetComponent(out navMeshAgent);

            int environmentLayerIndex = LayerMask.NameToLayer("Environment");
            if (environmentLayerIndex >= 0)
            {
                environmentLayer = environmentLayerIndex;
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!CanApplyIK())
            {
                FadeOutIK(Time.deltaTime);
                return;
            }

            float animationCurveWeight = GetAnimationClipCurveWeight();
            float baseTargetWeight = GetBaseTargetWeight() * animationCurveWeight;
            float leftTargetWeight = baseTargetWeight * GetFootPlantWeight(AvatarIKGoal.LeftFoot);
            float rightTargetWeight = baseTargetWeight * GetFootPlantWeight(AvatarIKGoal.RightFoot);

            bool leftPlantedNow = useFootPlantParameters && hasLeftFootPlantParameter && isLeftFootPlanted;
            bool rightPlantedNow = useFootPlantParameters && hasRightFootPlantParameter && isRightFootPlanted;

            UpdateFootIK(
                AvatarIKGoal.LeftFoot,
                ref leftFootTargetPosition,
                ref leftFootTargetRotation,
                ref leftFootCurrentWeight,
                ref leftFootInitialized,
                leftTargetWeight,
                leftPlantedNow,
                ref isLeftFootXZLocked,
                ref leftFootLockedXZ,
                Time.deltaTime);

            UpdateFootIK(
                AvatarIKGoal.RightFoot,
                ref rightFootTargetPosition,
                ref rightFootTargetRotation,
                ref rightFootCurrentWeight,
                ref rightFootInitialized,
                rightTargetWeight,
                rightPlantedNow,
                ref isRightFootXZLocked,
                ref rightFootLockedXZ,
                Time.deltaTime);
        }

        private bool CanApplyIK()
        {
            if (animator == null) return false;
            if (!animator.isHuman) return false;
            return true;
        }

        private float GetBaseTargetWeight()
        {
            float speed = GetCurrentSpeed();
            if (applyOnlyWhenMoving && speed < movingThreshold)
            {
                return 0f;
            }

            float speedWeight = 1f;
            if (speedToIkWeight != null && speedToIkWeight.length > 0)
            {
                float normalizedSpeed = maxReferenceSpeed > Mathf.Epsilon
                    ? Mathf.Clamp01(speed / maxReferenceSpeed)
                    : 0f;
                speedWeight = Mathf.Clamp01(speedToIkWeight.Evaluate(normalizedSpeed));
            }

            float baseWeight = positionWeight * speedWeight;

            if (speed <= idleSpeedThreshold)
            {
                baseWeight = Mathf.Max(baseWeight, positionWeight * idleIkWeightFloor);
            }

            return Mathf.Clamp01(baseWeight);
        }

        private float GetAnimationClipCurveWeight()
        {
            if (!useAnimationClipCurveWeight || animator == null)
            {
                return 1f;
            }

            if (hasFootIkCurveParameter)
            {
                return Mathf.Clamp01(animator.GetFloat(footIkCurveHash));
            }

            return missingCurveWeightFallback;
        }

        private float GetCurrentSpeed()
        {
            if (navMeshAgent != null)
            {
                return navMeshAgent.velocity.magnitude;
            }

            if (animator != null)
            {
                return animator.velocity.magnitude;
            }

            return 0f;
        }

        private float GetFootPlantWeight(AvatarIKGoal goal)
        {
            if (!useFootPlantParameters || animator == null)
            {
                return 1f;
            }

            bool hasParameter;
            int parameterHash;
            bool isPlanted;

            if (goal == AvatarIKGoal.LeftFoot)
            {
                hasParameter = hasLeftFootPlantParameter;
                parameterHash = leftFootPlantHash;
                isPlanted = isLeftFootPlanted;
            }
            else if (goal == AvatarIKGoal.RightFoot)
            {
                hasParameter = hasRightFootPlantParameter;
                parameterHash = rightFootPlantHash;
                isPlanted = isRightFootPlanted;
            }
            else
            {
                return 1f;
            }

            if (!hasParameter)
            {
                return 1f;
            }

            float plantValue = Mathf.Clamp01(animator.GetFloat(parameterHash));
            bool nextPlanted = EvaluatePlantedState(isPlanted, plantValue);

            if (goal == AvatarIKGoal.LeftFoot)
            {
                isLeftFootPlanted = nextPlanted;
            }
            else
            {
                isRightFootPlanted = nextPlanted;
            }

            return nextPlanted ? plantValue : 0f;
        }

        private void UpdateFootIK(
            AvatarIKGoal goal,
            ref Vector3 smoothedPosition,
            ref Quaternion smoothedRotation,
            ref float currentWeight,
            ref bool initialized,
            float targetWeight,
            bool isPlantedNow,
            ref bool isXZLocked,
            ref Vector2 lockedXZ,
            float deltaTime)
        {
            Vector3 animIKPosition = animator.GetIKPosition(goal);
            bool hasGround = TrySampleGround(animIKPosition, out var hit);

            if (!isPlantedNow)
            {
                isXZLocked = false;
            }

            float nextWeight = hasGround ? targetWeight : 0f;
            currentWeight = Mathf.MoveTowards(currentWeight, nextWeight, weightBlendSpeed * deltaTime);

            if (!hasGround || currentWeight <= Mathf.Epsilon)
            {
                SetGoalIK(goal, animIKPosition, animator.GetIKRotation(goal), currentWeight);
                if (!hasGround)
                {
                    initialized = false;
                }

                return;
            }

            Vector3 targetPosition = animIKPosition;
            if (lockHorizontalWhenPlanted && isPlantedNow)
            {
                if (!isXZLocked)
                {
                    lockedXZ = new Vector2(animIKPosition.x, animIKPosition.z);
                    isXZLocked = true;
                }

                targetPosition.x = lockedXZ.x;
                targetPosition.z = lockedXZ.y;
            }
            else
            {
                isXZLocked = false;
                targetPosition.x = animIKPosition.x;
                targetPosition.z = animIKPosition.z;
            }

            targetPosition.y = hit.point.y + footHeightOffset;
            Quaternion targetRotation = ResolveFootRotation(goal, hit.normal);

            if (!initialized)
            {
                smoothedPosition = targetPosition;
                smoothedRotation = targetRotation;
                initialized = true;
            }
            else
            {
                float positionT = 1f - Mathf.Exp(-positionLerpSpeed * deltaTime);
                float rotationT = 1f - Mathf.Exp(-rotationLerpSpeed * deltaTime);

                smoothedPosition.x = targetPosition.x;
                smoothedPosition.z = targetPosition.z;
                smoothedPosition.y = Mathf.Lerp(smoothedPosition.y, targetPosition.y, positionT);
                smoothedRotation = Quaternion.Slerp(smoothedRotation, targetRotation, rotationT);
            }

            SetGoalIK(goal, smoothedPosition, smoothedRotation, currentWeight);
        }

        private bool TrySampleGround(Vector3 footPosition, out RaycastHit hit)
        {
            Vector3 origin = footPosition + Vector3.up * raycastStartHeight;
            float castDistance = raycastStartHeight + raycastDistance;
            int layerMask = ResolveEnvironmentLayerMask();

            return Physics.Raycast(origin, Vector3.down, out hit, castDistance, layerMask, queryTriggerInteraction);
        }

        private int ResolveEnvironmentLayerMask()
        {
            if (environmentLayer < 0 || environmentLayer > 31)
            {
                return Physics.DefaultRaycastLayers;
            }

            return 1 << environmentLayer;
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null)
            {
                hasLeftFootPlantParameter = false;
                hasRightFootPlantParameter = false;
                hasFootIkCurveParameter = false;
                return;
            }

            leftFootPlantHash = string.IsNullOrWhiteSpace(leftFootPlantParameter)
                ? 0
                : Animator.StringToHash(leftFootPlantParameter);
            rightFootPlantHash = string.IsNullOrWhiteSpace(rightFootPlantParameter)
                ? 0
                : Animator.StringToHash(rightFootPlantParameter);
            footIkCurveHash = string.IsNullOrWhiteSpace(footIkCurveParameter)
                ? 0
                : Animator.StringToHash(footIkCurveParameter);

            hasLeftFootPlantParameter = HasFloatParameter(leftFootPlantHash);
            hasRightFootPlantParameter = HasFloatParameter(rightFootPlantHash);
            hasFootIkCurveParameter = HasFloatParameter(footIkCurveHash);
        }

        private bool HasFloatParameter(int parameterHash)
        {
            if (animator == null || parameterHash == 0)
            {
                return false;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.nameHash == parameterHash)
                {
                    return true;
                }
            }

            return false;
        }

        private bool EvaluatePlantedState(bool currentState, float plantValue)
        {
            if (!currentState)
            {
                return plantValue >= plantEnterThreshold;
            }

            if (plantValue <= plantExitThreshold)
            {
                return false;
            }

            return true;
        }

        private Quaternion ResolveFootRotation(AvatarIKGoal goal, Vector3 groundNormal)
        {
            Quaternion animIKRotation = animator.GetIKRotation(goal);
            Quaternion terrainRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
            return terrainRotation * animIKRotation;
        }

        private void SetGoalIK(AvatarIKGoal goal, Vector3 position, Quaternion rotation, float weight)
        {
            if (animator == null) return;

            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight * rotationWeight);

            if (weight > Mathf.Epsilon)
            {
                animator.SetIKPosition(goal, position);
                animator.SetIKRotation(goal, rotation);
            }
        }

        private void FadeOutIK(float deltaTime)
        {
            leftFootCurrentWeight = Mathf.MoveTowards(leftFootCurrentWeight, 0f, weightBlendSpeed * deltaTime);
            rightFootCurrentWeight = Mathf.MoveTowards(rightFootCurrentWeight, 0f, weightBlendSpeed * deltaTime);

            SetGoalIK(AvatarIKGoal.LeftFoot, Vector3.zero, Quaternion.identity, leftFootCurrentWeight);
            SetGoalIK(AvatarIKGoal.RightFoot, Vector3.zero, Quaternion.identity, rightFootCurrentWeight);
        }


        private void OnDrawGizmosSelected()
        {
            if (!drawFootRaycastGizmo)
            {
                return;
            }

            if (animator == null)
            {
                TryGetComponent(out animator);
            }

            if (animator == null || !animator.isHuman)
            {
                return;
            }

            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

            DrawFootRaycastGizmo(leftFoot, leftFootRaycastGizmoColor);
            DrawFootRaycastGizmo(rightFoot, rightFootRaycastGizmoColor);
        }

        private void DrawFootRaycastGizmo(Transform footTransform, Color color)
        {
            if (footTransform == null)
            {
                return;
            }

            Vector3 origin = footTransform.position + Vector3.up * raycastStartHeight;
            float castDistance = raycastStartHeight + raycastDistance;
            Vector3 end = origin + Vector3.down * castDistance;

            Gizmos.color = color;
            Gizmos.DrawLine(origin, end);
            Gizmos.DrawWireSphere(origin, gizmoSphereRadius);
            Gizmos.DrawWireSphere(end, gizmoSphereRadius);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, castDistance, ResolveEnvironmentLayerMask(), queryTriggerInteraction))
            {
                Gizmos.DrawSphere(hit.point, gizmoSphereRadius * 0.8f);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            raycastStartHeight = Mathf.Max(0.01f, raycastStartHeight);
            raycastDistance = Mathf.Max(0.05f, raycastDistance);
            movingThreshold = Mathf.Max(0f, movingThreshold);
            idleSpeedThreshold = Mathf.Max(0f, idleSpeedThreshold);
            idleIkWeightFloor = Mathf.Clamp01(idleIkWeightFloor);
            maxReferenceSpeed = Mathf.Max(0.01f, maxReferenceSpeed);
            missingCurveWeightFallback = Mathf.Clamp01(missingCurveWeightFallback);
            plantEnterThreshold = Mathf.Clamp01(plantEnterThreshold);
            plantExitThreshold = Mathf.Clamp01(plantExitThreshold);
            if (plantExitThreshold > plantEnterThreshold)
            {
                float temp = plantEnterThreshold;
                plantEnterThreshold = plantExitThreshold;
                plantExitThreshold = temp;
            }

            weightBlendSpeed = Mathf.Max(0.01f, weightBlendSpeed);
            positionLerpSpeed = Mathf.Max(0.01f, positionLerpSpeed);
            rotationLerpSpeed = Mathf.Max(0.01f, rotationLerpSpeed);
            gizmoSphereRadius = Mathf.Max(0.005f, gizmoSphereRadius);

            if (speedToIkWeight == null || speedToIkWeight.length == 0)
            {
                speedToIkWeight = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            }

            if (environmentLayer < 0 || environmentLayer > 31)
            {
                int environmentLayerIndex = LayerMask.NameToLayer("Environment");
                if (environmentLayerIndex >= 0)
                {
                    environmentLayer = environmentLayerIndex;
                }
            }

            CacheAnimatorParameters();
        }
#endif
    }
}

