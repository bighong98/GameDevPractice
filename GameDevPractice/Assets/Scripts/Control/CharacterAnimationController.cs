using TH.Combat;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Control
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterAnimationController : MonoBehaviour
    {
        [Header("Default")]
        [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
        [SerializeField] private Animator animator;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] private SkillTypeSO activeSkillDebug;
        [SerializeField] private SkillTypeSO resolvedSkillDebug;
#endif

        private ISkillController skillController;
        private RuntimeAnimatorController baseAnimatorController;

        private void Awake()
        {
            if (animator.IsNull())
                TryGetComponent(out animator);
            TryGetComponent(out skillController);

            CacheBaseAnimatorController();
        }

        private void Start()
        {
            if (skillController.IsNotNull() && skillController.HasResolvedSkill)
            {
                ApplySkillAnimator(skillController.ResolvedSkill);
                return;
            }

            if (skillController.IsNotNull() && skillController.HasActiveSkill)
            {
                ApplySkillAnimator(skillController.ActiveSkill);
                return;
            }

            RestoreBaseAnimator();
        }

        private void OnEnable()
        {
            if (skillController.IsNotNull())
            {
                skillController.OnActiveSkillChanged += HandleActiveSkillChanged;
                skillController.OnResolvedSkillChanged += HandleResolvedSkillChanged;
            }
        }

        private void OnDisable()
        {
            if (skillController.IsNotNull())
            {
                skillController.OnActiveSkillChanged -= HandleActiveSkillChanged;
                skillController.OnResolvedSkillChanged -= HandleResolvedSkillChanged;
            }
        }

        private void HandleActiveSkillChanged(SkillTypeSO skill)
        {
            ApplySkillAnimator(skill);
        }

        private void HandleResolvedSkillChanged(SkillTypeSO skill)
        {
            ApplySkillAnimator(skill);
        }

        public void ApplySkillAnimator(SkillTypeSO skill)
        {
#if UNITY_EDITOR
            SetDebugActiveSkill(skill);
#endif
            if (animator.IsNull()) return;

            if (skill.IsNotNull() && skill.AnimatorOverride.IsNotNull())
            {
                animator.runtimeAnimatorController = skill.AnimatorOverride;
                return;
            }

            RestoreBaseAnimator();
        }

        private void CacheBaseAnimatorController()
        {
            if (defaultAnimatorController.IsNotNull())
            {
                baseAnimatorController = defaultAnimatorController;
                return;
            }

            if (animator.IsNull()) return;

            if (animator.runtimeAnimatorController is AnimatorOverrideController overrideController)
            {
                baseAnimatorController = overrideController.runtimeAnimatorController;
                return;
            }

            baseAnimatorController = animator.runtimeAnimatorController;
        }

        private void RestoreBaseAnimator()
        {
            if (animator.IsNull()) return;

            if (baseAnimatorController.IsNotNull())
            {
                animator.runtimeAnimatorController = baseAnimatorController;
                return;
            }

            if (animator.runtimeAnimatorController is AnimatorOverrideController overrideController)
            {
                animator.runtimeAnimatorController = overrideController.runtimeAnimatorController;
            }
        }

#if UNITY_EDITOR
        private void SetDebugActiveSkill(SkillTypeSO skill)
        {
            activeSkillDebug = skill;
            resolvedSkillDebug = skillController.IsNotNull() ? skillController.ResolvedSkill : null;
        }
#endif
    }
}
