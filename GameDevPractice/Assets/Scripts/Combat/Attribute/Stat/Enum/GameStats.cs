using System;
using System.Collections.Generic;
using TH.Utils;
using UnityEngine;

namespace TH.Attribute.Stat
{
    public static class GameStats
    {
        private static readonly Dictionary<int, GameStatSO> LegacyIdLookup = new();
        private static readonly Dictionary<string, GameStatSO> NameLookup = new(StringComparer.OrdinalIgnoreCase);
        private static bool initialized;

        public static GameStatSO Health => GetByLegacyId(101, nameof(Health));
        public static GameStatSO AD => GetByLegacyId(201, nameof(AD));
        public static GameStatSO AP => GetByLegacyId(202, nameof(AP));
        public static GameStatSO ExperienceReward => GetByLegacyId(1001, nameof(ExperienceReward));
        public static GameStatSO ExperienceToLevelUp => GetByLegacyId(1002, nameof(ExperienceToLevelUp));

        public static bool TryGetByLegacyId(int legacyId, out GameStatSO statType)
        {
            EnsureCache();
            return LegacyIdLookup.TryGetValue(legacyId, out statType);
        }

        public static void Register(GameStatSO statType)
        {
            if (statType == null) return;

            if (statType.LegacyId != 0)
            {
                LegacyIdLookup[statType.LegacyId] = statType;
            }

            if (!string.IsNullOrWhiteSpace(statType.DisplayName))
            {
                NameLookup[statType.DisplayName] = statType;
            }

            if (!string.IsNullOrWhiteSpace(statType.name))
            {
                NameLookup[statType.name] = statType;
            }

            initialized = true;
        }

        private static GameStatSO GetByLegacyId(int legacyId, string fallbackName)
        {
            EnsureCache();

            if (LegacyIdLookup.TryGetValue(legacyId, out var statType))
            {
                return statType;
            }

            if (NameLookup.TryGetValue(fallbackName, out statType))
            {
                return statType;
            }

            RefreshCache();

            if (LegacyIdLookup.TryGetValue(legacyId, out statType))
            {
                return statType;
            }

            if (NameLookup.TryGetValue(fallbackName, out statType))
            {
                return statType;
            }

            Logg.LogError($"[GameStats] Missing StatTypeSO for {fallbackName} (legacyId: {legacyId})");
            return null;
        }

        private static void EnsureCache()
        {
            if (initialized) return;
            RefreshCache();
        }

        private static void RefreshCache()
        {
            initialized = true;
            foreach (var statType in Resources.FindObjectsOfTypeAll<GameStatSO>())
            {
                Register(statType);
            }
        }
    }
}
