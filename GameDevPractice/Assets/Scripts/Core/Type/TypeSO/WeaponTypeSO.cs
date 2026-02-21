using System.Collections.Generic;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "WeaponTypeSo", menuName = "Scriptable Objects/Type/Item/WeaponTypeSO")]
    public class WeaponTypeSO : EquipmentTypeSO
    {
        [Header("Weapon")]
        public Enums.WeaponType weaponType;

        [Tooltip("GripHand: Both - 오른손 무기 프리펩으로 사용")]
        public GameObject EquippedPrefab;
        [SerializeField] private GameObject equippedPrefabLeft;
        [SerializeField] private Hand hand;
        [SerializeField] private List<SkillTypeSO> defaultSkills = new();

        public Hand GripHand => hand;
        public IReadOnlyList<SkillTypeSO> DefaultSkills => defaultSkills;
        public GameObject EquippedPrefabLeft => equippedPrefabLeft != null ? equippedPrefabLeft : EquippedPrefab;

        public enum Hand
        {
            Right,
            Left,
            Both,
        }
    }
}