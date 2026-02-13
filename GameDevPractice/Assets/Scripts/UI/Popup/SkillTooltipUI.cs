using System.Collections.Generic;
using TH.Attribute.Stat;
using TH.Core.Pool;
using TH.Core.Service;
using TH.Resource;
using TH.Utils;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace TH.UI
{
    [DisallowMultipleComponent]
    public sealed class SkillTooltipUI : BaseUI, IPoolObject
    {
        private const string SkillTooltipLabelMapKey = "SkillTooltipLabelMap";

        private enum TMPTexts
        {
            ItemNameText,
            ItemTypeText
        }

        [SerializeField] private Transform descParent;
        [SerializeField] private RectTransform bodyRect;
        [SerializeField] private GameObject descPanelTemplate;

        private ObjectPool<IPoolObject> descPanelPool;
        private SkillTypeSO lastSkillInfo;
        private float lastSourceDamage = float.NaN;
        private readonly List<ItemTooltipDescPanel> descPanels = new();
        private IScreenSpaceClamper screenClamper;
        private SkillTooltipLabelMapSO cachedLabelMap;

        protected override void Awake()
        {
            base.Awake();
            Init();
        }

        public override bool Init()
        {
            if (base.Init() == false)
                return false;

            if (canvas == null && !TryGetComponent(out canvas))
            {
                this.LogWarning("failed to GetComponent<Canvas>, Add Canvas with Default Setting", context: this);
                canvas = gameObject.AddComponent<Canvas>();
            }

            if (transform is RectTransform rect)
                screenClamper = new ScreenSpaceClamper(rect, canvas.rootCanvas);
            else
                this.LogWarning($"UI doesnt have valid RectTransform for {nameof(IScreenSpaceClamper)}", context: this);

            BindTMPText(typeof(TMPTexts));
            CacheLayoutTargets();
            InitDescPanelPool();
            return true;
        }

        public void ShowTooltip() => gameObject.SetActive(true);

        public void HideTooltip() => gameObject.SetActive(false);

        public void ShowTooltipAt(Vector2 screenPos, SkillTypeSO skill, IStatHolder statHolder = null)
        {
            ShowTooltip(skill, statHolder, out bool contentChanged);

            if (contentChanged && transform is RectTransform rectTransform)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);

            ClampToScreen(screenPos);
        }

        public void MoveTooltip(Vector2 pos)
        {
            ClampToScreen(pos);
        }

        public void MoveTooltip(Vector3 pos)
        {
            ClampToScreen(pos);
        }

        private void CacheLayoutTargets()
        {
            if (descParent == null)
                return;

            if (bodyRect == null && transform is RectTransform rootRect)
            {
                var bodyTransform = rootRect.Find("Body");
                if (bodyTransform != null)
                    bodyRect = bodyTransform as RectTransform;
            }
        }

        private void InitDescPanelPool()
        {
            if (descPanelTemplate == null || descParent == null)
                return;

            descPanelPool = PoolManager.Instance.GetPool(
                descPanelTemplate,
                descParent,
                createAction: null,
                getAction: obj =>
                {
                    if (obj is Component component)
                    {
                        component.transform.SetParent(descParent, worldPositionStays: false);
                        component.transform.localScale = Vector3.one;
                    }
                },
                registerPool: false);
        }

        private void ShowTooltip(SkillTypeSO skill, IStatHolder statHolder, out bool contentChanged)
        {
            contentChanged = false;

            if (skill == null)
            {
                this.LogWarning("ShowTooltip: invalid skill data");
                return;
            }

            ShowTooltip();
            float sourceDamage = ResolveSourceDamage(skill, statHolder);

            if (lastSkillInfo == null || !ReferenceEquals(lastSkillInfo, skill) || float.IsNaN(lastSourceDamage) ||
                !Mathf.Approximately(lastSourceDamage, sourceDamage))
            {
                contentChanged = true;
                lastSkillInfo = skill;
                lastSourceDamage = sourceDamage;
                PrepareTooltip(skill, sourceDamage);
                RebuildTooltipLayout();
            }
        }

        private void PrepareTooltip(SkillTypeSO skill, float sourceDamage)
        {
            var labelMap = GetSkillLabelMap();

            GetTMPText((int)TMPTexts.ItemNameText)?.SetText(skill.SkillId ?? string.Empty);

            string skillTypeLabel = labelMap != null
                ? labelMap.GetLabel(TooltipLabelKeys.SkillType, "Skill")
                : "Skill";
            GetTMPText((int)TMPTexts.ItemTypeText)?.SetText(skillTypeLabel);

            BuildDescriptionPanels(skill, sourceDamage, labelMap);
        }

        private void BuildDescriptionPanels(SkillTypeSO skill, float sourceDamage, SkillTooltipLabelMapSO labelMap)
        {
            if (descParent == null || descPanelTemplate == null)
            {
                Logg.LogError("[SkillTooltip] descParent or descPanelTemplate is null");
                return;
            }

            ClearDescPanels();

            if (skill == null)
            {
                this.LogWarning("BuildDescriptionPanels - invalid skill");
                return;
            }

            var sections = SkillTooltipContentBuilder.BuildDescriptionSections(skill, sourceDamage, labelMap);
            foreach (var section in sections)
                AddDescPanel(section);
        }

        private static float ResolveSourceDamage(SkillTypeSO skill, IStatHolder statHolder)
        {
            if (skill == null)
                return 0f;

            if (statHolder != null &&
                skill.AttackSourceStatSO != null &&
                statHolder.TryGetStat(skill.AttackSourceStatSO, out var attackSourceStat) &&
                attackSourceStat != null)
            {
                return attackSourceStat.Value;
            }

            return skill.BaseDamage;
        }

        private SkillTooltipLabelMapSO GetSkillLabelMap()
        {
            if (cachedLabelMap != null)
                return cachedLabelMap;

            if (ResourceManager.Instance.TryLoad<SkillTooltipLabelMapSO>(SkillTooltipLabelMapKey, out var labelMap) && labelMap != null)
            {
                cachedLabelMap = labelMap;
                return cachedLabelMap;
            }

            this.LogWarning($"failed to load addressable key: {SkillTooltipLabelMapKey}", context: this);
            return null;
        }

        private void AddDescPanel(string content)
        {
            if (string.IsNullOrWhiteSpace(content) || descPanelPool == null)
                return;

            if (descPanelPool.Get() is not ItemTooltipDescPanel panel)
                return;

            panel.SetText(content, false);
            descPanels.Add(panel);
        }

        private void RebuildTooltipLayout()
        {
            Canvas.ForceUpdateCanvases();
            SyncDescPanelsHeight();
            SyncBodyHeight();

            if (bodyRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRect);

            if (transform is RectTransform rectTransform)
                LayoutRebuilder.MarkLayoutForRebuild(rectTransform);
        }

        private void SyncDescPanelsHeight()
        {
            for (int i = 0; i < descPanels.Count; i++)
            {
                var panel = descPanels[i];
                if (panel == null)
                    continue;

                panel.RefreshHeight();
            }
        }

        private void SyncBodyHeight()
        {
            if (bodyRect == null || descParent == null)
                return;

            if (descParent is not RectTransform panelsRect)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelsRect);
            bodyRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelsRect.rect.height);
        }

        private void ClampToScreen(Vector2 screenPos)
        {
            screenClamper?.ClampToScreen(screenPos);
        }

        private void ClearDescPanels()
        {
            for (int i = 0; i < descPanels.Count; i++)
            {
                if (descPanels[i] != null && descPanelPool != null)
                    descPanelPool.Release(descPanels[i]);
            }

            descPanels.Clear();
        }

        private void OnDestroy()
        {
            ClearDescPanels();
            descPanelPool?.Clear();
            descPanelPool = null;
        }

        public GameObject Origin { get; set; }

        public void OnCreateFromPool()
        {
        }

        public void OnGetFromPool()
        {
        }

        public void OnReleaseFromPool()
        {
            HideTooltip();
        }

        public void OnDestroyFromPool()
        {
        }

        public void ReleaseSelf()
        {
            HideTooltip();
        }
    }
}
