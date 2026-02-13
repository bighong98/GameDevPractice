using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TH.Attribute;
using TH.Attribute.Stat;
using TH.Combat.Service;
using TH.Core.Service;
using TH.Item;
using TH.Resource;
using TH.Utils;
using UnityEngine;

namespace TH.Combat
{
    // 에디터 디버그 표시 필드 동기화 파트
    public sealed partial class SkillController
    {
#region For Debug (Editor Only)
#if UNITY_EDITOR
        [Header("Debug")]
        // 현재 활성 스킬 디버그 표시 필드
        [SerializeField] private SkillTypeSO activeSkillDebug;
        // 현재 해석 스킬 디버그 표시 필드
        [SerializeField] private SkillTypeSO resolvedSkillDebug;
        // 현재 콤보 인덱스 디버그 표시 필드
        [SerializeField] private int comboStepIndexDebug;
        // 현재 콤보 단계 수 디버그 표시 필드
        [SerializeField] private int comboStepCountDebug;
        // 활성 스킬 잔여 쿨다운 디버그 표시 필드
        [SerializeField] private float activeSkillRemainCooldownDebug;
#endif

        // 에디터 전용 디버그 필드 동기화
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void SyncDebugValues()
        {
#if UNITY_EDITOR
            activeSkillDebug = ActiveSkill;
            resolvedSkillDebug = ResolvedSkill;
            comboStepIndexDebug = CurrentComboStepIndex;
            comboStepCountDebug = CurrentComboStepCount;
            activeSkillRemainCooldownDebug = HasActiveSkill
                ? skillCaster.GetRemainingCooldown(skillBook.ActiveSkill)
                : 0f;
#endif
        }
#endregion
    }
}
