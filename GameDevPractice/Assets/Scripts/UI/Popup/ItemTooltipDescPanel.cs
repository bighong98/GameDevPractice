using TH.Core.Pool;
using TH.Core.Service;
using TH.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TH.UI
{
    /// <summary>
    /// 아이템 툴팁의 설명 패널 UI 컴포넌트.
    /// 텍스트와 레이아웃 높이를 포함한 UI 레이아웃 동기화 수행.
    /// 오브젝트 풀링을 지원하여 효율적인 UI 재사용 가능.
    /// </summary>
    public sealed class ItemTooltipDescPanel : MonoBehaviour, IPoolObject
    {
        /// <summary>설명 텍스트 컴포넌트</summary>
        
[SerializeField] private TMP_Text descText;
        /// <summary>레이아웃 요소 (높이 제어용)</summary>
        
[SerializeField] private LayoutElement layoutElement;

        /// <summary>설명 텍스트 컴포넌트 접근자</summary>
        
public TMP_Text DescText => descText;
        /// <summary>패널 RectTransform 접근자</summary>
        
public RectTransform PanelRect => transform as RectTransform;
        /// <summary>콘텐츠 영역 RectTransform (텍스트 부모)</summary>
        
public RectTransform ContentRect => descText != null ? descText.transform.parent as RectTransform : null;
        /// <summary>레이아웃 요소 접근자</summary>
        
public LayoutElement LayoutElement => layoutElement;

        /// <summary>풀링용 원본 프리팩 참조</summary>
        
public GameObject Origin { get; set; }

        private void Awake()
        {
            if (descText == null)
                descText = GetComponentInChildren<TMP_Text>(true);
            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();
        }

        /// <summary>
        /// 설명 텍스트를 갱신하고 필요 시 즉시 높이를 재계산.
        /// </summary>
        /// <param name="content">표시할 텍스트</param>
        /// <param name="syncHeight">높이 동기화 여부</param>
        public void SetText(string content, bool syncHeight = true)
        {
            if (descText != null)
                descText.SetText(content);
            if (syncHeight)
                SyncHeight();
        }

        /// <summary>
        /// 레이아웃 높이 재계산.
        /// </summary>
        
public void RefreshHeight()
        {
            SyncHeight();
        }

        /// <summary>풀에서 생성 시 호출</summary>
        
public void OnCreateFromPool() { }

        /// <summary>풀에서 가져올 때 호출</summary>
        
public void OnGetFromPool() { }

        /// <summary>
        /// 풀로 반환될 때 호출.
        /// 텍스트를 비우고 레이아웃 높이를 초기화.
        /// </summary>
        public void OnReleaseFromPool()
        {
            if (descText != null)
                descText.SetText(string.Empty);
            SyncHeight();
        }

        /// <summary>풀에서 파괴 시 호출</summary>
        
public void OnDestroyFromPool() { }

        /// <summary>
        /// 자신을 풀에 반환.
        /// </summary>
        
public void ReleaseSelf()
        {
            PoolManager.Instance.ReleaseFromPool(this);
        }

        /// <summary>
        /// 텍스트와 부모 레이아웃을 강제로 갱신한 뒤 패널/레이아웃 높이도 갱신.
        /// </summary>
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
