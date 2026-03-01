using TH.Utils;
using UnityEngine;

namespace TH.UI.Data
{
    // 이벤트 타입별 플로팅 텍스트 설정 SO 매핑 카탈로그
    [CreateAssetMenu(fileName = "FloatingTextPrefabCatalogSO", menuName = "Scriptable Objects/UI/FloatingTextPrefabCatalogSO")]
    public class FloatingTextCatalogSO : KeyValueListSO<FloatingTextEventType, FloatingTextSO>
    {
        // KeyValueListSO 동작 재사용 목적 빈 파생 타입
    }
}


