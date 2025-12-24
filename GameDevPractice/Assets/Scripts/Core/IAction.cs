using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Core
{
    public interface IAction
    {
        public CharacterActionScheduler ActionScheduler { get; }
        public void CancelAction();
        
    }
}
