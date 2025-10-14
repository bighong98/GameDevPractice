using RPG.UI;
using UnityEngine;

namespace TH.UI
{
    public class InventoryUI : PopupUI, IPlayerInventoryUI
    {
        [SerializeField] private IPlayerStorageUI storageUI;
        [SerializeField] private IEquipmentHolderUI equipmentUI;
        
        
        
        protected override void Awake()
        {
            base.Awake();

            EnsureStorageUI();
            EnsureEquipmentUI();
            
            
        }

        #region Initialization

        private void EnsureStorageUI()
        {
            if (storageUI == null)
            {
                if (Util.FindChild(gameObject, "InventoryArea", true) is { } found
                    && found.TryGetComponent(out IPlayerStorageUI pStorageUI))
                {
                    storageUI = pStorageUI;
                }
            }
        }
        
        private void EnsureEquipmentUI()
        {
            if (equipmentUI == null)
            {
                if (Util.FindChild(gameObject, "EquipmentArea", true) is { } found
                    && found.TryGetComponent(out IEquipmentHolderUI pEquipmentUI))
                {
                    equipmentUI = pEquipmentUI;
                }
            }
        }

        #endregion
        

        
    }
}

