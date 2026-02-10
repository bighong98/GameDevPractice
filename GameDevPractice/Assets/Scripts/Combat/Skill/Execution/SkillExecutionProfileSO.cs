using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Combat
{
    [CreateAssetMenu(fileName = "SkillExecutionProfileSO", menuName = "Scriptable Objects/Combat/Skill/ExecutionProfileSO")]
    public class SkillExecutionProfileSO : ScriptableObject
    {
        [SerializeField] private List<SkillActionSO> actions = new();

        public bool HasActions => actions != null && actions.Count > 0;
        public IReadOnlyList<SkillActionSO> Actions => actions;

        public IEnumerator Execute(SkillExecutionContext context, ISkillExecutionServices services)
        {
            if (!HasActions)
            {
                yield break;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                yield return action.Execute(context, services);
            }
        }
    }
}
