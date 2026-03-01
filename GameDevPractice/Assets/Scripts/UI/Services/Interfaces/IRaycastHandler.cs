using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace TH.UI
{
    public interface IRaycastHandler
    {
        T RaycastAndGetFirstUIComponent<T>(PointerEventData pointerEventData, List<RaycastResult> raycastResults) where T : Component;
        T RaycastAndGetFirstPhysicsComponent<T>(Vector2 pos, int layerMask) where T : Component;
        
        bool IsInsideScreen(Vector3 worldPosition, out Vector3 screenPosition, bool ignoreLOD = true, float maxDistance = 0);

        Vector3 GetWorldScreenPosition(Vector3 pos, bool ignoreDepthZ = true);
        Vector3 GetMouseWorldPosition();
        Vector3 GetScreenWorldPosition(Vector2 pos = new Vector2());
        bool GetMouseScreenPosition(RectTransform rect, Vector2 pos, out Vector2 result);
        void ForceInit();
    }
}

