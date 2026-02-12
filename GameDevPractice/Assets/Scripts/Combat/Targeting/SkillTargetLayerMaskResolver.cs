using UnityEngine;

namespace TH.Combat
{
    public sealed class SkillTargetLayerMaskResolver
    {
        private const string AllyLayerName = "Ally";
        private const string EnemyLayerName = "Enemy";
        private const string NeutralLayerName = "Neutral";
        private const string ObjectLayerName = "Object";
        private const string AllyAttackLayerName = "AllyAttack";
        private const string EnemyAttackLayerName = "EnemyAttack";

        private readonly SkillTargetLayerMapSO layerMap;

        public SkillTargetLayerMaskResolver(SkillTargetLayerMapSO layerMap)
        {
            this.layerMap = layerMap;
        }

        public int Resolve(IAttacker attacker, in SkillTargetPolicy policy)
        {
            if (attacker is not Component attackerComponent)
                return 0;

            return Resolve(attackerComponent.gameObject.layer, policy.AllowedGroups);
        }

        public int ResolveProjectileLayer(IAttacker attacker)
        {
            if (attacker is not Component attackerComponent)
                return -1;

            return ResolveProjectileLayer(attackerComponent.gameObject.layer);
        }

        public int Resolve(int casterLayer, SkillTargetGroup groups)
        {
            if (groups == SkillTargetGroup.None)
                return 0;

            if (casterLayer < 0 || casterLayer > 31)
                return 0;

            if (layerMap != null && layerMap.TryGetMapping(casterLayer, out var mapping))
                return mapping.ResolveMask(groups);

            return ResolveByConvention(casterLayer, groups);
        }

        public int ResolveProjectileLayer(int casterLayer)
        {
            if (casterLayer < 0 || casterLayer > 31)
                return -1;

            if (layerMap != null && layerMap.TryResolveProjectileLayer(casterLayer, out var projectileLayer))
                return projectileLayer;

            return ResolveProjectileLayerByConvention(casterLayer);
        }

        private static int ResolveByConvention(int casterLayer, SkillTargetGroup groups)
        {
            int mask = 0;

            if ((groups & SkillTargetGroup.Ally) != 0)
                mask |= 1 << casterLayer;

            if ((groups & SkillTargetGroup.Enemy) != 0)
            {
                int enemyLayer = ResolveEnemyLayer(casterLayer);
                if (enemyLayer >= 0)
                    mask |= 1 << enemyLayer;
            }

            if ((groups & SkillTargetGroup.Neutral) != 0)
                AddNamedLayerToMask(NeutralLayerName, ref mask);

            if ((groups & SkillTargetGroup.Object) != 0)
                AddNamedLayerToMask(ObjectLayerName, ref mask);

            return mask;
        }

        private static int ResolveEnemyLayer(int casterLayer)
        {
            string casterLayerName = LayerMask.LayerToName(casterLayer);
            if (string.Equals(casterLayerName, AllyLayerName, System.StringComparison.Ordinal))
                return LayerMask.NameToLayer(EnemyLayerName);

            if (string.Equals(casterLayerName, EnemyLayerName, System.StringComparison.Ordinal))
                return LayerMask.NameToLayer(AllyLayerName);

            return -1;
        }

        private static int ResolveProjectileLayerByConvention(int casterLayer)
        {
            string casterLayerName = LayerMask.LayerToName(casterLayer);
            if (string.Equals(casterLayerName, AllyLayerName, System.StringComparison.Ordinal))
                return ResolveExistingLayerOrFallback(AllyAttackLayerName, casterLayer);

            if (string.Equals(casterLayerName, EnemyLayerName, System.StringComparison.Ordinal))
                return ResolveExistingLayerOrFallback(EnemyAttackLayerName, casterLayer);

            return casterLayer;
        }

        private static void AddNamedLayerToMask(string layerName, ref int mask)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
                return;

            mask |= 1 << layer;
        }

        private static int ResolveExistingLayerOrFallback(string layerName, int fallbackLayer)
        {
            int layer = LayerMask.NameToLayer(layerName);
            return layer >= 0 ? layer : fallbackLayer;
        }
    }
}
