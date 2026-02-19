#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(
        fileName = "SkillVfxProfileSO",
        menuName = "Scriptable Objects/Combat/Skill/VFX/SkillVfxProfileSO")]
    public sealed class SkillVfxProfileSO : ScriptableObject
    {
        [SerializeField] private List<SkillVFXCue> cues = new();

        public IReadOnlyList<SkillVFXCue> Cues => cues;
        public bool HasCues => cues != null && cues.Exists(cue => cue != null && cue.IsValid);
    }
}
#endif
