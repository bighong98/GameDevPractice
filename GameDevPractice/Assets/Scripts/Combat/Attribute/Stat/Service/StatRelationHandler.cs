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
            if (map == null) return;

            foreach ((var target, var influences) in map.Items)
            {
                if (target.IsNull() || influences == null || influences.Count == 0) continue;

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

            // [핵심 로직] 
            // 값이 넘어오지 않으므로, 핸들러가 능동적으로 소스 값을 조회(Pull)하여 적용
            void Apply()
            {
                // // 1. 소스 스탯 조회 (이 시점에 Lazy Calculation 발생)
                // var sourceStat = statHolder.GetStat(influence.source);
                
                // // 소스가 아직 없으면 관계 적용 불가 (Pending 상태일 수 있음)
                // if (sourceStat == null) return;

                // 1. 소스 스탯 조회 (이 시점에 Lazy Calculation 발생)
                // 소스가 아직 없으면 관계 적용 불가 (Pending 상태일 수 있음)
                if (!statHolder.TryGetStat(influence.source, out var sourceStat)) return;
                float sourceValue = sourceStat.Value; 

                // 2. 기존 모디파이어 제거
                if (currentModifier != null)
                {
                    statHolder.RemoveModifier(target, currentModifier);
                }

                // 3. 새로운 모디파이어 생성 및 적용
                currentModifier = influence.GetModifier(sourceValue, source: this);
                statHolder.AddModifier(target, currentModifier);
            }

            // [Source Listener] 소스 스탯이 변하면 -> Apply 실행
            Action onSourceChanged = () => Apply();

            // [Target Listener] 타겟 스탯이 생성(Pending 해제)되면 -> Apply 실행
            Action onTargetReady = null;
            onTargetReady = () =>
            {
                // 타겟이 준비되었으므로 대기 리스너 해제
                statHolder.UnBindEvent(target, onTargetReady);
                
                // 관계 적용 시도
                Apply();
            };

            // 1. 타겟 스탯 감시 (Pending 허용)
            // 타겟 스탯이 아직 없어도 나중에 생성되면 onTargetReady가 호출되어 관계가 맺어짐
            var targetStat = statHolder.BindEvent(target, onTargetReady, pending: true);

            // 2. 소스 스탯 감시 (Pending 허용)
            // 소스 스탯이 변할 때마다 타겟 스탯 갱신
            var sourceStat = statHolder.BindEvent(influence.source, onSourceChanged, pending: true);

            // 3. 초기 동기화 (Initial Sync)
            // 만약 이미 두 스탯이 모두 존재한다면, 이벤트 발생을 기다리지 않고 즉시 적용
            if (targetStat != null && sourceStat != null)
            {
                Apply();
            }
        }
    }
}

