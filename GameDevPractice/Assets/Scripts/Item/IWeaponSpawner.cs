using TH.Combat;
using UnityEngine;

namespace TH.Item
{
    public interface IWeaponSpawner
    {
        Transform RightHand { get; }
        Transform LeftHand { get; }
        GameObject EquippedWeapon { get; }

        void SetHandRoot(WeaponTypeSO.Hand hand, Transform trs);
        void SpawnWeapon(WeaponTypeSO data);
        void DespawnWeapon(WeaponTypeSO data);
        void DespawnWeapon();
    }
}

