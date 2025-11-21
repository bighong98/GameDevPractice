using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TH.Core
{
    public interface IAction
    {
        public ActoinScheduler ActionScheduler { get; }
        public void Cancel();
        
    }
}
