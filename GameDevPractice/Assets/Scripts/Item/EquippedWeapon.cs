using System;
using UnityEngine;

namespace RPG.Item
{
    [RequireComponent(typeof(WeaponTypeHolder))]
    public class EquippedWeapon : MonoBehaviour
    {
        private void Start()
        {
            if (GetComponent<WeaponTypeHolder>() is { } weaponTypeHolder)
            {
                
            }
        }
    }
}

