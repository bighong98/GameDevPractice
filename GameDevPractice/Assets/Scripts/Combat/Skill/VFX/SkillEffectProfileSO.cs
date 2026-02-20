#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(
        fileName = "SkillEffectProfileSO",
        menuName = "Scriptable Objects/Combat/Skill/Effect/SkillEffectProfileSO")]
    public sealed class SkillEffectProfileSO : ScriptableObject
    {
        [SerializeField] private List<SkillEffectCue> cues = new();

        public IReadOnlyList<SkillEffectCue> Cues => cues;
        public bool HasCues => cues != null && cues.Exists(cue => cue != null && cue.IsValid);
    }
}
#endif
