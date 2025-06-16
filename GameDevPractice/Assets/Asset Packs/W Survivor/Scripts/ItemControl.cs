// using System;
// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using System.Text;
//
// public class ItemControl : MonoBehaviour
// {
//     public ItemData itemData;
//     public int itemLevel;
//     public Weapon weapon;
//
//     public Image iconImage;
//     public Text textLevel;
//     public string stringLevel;
//
//     private void Awake()
//     {
//         stringLevel = "Lv.0";
//     }
//
//     private void Start()
//     {
//         iconImage = GetComponentsInChildren<Image>()[1];
//         iconImage.sprite = itemData.itemIcon;
//         
//         Text[] texts = GetComponentsInChildren<Text>();
//         textLevel = texts[0];
//     }
//
//     private void UpdateTextLevel()
//     {
//         textLevel.text = "Lv." + itemLevel;
//     }
//
//     public void OnClick()
//     {
//         if (itemLevel == itemData.damages.Length)
//         {
//             //GetComponent<Button>().interactable = false;
//             return;
//         }
//         
//         switch (itemData.itemType)
//         {
//             case ItemData.ItemType.Melee:
//             case ItemData.ItemType.Range:
//                 break;
//             case ItemData.ItemType.Glove:
//                 break;
//             case ItemData.ItemType.Shoe:
//                 break;
//             case ItemData.ItemType.Heal:
//                 break;
//         }
//
//         itemLevel++;
//         UpdateTextLevel();
//     }
// }
