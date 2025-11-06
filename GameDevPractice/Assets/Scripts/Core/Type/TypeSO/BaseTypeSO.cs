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
    }
}

#region Deprecated

// [Header("Minimap")]
// public bool showInMinimap;
// public Sprite minimapSprite; // 미니맵용 스프라이트 데이터. 추후 필요하면 사용
// public Color minimapSpriteColor = Color.white; // 미니맵용 스프라이트 컬러 데이터. 추후 불필요해지면 제거

#endregion