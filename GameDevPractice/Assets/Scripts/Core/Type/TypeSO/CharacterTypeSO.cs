using System.Collections.Generic;
using UnityEngine;
using TH.Stats;
using UnityEngine.Serialization;
using TH.Attribute.Stat;

namespace TH.Resource
{
    public abstract class CharacterTypeSO : BaseTypeSO
    {
        [Header("Character")] 
        public CharacterType characterType;
        public BaseStatListSO characterBaseStats;
        public float height;
        public int startingLevel;

        [SerializeField] public List<EquipmentTypeSO> defaultEquipments;
        [SerializeField] private GameObject hpBarPrefab;

        public GameObject HpBarPrefab => hpBarPrefab;
    }
}

