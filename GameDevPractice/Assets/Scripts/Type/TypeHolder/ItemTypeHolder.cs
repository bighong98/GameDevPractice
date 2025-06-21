using System;
using UnityEngine;

// 아이템의 데이터 SO와 개수를 포함하는 MonoBehaviour 클래스
// 필드 드랍 아이템 등에 사용
public class ItemTypeHolder : TypeHolder<ItemTypeSO>
{
    [Header("Item")] [Tooltip("Only Countable Item type can have amount of more than one. Amount number larger than 1 will be ignored for other ItemType")]
    [SerializeField] private int amount;

    public int GetAmount => amount;
}
