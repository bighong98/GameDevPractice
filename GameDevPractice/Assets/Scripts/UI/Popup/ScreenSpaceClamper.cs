using UnityEngine;

namespace TH.UI
{
    public interface IScreenSpaceClamper
    {
        void ClampToScreen(Vector2 screenPos);
    }

    public sealed class ScreenSpaceClamper : IScreenSpaceClamper
    {
        private readonly RectTransform target;
        private readonly RectTransform planeRect;
        private readonly Canvas rootCanvas;
        private readonly Camera uiCamera;

        public ScreenSpaceClamper(RectTransform target, Canvas rootCanvas)
        {
            this.target = target;
            this.rootCanvas = rootCanvas;

            planeRect = target != null
                ? target.parent as RectTransform
                : null;

            if (planeRect == null && rootCanvas != null)
                planeRect = rootCanvas.transform as RectTransform;

            if (rootCanvas == null || rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                uiCamera = null;
            else
                uiCamera = rootCanvas.worldCamera;
        }

        public void ClampToScreen(Vector2 screenPos)
        {
            if (target == null || rootCanvas == null || planeRect == null)
                return;

            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(planeRect, screenPos, uiCamera, out var targetWorldPos))
                return;

            Vector3 currentPos = target.position;
            Vector3 worldDelta = targetWorldPos - currentPos;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(target);
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            Vector3[] localCorners = new[]
            {
                new Vector3(min.x, min.y, 0f),
                new Vector3(min.x, max.y, 0f),
                new Vector3(max.x, max.y, 0f),
                new Vector3(max.x, min.y, 0f)
            };

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < localCorners.Length; i++)
            {
                Vector3 predictedWorldPoint = target.TransformPoint(localCorners[i]) + worldDelta;
                Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, predictedWorldPoint);

                minX = Mathf.Min(minX, screenPoint.x);
                maxX = Mathf.Max(maxX, screenPoint.x);
                minY = Mathf.Min(minY, screenPoint.y);
                maxY = Mathf.Max(maxY, screenPoint.y);
            }

            float screenWidth = rootCanvas.pixelRect.width;
            float screenHeight = rootCanvas.pixelRect.height;

            float adjustX = 0f;
            float adjustY = 0f;

            if (minX < 0f) adjustX = -minX;
            else if (maxX > screenWidth) adjustX = screenWidth - maxX;

            if (minY < 0f) adjustY = -minY;
            else if (maxY > screenHeight) adjustY = screenHeight - maxY;

            if (Mathf.Abs(adjustX) > 0.01f || Mathf.Abs(adjustY) > 0.01f)
            {
                Vector2 adjustedScreenPos = new Vector2(screenPos.x + adjustX, screenPos.y + adjustY);
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(planeRect, adjustedScreenPos, uiCamera, out var finalWorldPos))
                {
                    target.position = finalWorldPos;
                }
            }
            else
            {
                target.position = targetWorldPos;
            }
        }
    }
}
