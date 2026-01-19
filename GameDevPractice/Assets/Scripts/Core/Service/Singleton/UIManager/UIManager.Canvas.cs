using System;
using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TH.Core.Service
{
    public partial class UIManager
    {
        #region Common UI Method

        private Canvas EnsureCanvas(GameObject go)
        {
            Canvas canvas = go.GetOrAddComponent<Canvas>();
            if (canvas != null)
                canvas.overrideSorting = true;
            return canvas;
        }

        private void ConfigureCanvasScaler(GameObject go)
        {
            CanvasScaler cs = go.GetOrAddComponent<CanvasScaler>();
            if (cs == null) return;

            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
        }

        private void ConfigureGraphicRaycaster(GameObject go, bool isInteractable)
        {
            if (!isInteractable) return;
            go.GetOrAddComponent<GraphicRaycaster>();
        }

        // UI Canavas 설정 일괄 적용 
        public void SetCanvas(GameObject go, UICanvas canvasType, bool isInteractable = true, bool renderWorldSpace = false)
        {
            // Canvas 컴포넌트 추가 또는 가져오기
            Canvas canvas = EnsureCanvas(go);
            if (canvas != null)
            {
                if (go.TryGetComponent<PopupUI>(out var popup))
                {
                    // RenderType에 따라 Canvas 모드 설정
                    canvas.renderMode = popup.UiRenderType switch
                    {
                        Enums.UIRenderType.WorldSpace => RenderMode.WorldSpace,
                        Enums.UIRenderType.ScreenCamera => RenderMode.ScreenSpaceCamera,
                        _ => RenderMode.ScreenSpaceOverlay
                    };
                }
                else
                {
                    canvas.renderMode = renderWorldSpace ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
                }
            }

            // CanvasScaler 컴포넌트 설정 (1920x1080 기준 스케일링)
            ConfigureCanvasScaler(go);

            // 상호작용 가능하면 GraphicRaycaster 추가
            ConfigureGraphicRaycaster(go, isInteractable);

            // PopupUI에 Canvas 설정 (팝업 전용)
            if (go.TryGetComponent<PopupUI>(out _))
            {
                // CanvasGroup으로 투명도 제어 (애니메이션용)
                CanvasGroup cg = go.GetOrAddComponent<CanvasGroup>();
                if (cg != null)
                    cg.alpha = 0f; // UI 애니메이션, 애니메이션 전처리를 위해 투명화 -> PopupUI.OnGetFromPool()에서 투명도 제거처리
            }

            SortCanvas(canvas, canvasType);
        }

        // Canvas의 sortingOrder 수동 설정
        public void SortCanvas(Canvas canvas, int sortOrder = 0)
        {
            canvas.sortingOrder = sortOrder;
            canvas.overrideSorting = true;
        }

        // Canvas sortingOrder 자동 정렬 (캔버스 타입별 구분)
        public void SortCanvas(Canvas canvas, UICanvas canvasType)
        {
            if (uiCanvasSettingSO == null) return;

            int order = sortOrders[(int)canvasType] += 1;
            canvas.sortingOrder = order;
            canvas.overrideSorting = true;
        }

        // 캔버스의 정렬 순서 초기화 (ScriptableObject에서 기본값 가져오기)
        private void ResetCanvasOrder(UICanvas canvasType)
        {
            if (uiCanvasSettingSO != null
                && uiCanvasSettingSO.GetCanvasSetting(canvasType)?.DefaultSortingOrder
                    is { } resultSortingOrder)
                sortOrders[(int)canvasType] = resultSortingOrder;
        }

        // 특정 타입의 UI 부모 Transform을 반환
        private Transform GetUIParent(UICanvas type)
        {
            if (canvases[(int)type] is { } canvasGo)
                return canvasGo.transform;
            return null;
        }

        private Transform GetOrCreateUIPoolContainer(UICanvas canvasType, Type type)
        {
            var parent = GetUIParent(canvasType);
            if (parent == null) return null;

            string key = $"{canvasType}:{type.FullName}";
            if (uiPoolContainers.TryGetValue(key, out var existing) && existing != null)
                return existing;

            var containerGo = new GameObject($"{type.Name}", typeof(RectTransform), typeof(LayoutElement));
            var container = containerGo.GetComponent<RectTransform>();
            container.SetParent(parent, worldPositionStays: false);

            if (parent is RectTransform parentRect)
            {
                container.anchorMin = parentRect.anchorMin;
                container.anchorMax = parentRect.anchorMax;
                container.pivot = parentRect.pivot;
                container.anchoredPosition = parentRect.anchoredPosition;
                container.sizeDelta = parentRect.sizeDelta;
                container.localScale = Vector3.one;
            }
            else
            {
                container.anchoredPosition = Vector2.zero;
                container.localScale = Vector3.one;
            }

            var layoutElement = containerGo.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            uiPoolContainers[key] = container;
            return container;
        }

        #endregion
    }
}
