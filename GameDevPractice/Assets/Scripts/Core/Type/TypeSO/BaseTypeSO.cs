using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

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
            return UniTask.CompletedTask;
        }
    }
}

#region Deprecated

// [Header("Minimap")]
// public bool showInMinimap;
// public Sprite minimapSprite; // 미니맵용 스프라이트 데이터. 추후 필요하면 사용
// public Color minimapSpriteColor = Color.white; // 미니맵용 스프라이트 컬러 데이터. 추후 불필요해지면 제거

#endregion