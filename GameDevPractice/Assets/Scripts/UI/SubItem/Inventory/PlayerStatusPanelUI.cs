using System;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.UI;
using TH.Utils;

using UnityEngine.UI;
using UnityEngine;

public class PlayerStatusPanelUI : BaseUI
{
    [SerializeField] private PlayerStatusPanelStatEntryUI entryPrefab;
    [SerializeField] private GameObject player; // serialize for debug
    [Tooltip("디버깅 용 플레이어 필드 (임의로 참조 연결하지 말고 비워둘 것)")]
    [SerializeField] private bool debugLayout = false;
    
    private StatHolder statHolder;

    private readonly Dictionary<StatTypeSO, StatEntryBinding> statEntries = new();
    private readonly Dictionary<GameStatCategory, PlayerStatusPanelSectionUI> sectionLookup = new();

    protected override void Awake()
    {
        base.Awake();
        CacheSections();
    }

    public void SetPlayer(GameObject playerInstance)
    {
        if (playerInstance == null)
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelUI)}] SetPlayer called with null player instance");
            return;
        }

        CacheSections();

        player = playerInstance;
        if (!player.TryGetComponent(out StatHolder holder))
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelUI)}] StatHolder not found on {player.name}");
            return;
        }

        if (statHolder == holder && statEntries.Count > 0)
        {
            RefreshValues();
            return;
        }

        DisconnectStatEvents();
        ClearEntries();

        statHolder = holder;
        BuildStatEntries();
    }

    private void CacheSections()
    {
        sectionLookup.Clear();
        foreach (var section in GetComponentsInChildren<PlayerStatusPanelSectionUI>(true))
        {
            if (section == null) continue;
            sectionLookup[section.Category] = section;
        }
    }

    private RectTransform GetContentForCategory(GameStatCategory category)
    {
        if (sectionLookup.TryGetValue(category, out var section) && section != null)
            return section.EnsureAndGetContent();

        Logg.LogWarning($"[{nameof(PlayerStatusPanelUI)}] Section not found for category: {category}");
        return null;
    }


    
    private void BuildStatEntries()
    {
        if (statHolder == null) return;
        if (entryPrefab == null)
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelUI)}] Stat entry prefab is missing");
            return;
        }

        if (debugLayout)
        {
            Logg.Log($"[{nameof(PlayerStatusPanelUI)}] BuildStatEntries stats={statHolder.Stats?.Count ?? 0}, sections={sectionLookup.Count}");
        }

        foreach (var pair in statHolder.Stats)
        {
            var statType = pair.Key;
            var stat = pair.Value;
            if (statType == null || stat == null) continue;

            var parent = GetContentForCategory(statType.Category);
            if (parent == null) continue;

            var entry = CreateStatEntry(parent, statType.DisplayName, stat);
            if (entry == null) continue;

            var binding = new StatEntryBinding
            {
                Entry = entry,
                Stat = stat,
            };

            binding.OnChanged = () => UpdateStatValue(binding);
            statEntries[statType] = binding;
            statHolder.BindEvent(statType, binding.OnChanged);

            if (debugLayout)
            {
                LogEntryState(entry, parent, statType);
            }
        }

        RebuildLayouts();
    }

    private void RebuildLayouts()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var section in sectionLookup.Values)
        {
            if (section == null) continue;
            var content = section.EnsureAndGetContent();
            if (content == null) continue;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);

            if (debugLayout)
            {
                Logg.Log($"[{nameof(PlayerStatusPanelUI)}] RebuildLayouts section={section.name}, contentSize={content.rect.size}, children={content.childCount}", Logg.LoggingMode.Completed);
            }
        }
    }

    private void LogEntryState(PlayerStatusPanelStatEntryUI entry, RectTransform parent, StatTypeSO statType)
    {
        if (!debugLayout || entry == null || parent == null) return;

        var entryRect = entry.GetComponent<RectTransform>();
        var canvas = parent.GetComponentInParent<Canvas>(true);
        var canvasGroup = parent.GetComponentInParent<CanvasGroup>(true);
        var mask = parent.GetComponentInParent<Mask>(true);

        Logg.Log($"[{nameof(PlayerStatusPanelUI)}] Entry '{(statType.IsNotNull() ? statType.name : string.Empty)}' active={entry.gameObject.activeInHierarchy}, parentActive={parent.gameObject.activeInHierarchy}, entrySize={entryRect.sizeDelta}, entryScale={entryRect.lossyScale}, parentRect={parent.rect.size}, canvas={canvas?.name}, renderMode={canvas?.renderMode}, order={canvas?.sortingOrder}, cam={(canvas != null ? canvas.worldCamera?.name : "null")}, canvasAlpha={(canvasGroup != null ? canvasGroup.alpha : -1f)}, mask={(mask != null ? mask.name : "null")}", Logg.LoggingMode.Completed, context: this);
    }

    private PlayerStatusPanelStatEntryUI CreateStatEntry(RectTransform parent, string statName, GameStat stat)
    {
        if (entryPrefab == null) return null;

        var entry = Instantiate(entryPrefab, parent);
        entry.SetName(statName);
        entry.SetValue(FormatValue(stat.Value));

        return entry;
    }

    private void UpdateStatValue(StatEntryBinding binding)
    {
        if (binding?.Entry == null || binding.Stat == null) return;
        binding.Entry.SetValue(FormatValue(binding.Stat.Value));
    }

    private void RefreshValues()
    {
        foreach (var entry in statEntries.Values)
        {
            UpdateStatValue(entry);
        }
    }

    private string FormatValue(float value)
    {
        return value.ToString("0.##");
    }

    private void DisconnectStatEvents()
    {
        if (statHolder == null || statEntries.Count == 0) return;

        foreach (var pair in statEntries)
        {
            if (pair.Key == null || pair.Value?.OnChanged == null) continue;
            statHolder.UnBindEvent(pair.Key, pair.Value.OnChanged);
        }
    }

    private void ClearEntries()
    {
        foreach (var entry in statEntries.Values)
        {
            if (entry?.Entry != null)
                Destroy(entry.Entry.gameObject);
        }
        statEntries.Clear();
    }
    private void OnDestroy()
    {
        DisconnectStatEvents();
        ClearEntries();
    }



    private sealed class StatEntryBinding
    {
        public PlayerStatusPanelStatEntryUI Entry;
        public GameStat Stat;
        public Action OnChanged;
    }
}
