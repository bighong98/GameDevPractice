using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Core.Service;
using TH.Resource;
using UnityEngine;

namespace TH.Combat
{
    public enum SkillEffectTrigger
    {
        OnConsume = 0,
        OnHit = 1,
        OnAnimMarker = 2
    }

    public enum SkillEffectAnchorType
    {
        CharacterRoot = 0,
        WeaponSocket = 1,
        GroundUnderCharacter = 2,
        HitPoint = 3,
        TargetCenter = 4
    }

    [Serializable]
public sealed class SkillEffectCue
{
    [SerializeField] private string cueId;
    [SerializeField] private SkillEffectTrigger trigger = SkillEffectTrigger.OnConsume;
    [SerializeField] private string markerName;

    [NonSerialized] private GameObject effectPrefab;
    [SerializeField] private AssetReferenceGameObject effectPrefabReference;

    [NonSerialized] private AudioClip sfxClip;
    [SerializeField] private AssetReferenceAudioClip sfxClipReference;

    [SerializeField] private SkillEffectAnchorType anchorType = SkillEffectAnchorType.CharacterRoot;
    [SerializeField] private string anchorName;
    [SerializeField] private bool follow;
    [SerializeField] private bool oncePerAttackInstance;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField, Min(0f)] private float groundRayStartHeight = 1.5f;
    [SerializeField, Min(0.1f)] private float groundRayDistance = 6f;

    [NonSerialized] private bool initialized;

    public string CueId => cueId;
    public SkillEffectTrigger Trigger => trigger;
    public string MarkerName => markerName;
    public GameObject EffectPrefab => effectPrefab;
    public AudioClip SfxClip => sfxClip;
    public SkillEffectAnchorType AnchorType => anchorType;
    public string AnchorName => anchorName;
    public bool Follow => follow;
    public bool OncePerAttackInstance => oncePerAttackInstance;
    public Vector3 PositionOffset => positionOffset;
    public LayerMask GroundLayerMask => groundLayerMask;
    public float GroundRayStartHeight => Mathf.Max(0f, groundRayStartHeight);
    public float GroundRayDistance => Mathf.Max(0.1f, groundRayDistance);

    public bool IsValid => effectPrefab != null || sfxClip != null;

    public async UniTask InitializeAsync(CancellationToken token)
    {
        if (initialized)
        {
            return;
        }

        if (effectPrefabReference != null && effectPrefabReference.RuntimeKeyIsValid())
        {
            var loadedEffectPrefab = await ResourceManager.Instance.ExtractAssetRefAsync<GameObject>(effectPrefabReference, token);
            if (loadedEffectPrefab != null)
            {
                effectPrefab = loadedEffectPrefab;
            }
        }

        if (sfxClipReference != null && sfxClipReference.RuntimeKeyIsValid())
        {
            var loadedSfxClip = await ResourceManager.Instance.ExtractAssetRefAsync<AudioClip>(sfxClipReference, token);
            if (loadedSfxClip != null)
            {
                sfxClip = loadedSfxClip;
            }
        }

        initialized = true;
    }
}
}
