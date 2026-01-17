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

        // UI Canavas 설정 일괄 적용 
        public void SetCanvas(GameObject go, UICanvas canvasType, bool isInteractable = true, bool renderWorldSpace = false)
        {
            // Canvas 컴포넌트 추가 또는 가져오기
            Canvas canvas = go.GetOrAddComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = renderWorldSpace ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
            }

            // CanvasScaler 컴포넌트 설정 (1920x1080 기준 스케일링)
            CanvasScaler cs = go.GetOrAddComponent<CanvasScaler>();
            if (cs != null)
            {
                cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                cs.referenceResolution = new Vector2(1920, 1080);
            }

            // 상호작용 가능하면 GraphicRaycaster 추가
            if (isInteractable)
                go.GetOrAddComponent<GraphicRaycaster>();

            SortCanvas(canvas, canvasType);
        }

        // PopupUI에 Canvas 설정 (팝업 전용)
        // RenderType에 따라 Canvas 모드 설정
        // CanvasGroup으로 투명도 제어 (애니메이션용)
        public void SetCanvas(PopupUI popup, bool isInteractable = true)
        {
            Canvas canvas = popup.gameObject.GetOrAddComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = popup.UiRenderType switch
                {
                    Enums.UIRenderType.WorldSpace => RenderMode.WorldSpace,
                    Enums.UIRenderType.ScreenCamera => RenderMode.ScreenSpaceCamera,
                    _ => RenderMode.ScreenSpaceOverlay
                };
                canvas.overrideSorting = true;
            }

            CanvasGroup cg = popup.gameObject.GetOrAddComponent<CanvasGroup>();
            if (cg != null)
                cg.alpha = 0f; // UI 애니메이션, 애니메이션 전처리를 위해 투명화 -> PopupUI.OnGetFromPool()에서 투명도 제거처리

            if (isInteractable)
                popup.gameObject.GetOrAddComponent<GraphicRaycaster>();
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
                && uiCanvasSettingSO.GetCanvasSetting(canvasType)?.defaultSortingOrder
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

        #endregion
    }
}
