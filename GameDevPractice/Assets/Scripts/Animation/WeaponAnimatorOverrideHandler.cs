using TH.Core;
using UnityEngine;

namespace TH.Animate
{
    public class WeaponAnimatorOverrideHandler : IAnimatorOverrideHandler<WeaponTypeSO>
    {
        private readonly Animator animator;
        
        public WeaponAnimatorOverrideHandler(Animator animator)
        {
            this.animator = animator;
        }
        
        public void OverrideAnimator(WeaponTypeSO source)
        {
            
        }

        public void OverrideAnimator(object source)
        {
            if (source is not WeaponTypeSO weaponData) return;
            OverrideAnimator(weaponData);
        }
    }
}

