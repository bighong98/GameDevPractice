using System;
using System.Diagnostics;
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
    [SerializeField] private bool debugTiming = false;

    private readonly HashSet<GameStatCategory> missingCategoryLogged = new();
    private StatHolder statHolder;

    private readonly Dictionary<GameStatSO, StatEntryBinding> statEntries = new();
    private readonly HashSet<StatEntryBinding> pendingEntryUpdates = new();
    private readonly List<StatEntryBinding> pendingEntryBuffer = new();
    private bool hasPendingEntryUpdates;
    private readonly List<PlayerStatusPanelStatEntryUI> entryPool = new();
    private int entryPoolIndex;
    private readonly Dictionary<GameStatCategory, PlayerStatusPanelSectionUI> sectionLookup = new();

    protected override void Awake()
    {
        base.Awake();
        CacheSections();
    }

    public void SetPlayer(GameObject playerInstance)
    {
        Stopwatch totalSw = debugTiming ? Stopwatch.StartNew() : null;

        if (playerInstance == null)
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelUI)}] SetPlayer called with null player instance");
            return;
        }

        if (debugTiming)
        {
            var sw = Stopwatch.StartNew();
            CacheSections();
            Logg.Log($"[{nameof(PlayerStatusPanelUI)}] CacheSections took {sw.Elapsed.TotalMilliseconds:0.###} ms", Logg.LoggingMode.Completed);
        }
        else
        {
            CacheSections();
        }

        player = playerInstance;
        if (!player.TryGetComponent(out StatHolder holder))
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelUI)}] StatHolder not found on {player.name}");
            return;
        }

        if (statHolder == holder && statEntries.Count > 0)
        {
            if (debugTiming)
            {
                var sw = Stopwatch.StartNew();
                RefreshValues();
                Logg.Log($"[{nameof(PlayerStatusPanelUI)}] RefreshValues took {sw.Elapsed.TotalMilliseconds:0.###} ms", Logg.LoggingMode.Completed);
                Logg.Log($"[{nameof(PlayerStatusPanelUI)}] SetPlayer total took {totalSw.Elapsed.TotalMilliseconds:0.###} ms", Logg.LoggingMode.Completed);
            }
            else
            {
                RefreshValues();
            }
            return;
        }

        if (debugTiming)
        {
            var sw = Stopwatch.StartNew();
            DisconnectStatEvents();
            ClearEntries();
            Logg.Log($"[{nameof(PlayerStatusPanelUI)}] Disconnect+Clear took {sw.Elapsed.TotalMilliseconds:0.###} ms", Logg.LoggingMode.Completed);
        }
        else
        {
            DisconnectStatEvents();
            ClearEntries();
        }

        statHolder = holder;
        BuildStatEntries();

        if (debugTiming)
        {
            Logg.Log($"[{nameof(PlayerStatusPanelUI)}] SetPlayer total took {totalSw.Elapsed.TotalMilliseconds:0.###} ms", Logg.LoggingMode.Completed);
        }
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

        if (missingCategoryLogged.Add(category))
        {
            Logg.Log($"[{nameof(PlayerStatusPanelUI)}] Section not found for category: {category}", Logg.LoggingMode.Completed);
        }
        return null;
    }


    private void ResetEntryPool()
    {
        entryPoolIndex = 0;
        foreach (var entry in entryPool)
        {
            if (entry != null)
                entry.gameObject.SetActive(false);
        }
    }

    private PlayerStatusPanelStatEntryUI GetOrCreateEntry(RectTransform parent)
    {
        PlayerStatusPanelStatEntryUI entry;
        if (entryPoolIndex < entryPool.Count)
        {
            entry = entryPool[entryPoolIndex];
        }
        else
        {
            entry = Instantiate(entryPrefab, parent);
            entryPool.Add(entry);
        }

        entryPoolIndex++;

        if (entry != null)
        {
            var entryTransform = entry.transform as RectTransform;
            if (entryTransform != null && entryTransform.parent != parent)
                entryTransform.SetParent(parent, false);
            entry.gameObject.SetActive(true);
        }

        return entry;
    }


    
    private void BuildStatEntries()
    {
        if (statHolder == null) return;
        if (entryPrefab == null)
        {
            Logg.LogWarning($"[{nameof(PlayerStatusPanelUI)}] Stat entry prefab is missing");
            return;
        }

        Stopwatch buildSw = debugTiming ? Stopwatch.StartNew() : null;
        int statCount = statHolder.Stats?.Count ?? 0;
        int created = 0;
        int missingCategory = 0;

        if (debugLayout)
        {
            Logg.Log($"[{nameof(PlayerStatusPanelUI)}] BuildStatEntries stats={statCount}, sections={sectionLookup.Count}");
        }

        ResetEntryPool();

        // Cache content per category and pause layout recalculations during bulk instantiate.
        Dictionary<GameStatCategory, RectTransform> contentByCategory = new();
        List<Behaviour> pausedLayouts = new();
        foreach (var section in sectionLookup.Values)
        {
            if (section == null) continue;
            var content = section.EnsureAndGetContent();
            if (content == null) continue;
            contentByCategory[section.Category] = content;

            var layoutGroup = content.GetComponent<LayoutGroup>();
            if (layoutGroup != null && layoutGroup.enabled)
            {
                layoutGroup.enabled = false;
                pausedLayouts.Add(layoutGroup);
            }

            var contentSizeFitter = content.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter != null && contentSizeFitter.enabled)
            {
                contentSizeFitter.enabled = false;
                pausedLayouts.Add(contentSizeFitter);
            }
        }

        foreach (var pair in statHolder.Stats)
        {
            var statType = pair.Key;
            var stat = pair.Value;
            if (statType == null || stat == null) continue;

            if (!contentByCategory.TryGetValue(statType.Category, out var parent) || parent == null)
            {
                missingCategory++;
                if (missingCategoryLogged.Add(statType.Category))
                {
                    Logg.Log($"[{nameof(PlayerStatusPanelUI)}] Section not found for category: {statType.Category}", Logg.LoggingMode.Completed);
                }
                continue;
            }

            var entry = GetOrCreateEntry(parent);
            if (entry == null) continue;

            entry.SetName(statType.DisplayName);
            entry.SetValue(FormatValue(stat.Value));
            created++;

            var binding = new StatEntryBinding
            {
                Entry = entry,
                Stat = stat,
            };

            binding.OnChanged = () => MarkStatDirty(binding);
            statEntries[statType] = binding;
            statHolder.BindEvent(statType, binding.OnChanged);

            if (debugLayout)
            {
                LogEntryState(entry, parent, statType);
            }
        }

        foreach (var layout in pausedLayouts)
            layout.enabled = true;

        RebuildLayouts();

        if (debugTiming)
        {
            Logg.Log(
                $"[{nameof(PlayerStatusPanelUI)}] BuildStatEntries took {buildSw.Elapsed.TotalMilliseconds:0.###} ms (stats={statCount}, created={created}, missingCategory={missingCategory}, sections={sectionLookup.Count})",
                Logg.LoggingMode.Completed);
        }
    }

    private void MarkStatDirty(StatEntryBinding binding)
    {
        if (binding?.Entry == null || binding.Stat == null) return;
        if (pendingEntryUpdates.Add(binding))
            hasPendingEntryUpdates = true;
    }


    private void LateUpdate()
    {
        if (!hasPendingEntryUpdates) return;
        hasPendingEntryUpdates = false;

        pendingEntryBuffer.Clear();
        pendingEntryBuffer.AddRange(pendingEntryUpdates);
        pendingEntryUpdates.Clear();

        foreach (var binding in pendingEntryBuffer)
        {
            UpdateStatValue(binding);
        }

        pendingEntryBuffer.Clear();
    }


    private void RebuildLayouts()
    {
        Stopwatch sw = debugTiming ? Stopwatch.StartNew() : null;

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

        if (debugTiming)
        {
            Logg.Log($"[{nameof(PlayerStatusPanelUI)}] RebuildLayouts took {sw.Elapsed.TotalMilliseconds:0.###} ms", Logg.LoggingMode.Completed);
        }
    }

    private void LogEntryState(PlayerStatusPanelStatEntryUI entry, RectTransform parent, GameStatSO statType)
    {
        if (!debugLayout || entry == null || parent == null) return;

        var entryRect = entry.GetComponent<RectTransform>();
        var canvas = parent.GetComponentInParent<Canvas>(true);
        var canvasGroup = parent.GetComponentInParent<CanvasGroup>(true);
        var mask = parent.GetComponentInParent<Mask>(true);

        Logg.Log($"[{nameof(PlayerStatusPanelUI)}] Entry '{(statType.IsNotNull() ? statType.name : string.Empty)}' " +
                $"active={entry.gameObject.activeInHierarchy}, parentActive={parent.gameObject.activeInHierarchy}, " +
                $"entrySize={entryRect.sizeDelta}, entryScale={entryRect.lossyScale}, parentRect={parent.rect.size}, "+
                $"canvas={canvas?.name}, renderMode={canvas?.renderMode}, order={canvas?.sortingOrder}, " + 
                $"cam={(canvas != null ? canvas.worldCamera?.name : "null")}, canvasAlpha={(canvasGroup != null ? canvasGroup.alpha : -1f)}, " + 
                $"mask={(mask != null ? mask.name : "null")}", Logg.LoggingMode.Completed, context: this);
    }

    private PlayerStatusPanelStatEntryUI CreateStatEntry(RectTransform parent, string statName, IGameStat stat)
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
                entry.Entry.gameObject.SetActive(false);
        }
        statEntries.Clear();
        pendingEntryUpdates.Clear();
        pendingEntryBuffer.Clear();
        hasPendingEntryUpdates = false;
        entryPoolIndex = 0;
    }
    private void OnDestroy()
    {
        DisconnectStatEvents();
        ClearEntries();

        foreach (var entry in entryPool)
        {
            if (entry != null)
                Destroy(entry.gameObject);
        }
        entryPool.Clear();
    }



    private sealed class StatEntryBinding
    {
        public PlayerStatusPanelStatEntryUI Entry;
        public IGameStat Stat;
        public Action OnChanged;
    }
}
