using System;
using UnityEngine;

[Serializable]
public class TestData
{
    public ItemTypeSO type;
    public int amount;
}
public class InventoryTestData : MonoBehaviour
{
    public TestData[] itemTypeHolders;
}
