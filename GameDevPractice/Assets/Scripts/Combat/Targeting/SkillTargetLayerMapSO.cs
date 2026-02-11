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

            public int CasterLayer => casterLayer;

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
        }

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
    }
}
