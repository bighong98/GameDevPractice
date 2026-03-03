// 상태 전이 데이터 정의 스크립트
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Control.Data;
using TH.Core.Service;
using TH.Resource;
using UnityEngine;

namespace TH.Control.State
{
    [Serializable]
    // 상태 전이 조건과 목적 상태를 묶는 전이 데이터 구조체
    public struct ActionStateTransition
    {
        public IActionStateCondition Condition => condition; // 조건
        public IActionState DestinationState => destinationState;        // 참일 때 갈 상태
        
        [SerializeField] private ActionStateConditionSO condition;
        [SerializeField] private AssetReferenceActionStateConditionSO conditionReference;
        [SerializeField] private ActionStateSO destinationState;
        [SerializeField] private AssetReferenceActionStateSO destinationStateReference;

        public async UniTask InitializeAsync(CancellationToken token = default)
        {
            if (condition == null && conditionReference != null && conditionReference.RuntimeKeyIsValid())
            {
                condition = await ResourceManager.Instance.ExtractAssetRefAsync<ActionStateConditionSO>(conditionReference, token);
            }

            if (destinationState == null && destinationStateReference != null && destinationStateReference.RuntimeKeyIsValid())
            {
                destinationState = await ResourceManager.Instance.ExtractAssetRefAsync<ActionStateSO>(destinationStateReference, token);
            }

            if (destinationState is IAsyncInitializer asyncInitializer)
            {
                await asyncInitializer.InitializeAsync(token);
            }
        }
    }
}

