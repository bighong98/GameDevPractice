using System;
using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Resource;
using TH.Utils;

namespace TH.Attribute.Service
{
    public class StatRelationHandler : IStatRelationHandler
    {
        private const string StatCorrelationMapKey = "StatCorrelationMapSO";

        private readonly IResourceLoader resourceLoader;
        private StatCorrelationMapSO map;

        private readonly HashSet<IStatHolder> boundHolders = new();
        private readonly HashSet<IStatHolder> pendingHolders = new();

        public StatRelationHandler(IResourceLoader resourceLoader)
        {
            this.resourceLoader = resourceLoader;
            if (resourceLoader == null)
            {
                Logg.LogError($"[{nameof(StatRelationHandler)}] resourceLoader is null");
                return;
            }

            resourceLoader.WaitForPreLoad(Constants.PreLoadLabel, TryLoadMap);
        }

        public void BindRelations(IStatHolder statHolder)
        {
            if (statHolder == null) return;

            if (map == null)
            {
                pendingHolders.Add(statHolder);
                return;
            }

            BindRelationsInternal(statHolder);
        }

        private void TryLoadMap()
        {
            if (!resourceLoader.TryLoad(StatCorrelationMapKey, out map))
            {
                Logg.LogError($"[{nameof(StatRelationHandler)}] failed to load {StatCorrelationMapKey}");
                return;
            }

            if (pendingHolders.Count == 0) return;

            foreach (var holder in pendingHolders)
            {
                BindRelationsInternal(holder);
            }

            pendingHolders.Clear();
        }

        private void BindRelationsInternal(IStatHolder statHolder)
        {
            if (!boundHolders.Add(statHolder)) return;

            foreach (var pair in map.Items)
            {
                var target = pair.key;
                var influences = pair.value;

                if (target == null || influences == null || influences.Count == 0) continue;

                foreach (var influence in influences)
                {
                    if (influence.source == null) continue;
                    BindRelation(statHolder, target, influence);
                }
            }
        }

        private void BindRelation(IStatHolder statHolder, GameStatSO target, StatInfluenceData influence)
        {
            StatModifier currentModifier = null;
            float lastSourceValue = 0f;
            bool hasSourceValue = false;
            bool targetReady = false;

            void Apply()
            {
                if (!hasSourceValue || !targetReady) return;

                if (currentModifier != null)
                    statHolder.RemoveModifier(target, currentModifier);

                currentModifier = influence.GetModifier(lastSourceValue, source: this);
                statHolder.AddModifier(target, currentModifier);
            }

            Action<float> onSourceChanged = value =>
            {
                lastSourceValue = value;
                hasSourceValue = true;
                Apply();
            };

            Action<float> onTargetReady = null;
            onTargetReady = _ =>
            {
                if (targetReady) return;
                targetReady = true;
                statHolder.UnbindStatChanged(target, onTargetReady);

                if (!hasSourceValue) return;
                Apply();
            };

            statHolder.BindStatChanged(target, onTargetReady);
            statHolder.BindStatChanged(influence.source, onSourceChanged);
        }
    }
}

