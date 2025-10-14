using System;
using RPG.Control;
using RPG.UI;
using TH.Core.Service;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TH.Item
{
    [RequireComponent(typeof(PlayerInventoryUI))]
    public sealed class InventoryController: MonoBehaviour
    {
        // model
        private IPlayerInventory pInventory;
        private IEquipmentHolder pEquipHolder;
        // view
        private PlayerInventoryUI pInvenUI;
        //todo: add pEquipUI
        
        // sub popup
        private UI_ItemTooltip itemTooltip;
        private QuestionPopupUI removeConfirmPopup;

        private void Awake()
        {
            pInventory = ServiceLocator.Require<IPlayerInventory>();
            pInvenUI = GetComponent<PlayerInventoryUI>();
            //todo: add pEquipUI
            RenewPlayerReference();
            SceneManager.sceneLoaded += (_, _) => { RenewPlayerReference(); };
        }

        private void OnEnable()
        {
            
        }

        private void OnDisable()
        {
            
        }

        private void RenewPlayerReference()
        {
            if (FindFirstObjectByType<PlayerController>() is {} player 
                && player.TryGetComponent(out IEquipmentHolder e))
            {
                pEquipHolder = e;
            }
        }
    }
}


