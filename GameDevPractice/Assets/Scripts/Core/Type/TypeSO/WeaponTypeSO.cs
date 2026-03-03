using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using UnityEngine;

namespace TH.Resource
{
    [CreateAssetMenu(fileName = "WeaponTypeSo", menuName = "Scriptable Objects/Type/Item/WeaponTypeSO")]
    public class WeaponTypeSO : EquipmentTypeSO
{
    [Header("Weapon")]
    public Enums.WeaponType weaponType;

    [Tooltip("GripHand: Both - right-hand weapon is also used for left hand.")]
    [NonSerialized] public GameObject EquippedPrefab;
    [SerializeField] private AssetReferenceGameObject equippedPrefabReference;

    [NonSerialized] private GameObject equippedPrefabLeft;
    [SerializeField] private AssetReferenceGameObject equippedPrefabLeftReference;

    [SerializeField] private Hand hand;
    [SerializeField] private List<AssetReferenceSkillTypeSO> defaultSkillReferences = new();

    [NonSerialized] private readonly List<SkillTypeSO> defaultSkillStates = new();
    [NonSerialized] private bool initialized;

    public Hand GripHand => hand;
    public IReadOnlyList<SkillTypeSO> DefaultSkills => defaultSkillStates;
    public GameObject EquippedPrefabLeft => equippedPrefabLeft != null ? equippedPrefabLeft : EquippedPrefab;

    public override async UniTask InitializeAsync(CancellationToken token = default)
    {
        await base.InitializeAsync(token);

        if (initialized)
        {
            return;
        }

        if (equippedPrefabReference != null && equippedPrefabReference.RuntimeKeyIsValid())
        {
            var loadedRightPrefab = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(equippedPrefabReference, token);
            if (loadedRightPrefab != null)
            {
                EquippedPrefab = loadedRightPrefab;
            }
        }

        if (equippedPrefabLeftReference != null && equippedPrefabLeftReference.RuntimeKeyIsValid())
        {
            var loadedLeftPrefab = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(equippedPrefabLeftReference, token);
            if (loadedLeftPrefab != null)
            {
                equippedPrefabLeft = loadedLeftPrefab;
            }
        }

        defaultSkillStates.Clear();
        if (defaultSkillReferences != null)
        {
            for (int i = 0; i < defaultSkillReferences.Count; i++)
            {
                var skillReference = defaultSkillReferences[i];
                if (skillReference == null || !skillReference.RuntimeKeyIsValid())
                {
                    continue;
                }

                var loadedSkill = await ResourceManager.Instance.ExtractAssetRefAsync<SkillTypeSO>(skillReference, token);
                if (loadedSkill != null && !defaultSkillStates.Contains(loadedSkill))
                {
                    defaultSkillStates.Add(loadedSkill);
                }
            }
        }

        initialized = true;
    }

    public enum Hand
    {
        Right,
        Left,
        Both,
    }
}
}