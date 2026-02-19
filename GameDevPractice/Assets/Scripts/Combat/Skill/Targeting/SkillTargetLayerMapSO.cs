using System;
using System.Collections.Generic;
using TH.Editor;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "SkillTargetLayerMapSO", menuName = "Scriptable Objects/Combat/Targeting/SkillTargetLayerMapSO")]
    public class SkillTargetLayerMapSO : ScriptableObject
    {
        [SerializeField] private List<CasterLayerMapping> casterLayerMappings = new();

        [Serializable]
        public struct CasterLayerMapping
        {
            [SerializeField, Layer] private int casterLayer;
            [SerializeField] private LayerMask allyLayers;
            [SerializeField] private LayerMask enemyLayers;
            [SerializeField] private LayerMask neutralLayers;
            [SerializeField] private LayerMask objectLayers;
            [SerializeField, Layer] private int projectileLayer;

            public int CasterLayer => casterLayer;
            public int ProjectileLayer => projectileLayer;


            public int ResolveMask(SkillTargetGroup groups)
            {
                int mask = 0;

                if ((groups & SkillTargetGroup.Ally) != 0)
                    mask |= allyLayers.value;

                if ((groups & SkillTargetGroup.Enemy) != 0)
                    mask |= enemyLayers.value;

                if ((groups & SkillTargetGroup.Neutral) != 0)
                    mask |= neutralLayers.value;

                if ((groups & SkillTargetGroup.Object) != 0)
                    mask |= objectLayers.value;

                return mask;
            }

            public bool TryResolveProjectileLayer(out int layer)
            {
                if (projectileLayer <= 0 || projectileLayer > 31)
                {
                    layer = -1;
                    return false;
                }

                layer = projectileLayer;
                return true;
            }
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateMappings();
        }

        private void ValidateMappings()
        {
            var seenCasterLayers = new HashSet<int>();

            for (int i = 0; i < casterLayerMappings.Count; i++)
            {
                var mapping = casterLayerMappings[i];

                if (mapping.CasterLayer < 0 || mapping.CasterLayer > 31)
                {
                    Debug.LogWarning($"[{nameof(SkillTargetLayerMapSO)}:{name}] casterLayerMappings[{i}] has invalid casterLayer '{mapping.CasterLayer}'.", this);
                }

                if (!seenCasterLayers.Add(mapping.CasterLayer))
                {
                    Debug.LogWarning($"[{nameof(SkillTargetLayerMapSO)}:{name}] casterLayerMappings[{i}] duplicates casterLayer '{mapping.CasterLayer}'.", this);
                }

                if (mapping.ProjectileLayer == 0)
                {
                    Debug.LogWarning($"[{nameof(SkillTargetLayerMapSO)}:{name}] casterLayerMappings[{i}] projectileLayer is 0(Default). Set a dedicated projectile layer.", this);
                    continue;
                }

                if (mapping.ProjectileLayer < 0 || mapping.ProjectileLayer > 31)
                {
                    Debug.LogWarning($"[{nameof(SkillTargetLayerMapSO)}:{name}] casterLayerMappings[{i}] has invalid projectileLayer '{mapping.ProjectileLayer}'.", this);
                }
            }
        }
#endif

        public bool TryGetMapping(int casterLayer, out CasterLayerMapping mapping)
        {
            for (int i = 0; i < casterLayerMappings.Count; i++)
            {
                var current = casterLayerMappings[i];
                if (current.CasterLayer != casterLayer)
                    continue;

                mapping = current;
                return true;
            }

            mapping = default;
            return false;
        }

        public bool TryResolveProjectileLayer(int casterLayer, out int projectileLayer)
        {
            if (TryGetMapping(casterLayer, out var mapping) &&
                mapping.TryResolveProjectileLayer(out projectileLayer))
            {
                return true;
            }

            projectileLayer = -1;
            return false;
        }
    }
}
