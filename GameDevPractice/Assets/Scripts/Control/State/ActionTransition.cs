// 상태 전이 데이터 정의 스크립트
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Control.Data;
using TH.Core.Service;

using TH.Utils;
using TH.Resource;
using UnityEngine;

namespace TH.Control.State
{
    [Serializable]
    // 상태 전이 조건과 목적 상태를 묶는 전이 데이터 구조체
    public class ActionStateTransition
    {
        public IActionStateCondition Condition => condition; // 조건
        public IActionState DestinationState => destinationState; // 참일 때 변경될 상태
        
        [SerializeField] private AssetReferenceActionStateConditionSO conditionReference;
        [SerializeField] private AssetReferenceActionStateSO destinationStateReference;

        [NonSerialized] private ActionStateConditionSO condition;
        [NonSerialized] private ActionStateSO destinationState;

        public async UniTask InitializeAsync(CancellationToken token = default)
        {
            if (condition == null)
            {
                if (conditionReference == null)
                {
                    Logg.LogWarning("[ActionStateTransition] conditionReference is null");
                }
                else if (!conditionReference.RuntimeKeyIsValid())
                {
                    Logg.LogWarning($"[ActionStateTransition] invalid condition RuntimeKey. guid={conditionReference.AssetGUID}");
                }
                else
                {
                    condition = await ResourceManager.Instance.ExtractAssetRefAsync<ActionStateConditionSO>(conditionReference, token);
                    if (condition == null)
                    {
                        Logg.LogWarning($"[ActionStateTransition] failed to load condition. guid={conditionReference.AssetGUID}");
                    }
                }
            }

            if (destinationState == null)
            {
                if (destinationStateReference == null)
                {
                    Logg.LogWarning("[ActionStateTransition] destinationStateReference is null");
                }
                else if (!destinationStateReference.RuntimeKeyIsValid())
                {
                    Logg.LogWarning($"[ActionStateTransition] invalid destination RuntimeKey. guid={destinationStateReference.AssetGUID}");
                }
                else
                {
                    destinationState = await ResourceManager.Instance.ExtractAssetRefAsync<ActionStateSO>(destinationStateReference, token);
                    if (destinationState == null)
                    {
                        Logg.LogWarning($"[ActionStateTransition] failed to load destination state. guid={destinationStateReference.AssetGUID}");
                    }
                }
            }

            if (destinationState is ActionStateSO destinationStateSo)
            {
                // 순환 초기화 경로에서는 동일 SO의 자기 대기 교착을 피하기 위해 skip
                if (destinationStateSo.IsInitializing)
                {
                    Logg.Log($"[ActionStateTransition] skip destination init because destination is already initializing: {destinationStateSo.name}", Logg.LoggingMode.Completed);
                }
                else
                {
                    await destinationStateSo.InitializeAsync(token);
                }
            }
            else if (destinationState is IAsyncInitializer asyncInitializer)
            {
                await asyncInitializer.InitializeAsync(token);
            }

            if (condition == null || destinationState == null)
            {
                Logg.LogWarning($"[ActionStateTransition] unresolved reference after InitializeAsync. condition={(condition == null ? "null" : condition.name)}, destination={(destinationState == null ? "null" : destinationState.name)}");
            }
        }
    }
}

