using System;
using System.Collections.Generic;
using TH.Attribute;
using TH.Utils;

namespace TH.Combat.Service
{
    public sealed class SkillOnHitTriggerService : ISkillOnHitTriggerService
    {
        private const int MaxOncePerAttackCacheSize = 4096;

        private readonly ICombatSystem combatSystem;
        private readonly HashSet<ProcEntryExecutionKey> oncePerAttackCache = new();
        private readonly Queue<ProcEntryExecutionKey> oncePerAttackOrder = new();

        public SkillOnHitTriggerService(ICombatSystem combatSystem)
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
                Logg.Log(
                    $"[{nameof(SkillOnHitTriggerService)}] skip hit. reason=invalid_result, skill={(result.Skill == null ? "null" : result.Skill.name)}",
                    Logg.LoggingMode.Completed);
                return;
            }

            if (target is not Health primaryTarget)
            {
                Logg.Log(
                    $"[{nameof(SkillOnHitTriggerService)}] skip hit. reason=target_not_health, skill={result.Skill.name}",
                    Logg.LoggingMode.Completed);
                return;
            }

            var entries = result.Skill.OnHitProcEntries;
            if (entries == null || entries.Count == 0 || !result.Skill.HasOnHitProcEntries)
            {
                Logg.Log(
                    $"[{nameof(SkillOnHitTriggerService)}] no on-hit entries. skill={result.Skill.name}, attackId={result.AttackInstanceId}",
                    Logg.LoggingMode.Completed);
                return;
            }

            Logg.Log(
                $"[{nameof(SkillOnHitTriggerService)}] process on-hit. skill={result.Skill.name}, attackId={result.AttackInstanceId}, entryCount={entries.Count}",
                Logg.LoggingMode.Completed);
            var context = new SkillOnHitProcContext(result, primaryTarget);
            ExecuteEntries(entries, result.Skill.GetInstanceID(), context);
        }

        private void ExecuteEntries(IReadOnlyList<SkillTriggerRuleEntry> entries, int skillInstanceId, in SkillOnHitProcContext context)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.HasTriggeredSkills)
                {
                    Logg.Log(
                        $"[{nameof(SkillOnHitTriggerService)}] entry skipped. reason=empty_entry, index={i}",
                        Logg.LoggingMode.Completed);
                    continue;
                }

                if (entry.OncePerAttackInstance &&
                    !TryPassOncePerAttackGuard(context.AttackInstanceId, skillInstanceId, i))
                {
                    Logg.Log(
                        $"[{nameof(SkillOnHitTriggerService)}] entry skipped. reason=once_per_attack_guard, index={i}, attackId={context.AttackInstanceId}",
                        Logg.LoggingMode.Completed);
                    continue;
                }

                if (!entry.Evaluate(context))
                {
                    Logg.Log(
                        $"[{nameof(SkillOnHitTriggerService)}] entry skipped. reason=condition_failed, index={i}",
                        Logg.LoggingMode.Completed);
                    continue;
                }

                Logg.Log(
                    $"[{nameof(SkillOnHitTriggerService)}] entry execute. index={i}, attackId={context.AttackInstanceId}",
                    Logg.LoggingMode.Completed);
                entry.Execute(context, combatSystem);
            }
        }

        private bool TryPassOncePerAttackGuard(int attackInstanceId, int skillInstanceId, int entryIndex)
        {
            if (attackInstanceId <= 0)
            {
                return true;
            }

            var key = new ProcEntryExecutionKey(attackInstanceId, skillInstanceId, entryIndex);
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
            private readonly int skillInstanceId;
            private readonly int entryIndex;

            public ProcEntryExecutionKey(int attackInstanceId, int skillInstanceId, int entryIndex)
            {
                this.attackInstanceId = attackInstanceId;
                this.skillInstanceId = skillInstanceId;
                this.entryIndex = entryIndex;
            }

            public bool Equals(ProcEntryExecutionKey other)
            {
                return attackInstanceId == other.attackInstanceId &&
                       skillInstanceId == other.skillInstanceId &&
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
                    hash = (hash * 397) ^ skillInstanceId;
                    hash = (hash * 397) ^ entryIndex;
                    return hash;
                }
            }
        }
    }
}
