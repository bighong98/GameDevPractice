using System.Collections.Generic;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "SkillOnHitProcProfileSO", menuName = "Scriptable Objects/Combat/Skill/OnHit/ProcProfile")]
    public sealed class SkillOnHitProcProfileSO : ScriptableObject
    {
        [SerializeField] private List<SkillOnHitProcEntry> entries = new();

        public IReadOnlyList<SkillOnHitProcEntry> Entries => entries;
        public bool HasEntries => entries != null && entries.Exists(entry => entry != null && entry.HasEffects);
    }
}