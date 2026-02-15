using System;
using System.Collections.Generic;
using TH.Attribute;

namespace TH.Combat.Service
{
    public sealed class SkillOnHitProcService : ISkillOnHitProcService
    {
        private const int MaxOncePerAttackCacheSize = 4096;

        private readonly ICombatSystem combatSystem;
        private readonly HashSet<ProcEntryExecutionKey> oncePerAttackCache = new();
        private readonly Queue<ProcEntryExecutionKey> oncePerAttackOrder = new();

        public SkillOnHitProcService(ICombatSystem combatSystem)
        {
            this.combatSystem = combatSystem;

            if (combatSystem != null)
            {
                combatSystem.OnHitApplied += HandleHitApplied;
            }
        }

        private void HandleHitApplied(in HitResult result, IDamageable target)
        {
            if (result.Skill == null || result.Attacker.IsNull())
            {
                return;
            }

            if (target is not Health primaryTarget)
            {
                return;
            }

            var profile = result.Skill.OnHitProcProfile;
            if (profile == null || !profile.HasEntries)
            {
                return;
            }

            var context = new SkillOnHitProcContext(result, primaryTarget);
            ExecuteProfile(profile, context);
        }

        private void ExecuteProfile(SkillOnHitProcProfileSO profile, in SkillOnHitProcContext context)
        {
            var entries = profile.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.HasEffects)
                {
                    continue;
                }

                if (entry.OncePerAttackInstance &&
                    !TryPassOncePerAttackGuard(context.AttackInstanceId, profile.GetInstanceID(), i))
                {
                    continue;
                }

                if (!entry.Evaluate(context))
                {
                    continue;
                }

                entry.Execute(context, combatSystem);
            }
        }

        private bool TryPassOncePerAttackGuard(int attackInstanceId, int profileInstanceId, int entryIndex)
        {
            if (attackInstanceId <= 0)
            {
                return true;
            }

            var key = new ProcEntryExecutionKey(attackInstanceId, profileInstanceId, entryIndex);
            if (!oncePerAttackCache.Add(key))
            {
                return false;
            }

            oncePerAttackOrder.Enqueue(key);
            TrimOncePerAttackCache();
            return true;
        }

        private void TrimOncePerAttackCache()
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

        private readonly struct ProcEntryExecutionKey : IEquatable<ProcEntryExecutionKey>
        {
            private readonly int attackInstanceId;
            private readonly int profileInstanceId;
            private readonly int entryIndex;

            public ProcEntryExecutionKey(int attackInstanceId, int profileInstanceId, int entryIndex)
            {
                this.attackInstanceId = attackInstanceId;
                this.profileInstanceId = profileInstanceId;
                this.entryIndex = entryIndex;
            }

            public bool Equals(ProcEntryExecutionKey other)
            {
                return attackInstanceId == other.attackInstanceId &&
                       profileInstanceId == other.profileInstanceId &&
                       entryIndex == other.entryIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is ProcEntryExecutionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = attackInstanceId;
                    hash = (hash * 397) ^ profileInstanceId;
                    hash = (hash * 397) ^ entryIndex;
                    return hash;
                }
            }
        }
    }
}