using TH.Core.Pool;
using TH.Core.Service;
using TH.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    // 아이템 툴팁의 설명 패널 (Description Panel UI) 조작 목적 클래스
    // 텍스트와 레이아웃 높이를 포함한 UI 레이아웃 동기화 수행
    public sealed class ItemTooltipDescPanel : MonoBehaviour, IPoolObject
    {
        [SerializeField] private TMP_Text descText;
        [SerializeField] private LayoutElement layoutElement;

        public TMP_Text DescText => descText;
        public RectTransform PanelRect => transform as RectTransform;
        public RectTransform ContentRect => descText != null ? descText.transform.parent as RectTransform : null;
        public LayoutElement LayoutElement => layoutElement;

        public GameObject Origin { get; set; }

        private void Awake()
        {
            if (descText == null)
                descText = GetComponentInChildren<TMP_Text>(true);
            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();
        }

        // 설명 텍스트를 갱신하고 필요 시 즉시 높이를 재계산
        public void SetText(string content, bool syncHeight = true)
        {
            if (descText != null)
                descText.SetText(content);
            if (syncHeight)
                SyncHeight();
        }

        public void RefreshHeight()
        {
            SyncHeight();
        }

        public void OnCreateFromPool() { }

        public void OnGetFromPool() { }

        // 풀로 반환될 때 텍스트를 비우고 레이아웃 높이를 초기화
        public void OnReleaseFromPool()
        {
            if (descText != null)
                descText.SetText(string.Empty);
            SyncHeight();
        }

        public void OnDestroyFromPool() { }

        public void ReleaseSelf()
        {
            PoolManager.Instance.ReleaseFromPool(this);
        }

        // 텍스트와 부모 레이아웃을 강제로 갱신한 뒤 패널/레이아웃 높이도 갱신
        private void SyncHeight()
        {
            if (descText == null) return;

            var contentRect = ContentRect;
            var panelRect = PanelRect;
            if (contentRect == null || panelRect == null) return;

            descText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(descText.rectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            float height = contentRect.rect.height;
            panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

            if (layoutElement == null && TryGetComponent(out layoutElement))
            {
                this.LogWarning($"SyncHeight() - failed to get component {nameof(LayoutElement)}", context: this);
                return;
            }
            
            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
        }
    }
}
