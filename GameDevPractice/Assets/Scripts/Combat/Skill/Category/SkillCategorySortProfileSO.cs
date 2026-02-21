using System;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "SkillCategorySortProfileSO", menuName = "Scriptable Objects/Type/Skill/SkillCategorySortProfileSO")]
    public sealed class SkillCategorySortProfileSO : ScriptableObject
    {
        [Serializable]
        public struct Rule
        {
            public SkillCategory category;
            public int priority;
        }

        [SerializeField] private List<Rule> rules = new()
        {
            new Rule { category = SkillCategory.BasicSkill, priority = 300 },
            new Rule { category = SkillCategory.AdditiveSkill, priority = 200 },
            new Rule { category = SkillCategory.UltimateSkill, priority = 100 }
        };

        public IReadOnlyList<Rule> Rules => rules;
    }
}
