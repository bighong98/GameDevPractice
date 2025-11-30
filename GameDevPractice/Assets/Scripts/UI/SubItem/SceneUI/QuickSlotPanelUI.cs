using System.Collections.Generic;
using TH.UI;
using UnityEngine;

public class QuickSlotPanelUI : BaseUI
{
    #region Enums

    enum GameObjects
    {
        slots,
    }

    #endregion

    [SerializeField] private readonly List<QuickSlotUI> slotUIs = new(); // serialize for debug

    protected override void Awake() 
    {
        base.Awake();
        BindObject(typeof(GameObjects));

        if (slotUIs.Count > 0) return;
        if (GetObject((int)GameObjects.slots) is {} slots && slots.IsAlive())
        {
            int i = 0;
            foreach (var slotUI in slots.GetComponentsInChildren<QuickSlotUI>())
            {
                slotUI.SetIndex(i);
                slotUIs.Add(slotUI);
                i++;
            }
        }
    }
}
