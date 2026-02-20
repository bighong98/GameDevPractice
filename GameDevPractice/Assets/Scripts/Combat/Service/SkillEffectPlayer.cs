using System;
using System.Collections.Generic;
using TH.Core.Service;
using TH.Item;
using TH.Resource;
using UnityEngine;

namespace TH.Combat.Service
{
    public static class SkillEffectPlayer
    {
        private const int MaxOncePerAttackCacheSize = 4096;
        private const string DefaultGroundLayerName = "Environment";

        private static readonly HashSet<EffectExecutionKey> oncePerAttackCache = new();
        private static readonly Queue<EffectExecutionKey> oncePerAttackOrder = new();
        private static readonly List<MonoBehaviour> anchorProvidersBuffer = new(8);

        public static bool TryPlaySkillEffect(
            SkillTypeSO skill,
            SkillEffectTrigger trigger,
            in SkillEffectPlayContext context,
            string markerName = null)
        {
            if (skill == null || !skill.HasSkillVfxCues)
            {
                return false;
            }

            var cues = skill.SkillVfxCues;
            if (cues == null || cues.Count == 0)
            {
                return false;
            }

            bool anyPlayed = false;
            int skillInstanceId = skill.GetInstanceID();

            for (int i = 0; i < cues.Count; i++)
            {
                var cue = cues[i];
                if (!IsCueMatch(cue, trigger, markerName))
                {
                    continue;
                }

                if (cue.OncePerAttackInstance &&
                    !TryPassOncePerAttackGuard(context.AttackInstanceId, skillInstanceId, cue.CueId, i))
                {
                    continue;
                }

                if (TryPlayCue(cue, in context, out _))
                {
                    anyPlayed = true;
                }
            }

            return anyPlayed;
        }

        private static bool IsCueMatch(SkillEffectCue cue, SkillEffectTrigger trigger, string markerName)
        {
            if (cue == null || !cue.IsValid)
            {
                return false;
            }

            if (cue.Trigger != trigger)
            {
                return false;
            }

            if (trigger != SkillEffectTrigger.OnAnimMarker)
            {
                return true;
            }

            string configuredMarker = cue.MarkerName ?? string.Empty;
            string runtimeMarker = markerName ?? string.Empty;
            return string.Equals(configuredMarker, runtimeMarker, StringComparison.Ordinal);
        }

        private static bool TryPlayCue(SkillEffectCue cue, in SkillEffectPlayContext context, out bool playedSfx)
        {
            playedSfx = TryPlaySfxCue(cue);
            bool playedVfx = TryPlayVfxCue(cue, in context);
            return playedSfx || playedVfx;
        }

        private static bool TryPlayVfxCue(SkillEffectCue cue, in SkillEffectPlayContext context)
        {
            if (cue == null || cue.EffectPrefab == null)
            {
                return false;
            }

            if (!TryResolveSpawnPose(cue, in context, out var anchorTransform, out var position, out var rotation))
            {
                return false;
            }

            position += cue.PositionOffset;

            if (cue.Follow && anchorTransform != null)
            {
                var attached = PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(
                    cue.EffectPrefab,
                    anchorTransform,
                    worldPositionStays: true);
                if (attached == null)
                {
                    return false;
                }

                attached.transform.SetPositionAndRotation(position, rotation);
                return true;
            }

            var detached = PoolManager.Instance.GetFromPool<SimplePooledParticlePlayer>(
                cue.EffectPrefab,
                null,
                position);
            if (detached == null)
            {
                return false;
            }

            detached.transform.rotation = rotation;
            return true;
        }

        private static bool TryPlaySfxCue(SkillEffectCue cue)
        {
            if (cue == null || cue.SfxClip == null || SoundManager.Instance == null)
            {
                return false;
            }

            SoundManager.Instance.Play(Enums.AudioType.Effect, cue.SfxClip);
            return true;
        }

        private static bool TryResolveSpawnPose(
            SkillEffectCue cue,
            in SkillEffectPlayContext context,
            out Transform anchorTransform,
            out Vector3 position,
            out Quaternion rotation)
        {
            anchorTransform = null;
            position = default;
            rotation = Quaternion.identity;

            switch (cue.AnchorType)
            {
                case SkillEffectAnchorType.CharacterRoot:
                    if (context.AttackerComponent == null)
                    {
                        return false;
                    }

                    anchorTransform = context.AttackerComponent.transform;
                    position = anchorTransform.position;
                    rotation = anchorTransform.rotation;
                    return true;

                case SkillEffectAnchorType.WeaponSocket:
                    if (!TryResolveWeaponAnchor(context.AttackerComponent, cue.AnchorName, out anchorTransform))
                    {
                        if (context.AttackerComponent == null)
                        {
                            return false;
                        }

                        anchorTransform = context.AttackerComponent.transform;
                    }

                    position = anchorTransform.position;
                    rotation = anchorTransform.rotation;
                    return true;

                case SkillEffectAnchorType.GroundUnderCharacter:
                    if (context.AttackerComponent == null)
                    {
                        return false;
                    }

                    anchorTransform = context.AttackerComponent.transform;
                    position = ResolveGroundPosition(cue, anchorTransform.position);
                    rotation = anchorTransform.rotation;
                    return true;

                case SkillEffectAnchorType.HitPoint:
                    if (context.HasHitPoint)
                    {
                        position = context.HitPoint;
                        rotation = context.TargetComponent != null
                            ? context.TargetComponent.transform.rotation
                            : Quaternion.identity;
                        return true;
                    }

                    if (!TryResolveTargetCenter(context.TargetComponent, out position))
                    {
                        return false;
                    }

                    rotation = context.TargetComponent != null
                        ? context.TargetComponent.transform.rotation
                        : Quaternion.identity;
                    return true;

                case SkillEffectAnchorType.TargetCenter:
                    if (!TryResolveTargetCenter(context.TargetComponent, out position))
                    {
                        return false;
                    }

                    anchorTransform = context.TargetComponent != null
                        ? context.TargetComponent.transform
                        : null;
                    rotation = anchorTransform != null ? anchorTransform.rotation : Quaternion.identity;
                    return true;
            }

            return false;
        }

        private static bool TryResolveWeaponAnchor(Component attackerComponent, string anchorName, out Transform anchor)
        {
            anchor = null;
            if (attackerComponent == null || string.IsNullOrWhiteSpace(anchorName))
            {
                return false;
            }

            anchorProvidersBuffer.Clear();
            anchorProvidersBuffer.AddRange(attackerComponent.GetComponentsInChildren<MonoBehaviour>(true));
            for (int i = 0; i < anchorProvidersBuffer.Count; i++)
            {
                if (anchorProvidersBuffer[i] is IAnchorProvider provider &&
                    provider.TryGetAnchor(anchorName, out anchor))
                {
                    return anchor != null;
                }
            }

            return false;
        }

        private static Vector3 ResolveGroundPosition(SkillEffectCue cue, Vector3 playerPosition)
        {
            int layerMask = ResolveGroundLayerMask(cue);
            var rayStart = playerPosition + Vector3.up * cue.GroundRayStartHeight;
            if (Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out var hit,
                    cue.GroundRayDistance,
                    layerMask,
                    QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return playerPosition;
        }

        private static int ResolveGroundLayerMask(SkillEffectCue cue)
        {
            if (cue.GroundLayerMask.value != 0)
            {
                return cue.GroundLayerMask.value;
            }

            int environmentMask = LayerMask.GetMask(DefaultGroundLayerName);
            return environmentMask != 0 ? environmentMask : Physics.AllLayers;
        }

        private static bool TryResolveTargetCenter(Component targetComponent, out Vector3 center)
        {
            center = default;
            if (targetComponent == null)
            {
                return false;
            }

            if (targetComponent.TryGetComponent<Collider>(out var collider))
            {
                center = collider.bounds.center;
                return true;
            }

            if (targetComponent.TryGetComponent<Renderer>(out var renderer))
            {
                center = renderer.bounds.center;
                return true;
            }

            center = targetComponent.transform.position;
            return true;
        }

        private static bool TryPassOncePerAttackGuard(
            int attackInstanceId,
            int skillInstanceId,
            string cueId,
            int cueIndex)
        {
            if (attackInstanceId <= 0)
            {
                return true;
            }

            int cueIdHash = string.IsNullOrWhiteSpace(cueId) ? 0 : cueId.GetHashCode();
            var key = new EffectExecutionKey(attackInstanceId, skillInstanceId, cueIdHash, cueIndex);
            if (!oncePerAttackCache.Add(key))
            {
                return false;
            }

            oncePerAttackOrder.Enqueue(key);
            TrimOncePerAttackCache();
            return true;
        }

        private static void TrimOncePerAttackCache()
        {
            while (oncePerAttackOrder.Count > MaxOncePerAttackCacheSize)
            {
                if (!oncePerAttackOrder.TryDequeue(out var oldest))
                {
                    break;
                }

                oncePerAttackCache.Remove(oldest);
            }
        }

        private readonly struct EffectExecutionKey : IEquatable<EffectExecutionKey>
        {
            private readonly int attackInstanceId;
            private readonly int skillInstanceId;
            private readonly int cueIdHash;
            private readonly int cueIndex;

            public EffectExecutionKey(int attackInstanceId, int skillInstanceId, int cueIdHash, int cueIndex)
            {
                this.attackInstanceId = attackInstanceId;
                this.skillInstanceId = skillInstanceId;
                this.cueIdHash = cueIdHash;
                this.cueIndex = cueIndex;
            }

            public bool Equals(EffectExecutionKey other)
            {
                return attackInstanceId == other.attackInstanceId &&
                       skillInstanceId == other.skillInstanceId &&
                       cueIdHash == other.cueIdHash &&
                       cueIndex == other.cueIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is EffectExecutionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = attackInstanceId;
                    hash = (hash * 397) ^ skillInstanceId;
                    hash = (hash * 397) ^ cueIdHash;
                    hash = (hash * 397) ^ cueIndex;
                    return hash;
                }
            }
        }
    }
}
