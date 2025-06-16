using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Core
{
    public interface IAction
    {
        public ActoinScheduler ActionScheduler { get; }
        public void Cancel();
        
    }
}
