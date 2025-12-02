using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class ScaleInventoryUIWithItemSlots
{
    [MenuItem("Tools/Scale Inventory UI With Item Slots")]
    public static void ScaleInventoryUI()
    {
        try
        {
            string originalPath = "Assets/Game/UI/Popup/InventoryUI_Small.prefab";
            string newPath = "Assets/Game/UI/Popup/InventoryUI_Small_v2.prefab";
            string itemSlotPath = "Assets/Game/UI/SubItem/Inventory/ItemSlot/InvenSlotUI.prefab";
            string itemSlotSmallPath = "Assets/Game/UI/SubItem/Inventory/ItemSlot/InvenSlotUI_Small.prefab";
            
            // Step 1: InvenSlotUI 프리팹 사본 만들기
            GameObject itemSlotOriginal = AssetDatabase.LoadAssetAtPath<GameObject>(itemSlotPath);
            if (itemSlotOriginal == null)
            {
                Debug.LogError($"InvenSlotUI 프리팹을 찾을 수 없습니다: {itemSlotPath}");
                return;
            }
            
            Debug.Log($"InvenSlotUI 프리팹 찾음: {itemSlotPath}");
            
            // 기존 InvenSlotUI_Small이 있으면 삭제
            if (AssetDatabase.LoadAssetAtPath<GameObject>(itemSlotSmallPath) != null)
            {
                AssetDatabase.DeleteAsset(itemSlotSmallPath);
                AssetDatabase.Refresh();
                Debug.Log($"기존 InvenSlotUI_Small 삭제됨");
            }
            
            // InvenSlotUI 복사
            if (!AssetDatabase.CopyAsset(itemSlotPath, itemSlotSmallPath))
            {
                Debug.LogError($"InvenSlotUI 프리팹 복사 실패");
                return;
            }
            
            AssetDatabase.Refresh();
            
            // Step 2: InvenSlotUI_Small 크기 조정
            string itemSlotSmallPrefabPath = AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GameObject>(itemSlotSmallPath));
            GameObject itemSlotInstance = PrefabUtility.LoadPrefabContents(itemSlotSmallPrefabPath);
            
            Debug.Log($"InvenSlotUI_Small 크기 조정 시작...");
            int itemSlotCount = ScaleAllRectTransforms(itemSlotInstance.transform, 0.8f);
            
            PrefabUtility.SaveAsPrefabAsset(itemSlotInstance, itemSlotSmallPrefabPath);
            PrefabUtility.UnloadPrefabContents(itemSlotInstance);
            
            Debug.Log($"InvenSlotUI_Small 생성 완료: {itemSlotCount}개 요소 조정됨");
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Step 3: InventoryUI_Small_v2 만들기
            GameObject inventoryOriginal = AssetDatabase.LoadAssetAtPath<GameObject>(originalPath);
            if (inventoryOriginal == null)
            {
                Debug.LogError($"InventoryUI_Small을 찾을 수 없습니다: {originalPath}");
                return;
            }
            
            // 기존 v2가 있으면 삭제
            if (AssetDatabase.LoadAssetAtPath<GameObject>(newPath) != null)
            {
                AssetDatabase.DeleteAsset(newPath);
                AssetDatabase.Refresh();
                Debug.Log($"기존 InventoryUI_Small_v2 삭제됨");
            }
            
            // InventoryUI_Small 복사
            if (!AssetDatabase.CopyAsset(originalPath, newPath))
            {
                Debug.LogError($"InventoryUI_Small 복사 실패");
                return;
            }
            
            AssetDatabase.Refresh();
            
            // Step 4: InventoryUI_Small_v2의 ItemSlotUI들을 InvenSlotUI_Small로 교체
            string inventoryPrefabPath = AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GameObject>(newPath));
            GameObject inventoryInstance = PrefabUtility.LoadPrefabContents(inventoryPrefabPath);
            
            Debug.Log($"InventoryUI_Small_v2에서 InvenSlotUI 교체 시작...");
            
            // ItemSlots 오브젝트 찾기
            Transform itemSlotsTransform = FindTransformRecursive(inventoryInstance.transform, "ItemSlots");
            if (itemSlotsTransform == null)
            {
                Debug.LogError("ItemSlots 오브젝트를 찾을 수 없습니다");
                PrefabUtility.UnloadPrefabContents(inventoryInstance);
                return;
            }
            
            Debug.Log($"ItemSlots 오브젝트 찾음: {itemSlotsTransform.name}");
            
            // InvenSlotUI_Small 프리팹 로드
            GameObject itemSlotSmallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(itemSlotSmallPath);
            if (itemSlotSmallPrefab == null)
            {
                Debug.LogError($"InvenSlotUI_Small을 로드할 수 없습니다: {itemSlotSmallPath}");
                PrefabUtility.UnloadPrefabContents(inventoryInstance);
                return;
            }
            
            // GridLayoutGroup 설정 조정
            GridLayoutGroup gridLayout = itemSlotsTransform.GetComponent<GridLayoutGroup>();
            if (gridLayout != null)
            {
                Vector2 currentCellSize = gridLayout.cellSize;
                gridLayout.cellSize = currentCellSize * 0.8f;
                
                Vector2 currentSpacing = gridLayout.spacing;
                gridLayout.spacing = currentSpacing * 0.8f;
                
                Debug.Log($"GridLayoutGroup 조정: cellSize {currentCellSize} -> {gridLayout.cellSize}, spacing {currentSpacing} -> {gridLayout.spacing}");
            }
            else
            {
                Debug.LogWarning("GridLayoutGroup을 찾을 수 없습니다");
            }
            
            // 기존 ItemSlotUI 오브젝트들 교체
            int replacedCount = 0;
            Transform[] children = new Transform[itemSlotsTransform.childCount];
            for (int i = 0; i < itemSlotsTransform.childCount; i++)
            {
                children[i] = itemSlotsTransform.GetChild(i);
            }
            
            Debug.Log($"ItemSlots의 자식 오브젝트 수: {children.Length}");
            
            foreach (Transform child in children)
            {
                if (child == null) continue;
                
                string childName = child.name;
                
                if (childName.StartsWith("ItemSlotUI") || childName.StartsWith("InvenSlotUI"))
                {
                    // 기존 오브젝트의 형제 인덱스 저장
                    int siblingIndex = child.GetSiblingIndex();
                    
                    // 새 InvenSlotUI_Small 인스턴스 생성
                    GameObject newItemSlot = PrefabUtility.InstantiatePrefab(itemSlotSmallPrefab, itemSlotsTransform) as GameObject;
                    newItemSlot.name = childName;
                    newItemSlot.transform.SetSiblingIndex(siblingIndex);
                    
                    // 기존 오브젝트 삭제
                    Object.DestroyImmediate(child.gameObject);
                    
                    replacedCount++;
                    Debug.Log($"교체 완료: {childName} (인덱스 {siblingIndex})");
                }
            }
            
            Debug.Log($"총 {replacedCount}개의 ItemSlotUI가 InvenSlotUI_Small로 교체되었습니다");
            
            // 변경사항 저장
            PrefabUtility.SaveAsPrefabAsset(inventoryInstance, inventoryPrefabPath);
            PrefabUtility.UnloadPrefabContents(inventoryInstance);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            Debug.Log($"====================================");
            Debug.Log($"작업 완료!");
            Debug.Log($"- InvenSlotUI_Small 생성: {itemSlotSmallPath}");
            Debug.Log($"- InventoryUI_Small_v2 생성: {newPath}");
            Debug.Log($"- {replacedCount}개 ItemSlotUI 교체 완료");
            Debug.Log($"====================================");
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
            Vector2 currentSize = rectTransform.sizeDelta;
            rectTransform.sizeDelta = currentSize * scaleFactor;
            
            Vector2 currentPos = rectTransform.anchoredPosition;
            rectTransform.anchoredPosition = currentPos * scaleFactor;
            
            count++;
        }
        
        for (int i = 0; i < transform.childCount; i++)
        {
            count += ScaleAllRectTransforms(transform.GetChild(i), scaleFactor);
        }
        
        return count;
    }
}
