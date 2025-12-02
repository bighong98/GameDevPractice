using UnityEngine;
using UnityEditor;

public class ManualScaleInventoryUI
{
    [MenuItem("Tools/Scale Inventory UI to 80%")]
    public static void ScaleInventoryUI()
    {
        try
        {
            string originalPath = "Assets/Game/UI/Popup/InventoryUI.prefab";
            string newPath = "Assets/Game/UI/Popup/InventoryUI_Small.prefab";
            
            // 원본 프리팹이 존재하는지 확인
            GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(originalPath);
            if (originalPrefab == null)
            {
                Debug.LogError($"원본 프리팹을 찾을 수 없습니다: {originalPath}");
                return;
            }
            
            // 이미 복사본이 존재하면 삭제
            if (AssetDatabase.LoadAssetAtPath<GameObject>(newPath) != null)
            {
                AssetDatabase.DeleteAsset(newPath);
                AssetDatabase.Refresh();
                Debug.Log($"기존 복사본 삭제됨: {newPath}");
            }
            
            // 프리팹 복사
            if (!AssetDatabase.CopyAsset(originalPath, newPath))
            {
                Debug.LogError($"프리팹 복사 실패: {originalPath} -> {newPath}");
                return;
            }
            
            AssetDatabase.Refresh();
            
            // 복사된 프리팹 로드
            GameObject newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPath);
            if (newPrefab == null)
            {
                Debug.LogError($"복사된 프리팹 로드 실패: {newPath}");
                return;
            }
            
            // 프리팹 편집 모드로 열기
            string prefabPath = AssetDatabase.GetAssetPath(newPrefab);
            GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
            
            Debug.Log($"프리팹 구조 분석 시작...");
            
            // 모든 RectTransform의 크기를 80%로 조정
            int count = ScaleAllRectTransforms(prefabInstance.transform, 0.8f);
            
            // 변경사항 저장
            PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabInstance);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"프리팹 크기 조정 완료: {newPath}");
            Debug.Log($"총 {count}개의 RectTransform이 80% 크기로 조정되었습니다.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"오류 발생: {e.Message}\n{e.StackTrace}");
        }
    }
    
    private static int ScaleAllRectTransforms(Transform transform, float scaleFactor)
    {
        int count = 0;
        RectTransform rectTransform = transform.GetComponent<RectTransform>();
        
        if (rectTransform != null)
        {
            // sizeDelta 조정 (Width, Height)
            Vector2 currentSize = rectTransform.sizeDelta;
            rectTransform.sizeDelta = currentSize * scaleFactor;
            
            // anchoredPosition도 조정 (상대적 위치 유지)
            Vector2 currentPos = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = currentPos * scaleFactor;
            
            Debug.Log($"{transform.name}: size {currentSize} -> {rectTransform.sizeDelta}, pos {currentPos} -> {rectTransform.anchoredPosition}");
            count++;
        }
        
        // 모든 자식에 대해 재귀적으로 적용
        for (int i = 0; i < transform.childCount; i++)
        {
            count += ScaleAllRectTransforms(transform.GetChild(i), scaleFactor);
        }
        
        return count;
    }
}
