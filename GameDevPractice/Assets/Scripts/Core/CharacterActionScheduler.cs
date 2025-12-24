using System;
using UnityEngine;

namespace TH.Core
{
    [RequireComponent(typeof(Animator))]
    public class CharacterActionScheduler : MonoBehaviour
    {
        [SerializeField] Animator animator;
        private IAction currentAction;

        #region Chracter Animation Hash

        // private static readonly int DieAnimHash = Animator.StringToHash("die");

        #endregion

        #region Character Animation Event Handle

        void OnDead()
        {
            // animator.SetTrigger(DieAnimHash);
        }

        void OnRevived()
        {
            
        }
        
        #endregion

        private void Awake()
        {
            // if (animator == null)
            //     TryGetComponent(out animator);
        }

        private void OnEnable()
        {
            
        }

        private void OnDisable()
        {
            
        }

        public void StartAction(IAction action)
        {
            if (currentAction == action) return;
            
            currentAction?.CancelAction();
            currentAction = action;
        }

        public void CancelCurrentAction()
        {
            StartAction(null);
        }
    }
}
