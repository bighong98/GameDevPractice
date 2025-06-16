using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Core
{
    public class ActoinScheduler : MonoBehaviour
    {
        private IAction currentAction;
        
        public void StartAction(IAction action)
        {
            if (currentAction == action) return;
            
            currentAction?.Cancel();
            currentAction = action;
        }

        public void CancelCurrentAction()
        {
            StartAction(null);
        }
    }
}
