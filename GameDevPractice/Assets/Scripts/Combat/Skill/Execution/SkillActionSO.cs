using System.Collections;
using UnityEngine;

namespace TH.Combat
{
    public abstract class SkillActionSO : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] private float startDelay;
        [SerializeField, Min(1)] private int repeatCount = 1;
        [SerializeField, Min(0f)] private float repeatInterval;

        public IEnumerator Execute(SkillExecutionContext context, ISkillExecutionServices services)
        {
            if (startDelay > 0f)
            {
                yield return new WaitForSeconds(startDelay);
            }

            int resolvedRepeatCount = Mathf.Max(1, repeatCount);
            for (int i = 0; i < resolvedRepeatCount; i++)
            {
                ExecuteOnce(context, services, i, resolvedRepeatCount);
                if (i + 1 < resolvedRepeatCount && repeatInterval > 0f)
                {
                    yield return new WaitForSeconds(repeatInterval);
                }
            }
        }

        protected abstract void ExecuteOnce(SkillExecutionContext context, ISkillExecutionServices services, int iteration, int totalIterations);
    }
}
