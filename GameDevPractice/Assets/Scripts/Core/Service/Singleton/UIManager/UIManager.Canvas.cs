using TH.UI;
using TH.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace TH.Core.Service
{
    public partial class UIManager
    {
        
        #region UIManager.Canvas Public API

        // UI Canavas 설정 일괄 적용 
        // CanvasSettingSO에 등록된 UICanvas enum 타입별 데이터 사용
        public void SetCanvas(GameObject go, UICanvas canvasType, bool isInteractable = true, bool renderWorldSpace = false)
        {
            // Canvas 컴포넌트 추가 또는 가져오기
            Canvas canvas = EnsureCanvas(go);
            
            // Canvas.renderMode 설정
            SetCanvasRenderMode(canvasType, renderWorldSpace, canvas);
            // CanvasScaler 컴포넌트 설정 
            ConfigureCanvasScaler(go);
            // 상호작용 가능하면 GraphicRaycaster 추가
            ConfigureGraphicRaycaster(go, isInteractable);

            // PopupUI 전용 캔버스 설정 //todo: 추후 PopupUI 내부로 로직 이동 고려
            if (go.TryGetComponent<PopupUI>(out _))
            {
                // CanvasGroup으로 투명도 제어 (애니메이션용)
                CanvasGroup cg = go.GetOrAddComponent<CanvasGroup>();
                if (cg != null)
                    cg.alpha = 0f; // UI 애니메이션, 애니메이션 전처리를 위해 투명화 -> PopupUI.OnGetFromPool()에서 투명도 제거처리
            }
            // 캔버스 정렬 (UICanvas 타입별 우선순위 기반 내부 정렬)
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
            SortCanvas(canvas, order);
        }

        // 특정 UICanvas 타입의 부모 캔버스의 RectTransform 반환
        public RectTransform GetCanvasRect(UICanvas type)
        {
            if (canvases == null || canvases.Count == 0)
                return null;

            if (canvases[(int)type] is { } canvasGo)
                return canvasGo.transform as RectTransform;
            return null;
        }

        #endregion
        
        #region Canvas Setting Logic

        private Canvas EnsureCanvas(GameObject go)
        {
            Canvas canvas = go.GetOrAddComponent<Canvas>();
            if (canvas != null)
                canvas.overrideSorting = true;
            return canvas;
        }

        // UI 캔버스 RenderMode 설정
        private void SetCanvasRenderMode(UICanvas canvasType, bool renderWorldSpace, Canvas canvas)
        {
            if (canvas == null) return;
            
            RenderMode targetRenderMode;
            var canvasSetting = uiCanvasSettingSO != null ? uiCanvasSettingSO.GetCanvasSetting(canvasType) : null;

            if (canvasSetting != null)
            {
                targetRenderMode = canvasSetting.RenderMode;
            }
            else if (canvas.TryGetComponent<PopupUI>(out var popup))
            {
                // 구버전 코드 대응용, RenderType에 따라 Canvas 모드 설정 //todo: 캔버스 세팅 정리 후 코드 제거
                targetRenderMode = popup.UiRenderType switch
                {
                    Enums.UIRenderType.WorldSpace => RenderMode.WorldSpace,
                    Enums.UIRenderType.ScreenCamera => RenderMode.ScreenSpaceCamera,
                    _ => RenderMode.ScreenSpaceOverlay
                };
            }
            else
            {
                targetRenderMode = renderWorldSpace ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            }

            canvas.renderMode = targetRenderMode;
        }

        // CanvasScaler 설정
        private void ConfigureCanvasScaler(GameObject go)
        {
            CanvasScaler cs = go.GetOrAddComponent<CanvasScaler>();
            if (cs == null) return;

            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
        }
        // GraphicRaycaster 추가 (상호작용이 필요한 경우에만 적용)
        private void ConfigureGraphicRaycaster(GameObject go, bool isInteractable)
        {
            if (!isInteractable) return;
            go.GetOrAddComponent<GraphicRaycaster>();
        }

        // 캔버스의 정렬 순서 초기화 (ScriptableObject(UICanvasSettingSO)에서 기본값 가져오기)
        private void ResetCanvasOrder(UICanvas canvasType)
        {
            if (uiCanvasSettingSO != null
                && uiCanvasSettingSO.GetCanvasSetting(canvasType)?.DefaultSortingOrder
                    is { } resultSortingOrder)
                sortOrders[(int)canvasType] = resultSortingOrder;
        }

        // 특정 CanvasUI 타입의 UI 부모 Transform을 반환
        private Transform GetUIParent(UICanvas type)
        {
            if (canvases == null || 
                canvases.Count == 0 || 
                canvases[(int)type] is not { } canvasGo) 
                return null;
            
            return canvasGo.transform;
        }

        // UI 오브젝트 풀의 컨테이너 반환, 기존 컨테이너가 없을 경우 생성
        private Transform GetOrCreateUIPoolContainer(UICanvas canvasType, GameObject prefab)
        {
            var parent = GetUIParent(canvasType);
            if (parent == null) return null;
            // 이미 해당 UICanvas 타입용 컨테이너가 있을 경우 반환
            if (uiPoolContainers.TryGetValue(prefab, out var existing) && existing != null)
                return existing;
            // 
            var containerGo = new GameObject($"{prefab.name}", typeof(RectTransform), typeof(LayoutElement));
            var container = containerGo.GetComponent<RectTransform>();
            container.SetParent(parent, worldPositionStays: false);

            // UI 레이아웃에 영향이 가지 않도록 부모 캔버스의 RectTransform 앵커 관련 옵션 동일하게 적용
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
            // 현재는 레이아웃이 프리펩과 달라지지 않도록 LayoutElement가 있을 경우 ignoreLayout: true 적용
            // todo: 관련 정책 명확하게 하고 통일 적용 필요
            var layoutElement = containerGo.GetComponent<LayoutElement>();
            layoutElement.ignoreLayout = true;

            uiPoolContainers[prefab] = container;
            return container;
        }

        #endregion
    }
}
