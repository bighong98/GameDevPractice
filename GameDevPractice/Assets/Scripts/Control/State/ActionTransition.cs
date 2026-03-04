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
        public IActionStateCondition Condition
        {
            get
            {
                TryInitializeOnce();
                return condition;
            }
        }

        public IActionState DestinationState
        {
            get
            {
                TryInitializeOnce();
                return destinationState;
            }
        }

        [SerializeField] private AssetReferenceActionStateConditionSO conditionReference;
        [SerializeField] private AssetReferenceActionStateSO destinationStateReference;

        [NonSerialized] private ActionStateConditionSO condition;
        [NonSerialized] private ActionStateSO destinationState;
        [NonSerialized] private bool initialized;
        [NonSerialized] private bool initializeAttempted;
        [NonSerialized] private bool initializing;

        public async UniTask InitializeAsync(CancellationToken token = default)
        {
            if (initialized || initializing)
            {
                return;
            }

            initializing = true;
            initializeAttempted = true;
            try
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

                initialized = condition != null && destinationState != null;
                if (!initialized)
                {
                    Logg.LogWarning($"[ActionStateTransition] unresolved reference after InitializeAsync. condition={(condition == null ? "null" : condition.name)}, destination={(destinationState == null ? "null" : destinationState.name)}");
                }
            }
            finally
            {
                initializing = false;
            }
        }

        public bool TryInitializeOnce()
        {
            if (initialized)
            {
                return true;
            }

            if (initializeAttempted || initializing)
            {
                return false;
            }

            initializeAttempted = true;
            initializing = true;
            try
            {
                TryResolveConditionFromCache();
                TryResolveDestinationFromCache();

                initialized = condition != null && destinationState != null;
                if (!initialized)
                {
                    Logg.LogWarning($"[ActionStateTransition] unresolved reference in TryInitializeOnce. conditionGuid={conditionReference?.AssetGUID}, destinationGuid={destinationStateReference?.AssetGUID}");
                }

                return initialized;
            }
            finally
            {
                initializing = false;
            }
        }

        private void TryResolveConditionFromCache()
        {
            if (condition != null)
            {
                return;
            }

            if (conditionReference == null)
            {
                Logg.LogWarning("[ActionStateTransition] conditionReference is null");
                return;
            }

            if (!conditionReference.RuntimeKeyIsValid())
            {
                Logg.LogWarning($"[ActionStateTransition] invalid condition RuntimeKey. guid={conditionReference.AssetGUID}");
                return;
            }

            if (!ResourceManager.Instance.TryLoad(conditionReference, out ActionStateConditionSO loadedCondition) || loadedCondition == null)
            {
                Logg.LogWarning($"[ActionStateTransition] cache miss for condition. guid={conditionReference.AssetGUID}");
                return;
            }

            condition = loadedCondition;
        }

        private void TryResolveDestinationFromCache()
        {
            if (destinationState != null)
            {
                return;
            }

            if (destinationStateReference == null)
            {
                Logg.LogWarning("[ActionStateTransition] destinationStateReference is null");
                return;
            }

            if (!destinationStateReference.RuntimeKeyIsValid())
            {
                Logg.LogWarning($"[ActionStateTransition] invalid destination RuntimeKey. guid={destinationStateReference.AssetGUID}");
                return;
            }

            if (!ResourceManager.Instance.TryLoad(destinationStateReference, out ActionStateSO loadedDestination) || loadedDestination == null)
            {
                Logg.LogWarning($"[ActionStateTransition] cache miss for destination. guid={destinationStateReference.AssetGUID}");
                return;
            }

            destinationState = loadedDestination;
        }
    }
}

