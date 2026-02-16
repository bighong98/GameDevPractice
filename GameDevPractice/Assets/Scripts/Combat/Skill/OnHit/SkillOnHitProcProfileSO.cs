using System.Collections.Generic;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "SkillOnHitProcProfileSO", menuName = "Scriptable Objects/Combat/Skill/OnHit/ProcProfile")]
    public sealed class SkillOnHitProcProfileSO : ScriptableObject
    {
        [SerializeField] private List<SkillTriggerRuleEntry> entries = new();

        public IReadOnlyList<SkillTriggerRuleEntry> Entries => entries;
        public bool HasEntries => entries != null && entries.Exists(entry =>
            entry != null &&
            (entry.HasTriggeredSkills
#if UNITY_EDITOR
             || entry.HasLegacyEffects
#endif
            ));
    }
}
