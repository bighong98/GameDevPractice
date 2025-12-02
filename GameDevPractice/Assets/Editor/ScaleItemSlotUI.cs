using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class ScaleItemSlotUI
{
    [MenuItem("Tools/Scale ItemSlotUI and Create InventoryUI_Small_Fixed")]
    public static void ScaleItemSlotUIAndInventory()
    {
        try
        {
            // 1단계: ItemSlotUI 프리팹 사본 생성 및 크기 조정
            string originalItemSlotPath = "Assets/Game/UI/Slot/ItemSlotUI.prefab";
            string newItemSlotPath = "Assets/Game/UI/Slot/ItemSlotUI_Small.prefab";
            
            // 원본 ItemSlotUI 프리팹이 존재하는지 확인
            GameObject originalItemSlot = AssetDatabase.LoadAssetAtPath<GameObject>(originalItemSlotPath);
            if (originalItemSlot == null)
            {
                Debug.LogError($"원본 ItemSlotUI 프리팹을 찾을 수 없습니다: {originalItemSlotPath}");
                return;
            }
            
            // 이미 복사본이 존재하면 삭제
            if (AssetDatabase.LoadAssetAtPath<GameObject>(newItemSlotPath) != null)
            {
                AssetDatabase.DeleteAsset(newItemSlotPath);
                AssetDatabase.Refresh();
                Debug.Log($"기존 ItemSlotUI_Small 복사본 삭제됨: {newItemSlotPath}");
            }
            
            // ItemSlotUI 프리팹 복사
            if (!AssetDatabase.CopyAsset(originalItemSlotPath, newItemSlotPath))
            {
                Debug.LogError($"ItemSlotUI 프리팹 복사 실패: {originalItemSlotPath} -> {newItemSlotPath}");
                return;
            }
            
            AssetDatabase.Refresh();
            
            // 복사된 ItemSlotUI 프리팹 로드 및 크기 조정
            GameObject newItemSlot = AssetDatabase.LoadAssetAtPath<GameObject>(newItemSlotPath);
            if (newItemSlot == null)
            {
                Debug.LogError($"복사된 ItemSlotUI 프리팹 로드 실패: {newItemSlotPath}");
                return;
            }
            
            string itemSlotPrefabPath = AssetDatabase.GetAssetPath(newItemSlot);
            GameObject itemSlotInstance = PrefabUtility.LoadPrefabContents(itemSlotPrefabPath);
            
            Debug.Log("ItemSlotUI 크기 조정 시작...");
            int itemSlotCount = ScaleAllRectTransforms(itemSlotInstance.transform, 0.8f);
            
            PrefabUtility.SaveAsPrefabAsset(itemSlotInstance, itemSlotPrefabPath);
            PrefabUtility.UnloadPrefabContents(itemSlotInstance);
            
            Debug.Log($"ItemSlotUI 크기 조정 완료: {newItemSlotPath}");
            Debug.Log($"총 {itemSlotCount}개의 RectTransform이 80% 크기로 조정되었습니다.");
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 2단계: InventoryUI_Small 복사 및 ItemSlotUI 참조 교체
            string originalInventoryPath = "Assets/Game/UI/Popup/InventoryUI_Small.prefab";
            string newInventoryPath = "Assets/Game/UI/Popup/InventoryUI_Small_Fixed.prefab";
            
            // 원본 InventoryUI_Small 프리팹이 존재하는지 확인
            GameObject originalInventory = AssetDatabase.LoadAssetAtPath<GameObject>(originalInventoryPath);
            if (originalInventory == null)
            {
                Debug.LogError($"원본 InventoryUI_Small 프리팹을 찾을 수 없습니다: {originalInventoryPath}");
                return;
            }
            
            // 이미 복사본이 존재하면 삭제
            if (AssetDatabase.LoadAssetAtPath<GameObject>(newInventoryPath) != null)
            {
                AssetDatabase.DeleteAsset(newInventoryPath);
                AssetDatabase.Refresh();
                Debug.Log($"기존 InventoryUI_Small_Fixed 복사본 삭제됨: {newInventoryPath}");
            }
            
            // InventoryUI_Small 프리팹 복사
            if (!AssetDatabase.CopyAsset(originalInventoryPath, newInventoryPath))
            {
                Debug.LogError($"InventoryUI_Small 프리팹 복사 실패: {originalInventoryPath} -> {newInventoryPath}");
                return;
            }
            
            AssetDatabase.Refresh();
            
            // 복사된 InventoryUI 프리팹 로드
            GameObject newInventory = AssetDatabase.LoadAssetAtPath<GameObject>(newInventoryPath);
            if (newInventory == null)
            {
                Debug.LogError($"복사된 InventoryUI 프리팹 로드 실패: {newInventoryPath}");
                return;
            }
            
            string inventoryPrefabPath = AssetDatabase.GetAssetPath(newInventory);
            GameObject inventoryInstance = PrefabUtility.LoadPrefabContents(inventoryPrefabPath);
            
            Debug.Log("ItemSlotUI 참조 교체 시작...");
            
            // ItemSlots 찾기
            Transform itemSlotsTransform = FindTransformRecursive(inventoryInstance.transform, "ItemSlots");
            if (itemSlotsTransform == null)
            {
                Debug.LogError("ItemSlots를 찾을 수 없습니다!");
                PrefabUtility.UnloadPrefabContents(inventoryInstance);
                return;
            }
            
            // GridLayoutGroup 컴포넌트 찾기 및 cellSize 조정
            GridLayoutGroup gridLayout = itemSlotsTransform.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                Vector2 originalCellSize = gridLayout.cellSize;
                gridLayout.cellSize = originalCellSize * 0.8f;
                Debug.Log($"GridLayoutGroup cellSize 조정: {originalCellSize} -> {gridLayout.cellSize}");
                
                // spacing도 조정
                Vector2 originalSpacing = gridLayout.spacing;
                gridLayout.spacing = originalSpacing * 0.8f;
                Debug.Log($"GridLayoutGroup spacing 조정: {originalSpacing} -> {gridLayout.spacing}");
            }
            
            // 모든 ItemSlotUI 프리팹 인스턴스를 새로운 프리팹으로 교체
            GameObject newItemSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(newItemSlotPath);
            int replacedCount = 0;
            
            for (int i = itemSlotsTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = itemSlotsTransform.GetChild(i);
                
                // 프리팹 인스턴스인지 확인
                if (PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                {
                    // 기존 ItemSlotUI인지 확인
                    GameObject prefabParent = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                    if (prefabParent != null && prefabParent.name.Contains("ItemSlotUI"))
                    {
                        // 위치와 상태 저장
                        Vector3 localPosition = child.localPosition;
                        Vector3 localScale = child.localScale;
                        Quaternion localRotation = child.localRotation;
                        bool isActive = child.gameObject.activeSelf;
                        int siblingIndex = child.GetSiblingIndex();
                        
                        // 기존 오브젝트 삭제
                        Object.DestroyImmediate(child.gameObject);
                        
                        // 새로운 프리팹 인스턴스 생성
                        GameObject newInstance = (GameObject)PrefabUtility.InstantiatePrefab(newItemSlotPrefab, itemSlotsTransform);
                        newInstance.transform.localPosition = localPosition;
                        newInstance.transform.localScale = localScale;
                        newInstance.transform.localRotation = localRotation;
                        newInstance.SetActive(isActive);
                        newInstance.transform.SetSiblingIndex(siblingIndex);
                        
                        replacedCount++;
                    }
                }
            }
            
            Debug.Log($"{replacedCount}개의 ItemSlotUI 인스턴스가 교체되었습니다.");
            
            // 변경사항 저장
            PrefabUtility.SaveAsPrefabAsset(inventoryInstance, inventoryPrefabPath);
            PrefabUtility.UnloadPrefabContents(inventoryInstance);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"InventoryUI_Small_Fixed 생성 완료: {newInventoryPath}");
            Debug.Log("모든 작업이 완료되었습니다!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"오류 발생: {e.Message}\n{e.StackTrace}");
        }
    }
    
    private static Transform FindTransformRecursive(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;
        
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindTransformRecursive(parent.GetChild(i), name);
            if (result != null)
                return result;
        }
        
        return null;
    }
    
    private static int ScaleAllRectTransforms(Transform transform, float scaleFactor)
    {
        int count = 0;
        RectTransform rectTransform = transform.GetComponent<RectTransform>();
        
        if (rectTransform != null)
        {
            // sizeDelta 조정
            Vector2 currentSize = rectTransform.sizeDelta;
            rectTransform.sizeDelta = currentSize * scaleFactor;
            
            // anchoredPosition 조정
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
