using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace TH.Resource
{
    // 게임오브젝트 타입별 데이터 스크립터블 오브젝트의 상위 클래스
    // 모든 타입 데이터 SO의 공통 로직을 포함함
    public abstract class BaseTypeSO : ScriptableObject, ITypeSO
    {
        [Header("Prefab Reference")] [Tooltip("TypeSO and TypeHolder must be pair")]
        public GameObject prefab;
        
        [Header("Basic")]
        public string nameString;
        public Sprite sprite;

        // 비동기 초기화가 필요한 필드가 있는 경우 override해서 사용
        // 해당 필드가 AssetReference 타입이라면 ResourceManager.Instance.ExtractAssetFromRef() 사용
        public virtual UniTask InitializeAsync(CancellationToken token = default)
        {
            Logg.Log($"[{GetType().Name}, {nameString}] InitializeAsync() invoked", Logg.LoggingMode.Completed);
            return UniTask.CompletedTask;
        }
        // 상태 초기화가 필요한 경우 사용 (HasItemUseSfx, etc)
        // 에디터 환경에서 내부적으로 OnValidate() 타이밍에 자동 호출됨
        public virtual void RefreshStates()
        {
            Logg.Log($"[{GetType().Name}, {nameString}] RefreshStates() invoked", Logg.LoggingMode.Completed);
        }

        protected static bool IsAssetRefAssigned(AssetReference a)
        {
            return a.IsAlive() && a.RuntimeKeyIsValid();
        }

        protected static async UniTask<T> GetStateFromAssetReference<T>(AssetReference assetReference, CancellationToken token = default) where T : UnityEngine.Object
        {
            if (!IsAssetRefAssigned(assetReference)) return default;

            return await ResourceManager.Instance.ExtractAssetRefAsync<T>(assetReference, token);
        }

        protected static async UniTask<T> GetStateFromAssetReference<T>(AssetReferenceGeneric<T> assetReference, CancellationToken token = default) where T : UnityEngine.Object
        {
            return await GetStateFromAssetReference<T>((AssetReference)assetReference, token);
        }

        protected static async UniTask<T> GetStateFromAssetReference<T>(AssetReferenceT<T> assetReference, CancellationToken token = default) where T : UnityEngine.Object
        {
            return await GetStateFromAssetReference<T>((AssetReference)assetReference, token);
        }

#if UNITY_EDITOR

        void OnValidate()
        {
            RefreshStates();
        }

#endif
    }
}

#region Deprecated

// [Header("Minimap")]
// public bool showInMinimap;
// public Sprite minimapSprite; // 미니맵용 스프라이트 데이터. 추후 필요하면 사용
// public Color minimapSpriteColor = Color.white; // 미니맵용 스프라이트 컬러 데이터. 추후 불필요해지면 제거

#endregion