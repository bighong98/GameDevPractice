using System;
using System.Collections.Generic;
using TH.Core.Service;
using TH.Utils;
using UnityEngine;

public class SoundOptionPanelUI : OptionPanelUIBase
{
    [SerializeField] private SoundVolumeOptionItemUI optionItemTemplate;
    [SerializeField] private Transform optionItemParent;
    

    private readonly Dictionary<Enums.VolumeGroup, string> groupLabelMap = new();
    private readonly Dictionary<Enums.VolumeGroup, SoundVolumeOptionItemUI> groupItems = new();
    private readonly Dictionary<Enums.VolumeGroup, float> lastNonZeroVolumes = new();
    private readonly Dictionary<Enums.VolumeGroup, bool> isMuted = new();

    private const float DefaultVolume = 1f;
    private const string VolumeSuffix = "Volume";
    private const string volumeGroupMapKey = "SoundVolumeGroupMapSO";

    private void Awake()
    {
        LoadGroupLabels();
        CreateVolumeItems();
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }

    private void LoadGroupLabels()
    {
        groupLabelMap.Clear();
        if (string.IsNullOrEmpty(volumeGroupMapKey))
        {
            Logg.LogWarning("[SoundOptionPanelUI] volumeGroupMapKey is empty");
            FillLabelMapWithDefaults();
            return;
        }

        if (!ResourceManager.Instance.TryLoad(volumeGroupMapKey, out SoundVolumeGroupMapSO map) || map == null)
        {
            Logg.LogWarning($"[SoundOptionPanelUI] failed to load SoundVolumeGroupMapSO: {volumeGroupMapKey}");
            FillLabelMapWithDefaults();
            return;
        }

        foreach (var entry in map.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.label)) continue;
            groupLabelMap[entry.group] = entry.label;
        }
    }

    private void FillLabelMapWithDefaults()
    {
        foreach (Enums.VolumeGroup group in Enum.GetValues(typeof(Enums.VolumeGroup)))
        {
            groupLabelMap[group] = group.ToString();
        }
    }

    private void CreateVolumeItems()
    {
        if (optionItemTemplate == null)
        {
            Logg.LogError("[SoundOptionPanelUI] optionItemTemplate is not set");
            return;
        }

        Transform parent = optionItemParent != null ? optionItemParent : transform;

        foreach (Enums.VolumeGroup group in Enum.GetValues(typeof(Enums.VolumeGroup)))
        {
            SoundVolumeOptionItemUI item = Instantiate(optionItemTemplate, parent);
            item.gameObject.name = $"{group}VolumeOption";
            item.gameObject.SetActive(true);

            string label = groupLabelMap.TryGetValue(group, out var text) && !string.IsNullOrEmpty(text)
                ? text
                : group.ToString();

            float initial = PlayerPrefs.GetFloat(GetVolumeKey(group), DefaultVolume);
            lastNonZeroVolumes[group] = initial > 0f ? initial : DefaultVolume;
            isMuted[group] = initial <= 0f;

            item.Initialize(label, initial,
                value => OnVolumeChanged(group, value),
                () => ToggleMute(group));

            groupItems[group] = item;
            SoundManager.Instance.SetGroupVolume(group, initial);
        }
    }

    protected override void SyncFromSettings()
    {
        if (groupItems.Count == 0)
            return;

        foreach (Enums.VolumeGroup group in Enum.GetValues(typeof(Enums.VolumeGroup)))
        {
            if (!groupItems.TryGetValue(group, out var item))
                continue;

            float value = PlayerPrefs.GetFloat(GetVolumeKey(group), DefaultVolume);
            item.SetValueWithoutNotify(value);

            if (value > 0f)
            {
                lastNonZeroVolumes[group] = value;
                isMuted[group] = false;
            }
            else
            {
                isMuted[group] = true;
            }
        }
    }

    protected override void ResetToDefaults()
    {
        foreach (Enums.VolumeGroup group in Enum.GetValues(typeof(Enums.VolumeGroup)))
        {
            if (!groupItems.TryGetValue(group, out var item))
                continue;

            item.SetValueWithoutNotify(DefaultVolume);
            lastNonZeroVolumes[group] = DefaultVolume;
            isMuted[group] = false;

            SoundManager.Instance.SetGroupVolume(group, DefaultVolume);
            PlayerPrefs.SetFloat(GetVolumeKey(group), DefaultVolume);
        }
    }

    private void OnVolumeChanged(Enums.VolumeGroup group, float value)
    {
        if (value > 0f)
        {
            lastNonZeroVolumes[group] = value;
            isMuted[group] = false;
        }
        else
        {
            isMuted[group] = true;
        }

        SoundManager.Instance.SetGroupVolume(group, value);
        PlayerPrefs.SetFloat(GetVolumeKey(group), value);
    }

    private void ToggleMute(Enums.VolumeGroup group)
    {
        if (!groupItems.TryGetValue(group, out var item)) return;

        if (isMuted.TryGetValue(group, out var muted) && muted)
        {
            float restore = DefaultVolume;
            if (lastNonZeroVolumes.TryGetValue(group, out var cached) && cached > 0f)
                restore = cached;

            isMuted[group] = false;
            item.SetValueWithoutNotify(restore);
            SoundManager.Instance.SetGroupVolume(group, restore);
            PlayerPrefs.SetFloat(GetVolumeKey(group), restore);
        }
        else
        {
            isMuted[group] = true;
            item.SetValueWithoutNotify(0f);
            SoundManager.Instance.SetGroupVolume(group, 0f);
            PlayerPrefs.SetFloat(GetVolumeKey(group), 0f);
        }
    }

    private static string GetVolumeKey(Enums.VolumeGroup group)
    {
        return $"{group}{VolumeSuffix}";
    }
}
