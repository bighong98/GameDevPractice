using UnityEngine;
using TH.Resource;
namespace TH.Item
{
public class EquippedWeapon : WeaponTypeHolder
    {
        [SerializeField] private Transform handle;
        [SerializeField] private Transform model;

        public Transform Handle => handle;
        public Transform Model => model;
    }
}

