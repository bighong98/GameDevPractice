using UnityEngine;

namespace TH.Animate
{
    public interface IAnimatorOverrideHandler
    {
        void OverrideAnimator(object source);
    }

    public interface IAnimatorOverrideHandler<in T> : IAnimatorOverrideHandler
    {
        void OverrideAnimator(T source);
    }
}


