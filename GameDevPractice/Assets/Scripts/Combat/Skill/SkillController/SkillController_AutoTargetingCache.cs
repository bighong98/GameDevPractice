using TH.Attribute;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    // 자동 타겟 후보 캐시/재탐색 파트
    public sealed partial class SkillController
    {
        private const int AutoTargetScanCapacity = 32;

        public bool TryRefreshAutoTargetCandidate(IAttacker attacker)
        {
            autoTargetCache.Invalidate();

            if (!TryFindBestAutoTargetCandidate(attacker, out Health bestTarget))
            {
                return false;
            }

            autoTargetCache.SetCandidate(bestTarget);
            return true;
        }

        public bool TryGetValidAutoTargetCandidate(IAttacker attacker, out Health target)
        {
            target = null;
            if (!autoTargetCache.TryGetCandidate(out Health cachedTarget))
            {
                return false;
            }

            if (!CanUseActiveSkillOnTarget(attacker, cachedTarget))
            {
                autoTargetCache.Invalidate();
                return false;
            }

            target = cachedTarget;
            return true;
        }

        public bool TryGetValidOrRefreshAutoTargetCandidate(IAttacker attacker, out Health target)
        {
            if (TryGetValidAutoTargetCandidate(attacker, out target))
            {
                return true;
            }

            if (!TryRefreshAutoTargetCandidate(attacker))
            {
                target = null;
                return false;
            }

            return TryGetValidAutoTargetCandidate(attacker, out target);
        }

        private bool TryFindBestAutoTargetCandidate(IAttacker attacker, out Health bestTarget)
        {
            bestTarget = null;
            if (!TryBuildActiveSkillTargetingContext(attacker, out var context, out var originPos, out float range))
            {
                return false;
            }

            int layerMask = ResolveTargetLayerMask(context);
            if (layerMask == 0)
            {
                return false;
            }

            Collider[] scanBuffer = autoTargetCache.GetOverlapBuffer(AutoTargetScanCapacity);
            int hitCount = Physics.OverlapSphereNonAlloc(
                originPos,
                range,
                scanBuffer,
                layerMask,
                QueryTriggerInteraction.Ignore);

            float minDistanceSqr = float.MaxValue;
            int scanCount = Mathf.Min(hitCount, scanBuffer.Length);
            for (int i = 0; i < scanCount; i++)
            {
                var collider = scanBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                if (!collider.TryGetComponent<Health>(out var health))
                {
                    continue;
                }

                if (!CanUseActiveSkillOnTarget(context, originPos, range, health))
                {
                    continue;
                }

                float distSqr = (health.transform.position - originPos).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    bestTarget = health;
                }
            }

            return bestTarget.IsNotNull();
        }
    }
}
