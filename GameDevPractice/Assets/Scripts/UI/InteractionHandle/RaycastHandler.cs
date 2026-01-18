using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TH.SceneManagement;
using TH.Utils;

namespace TH.UI
{
    public class RaycastHandler : IRaycastHandler
    {
        private static Camera _mainCamera;
        private static EventSystem _eventSystem;

        public RaycastHandler(ISceneLoader sceneLoader)
        {
            InitOnce();
            sceneLoader.OnSceneChanged += InitAfterSceneChanged;
        }

        public void ForceInit() => Init();
        
        void Init()
        {
            RenewCamera();
        }

        //최초 1회 초기화 필요한 작업 수행
        void InitOnce()
        {
            Logg.Log($"[RaycastHandler] InitOnce() invoked", Logg.LoggingMode.Completed);
            Init();
            
        }
        
        //씬 변동마다 초기화 필요한 작업 수행
        void InitAfterSceneChanged(Scene scene)
        {
            Logg.Log($"[RaycastHandler] InitAfterSceneChanged() invoked", Logg.LoggingMode.Completed);
            Init();
        }

        void RenewCamera()
        {
            _mainCamera = Camera.main;
            _eventSystem = EventSystem.current;
        }
        
        private static readonly List<RaycastResult> RaycastResults = new List<RaycastResult>();
        public T RaycastAndGetFirstUIComponent<T>(PointerEventData pointerEventData, List<RaycastResult> raycastResults) where T : Component
        {
            raycastResults.Clear();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            if (raycastResults.Count == 0) return null;
            Logg.Log($"{nameof(RaycastAndGetFirstUIComponent)}: {raycastResults[0]}", Logg.LoggingMode.Completed);
            return raycastResults[0].gameObject.GetComponent<T>();
        }
        
        public T RaycastAndGetFirstPhysicsComponent<T>(Vector2 pos, int layerMask) where T : Component
        {
            return Physics2D.OverlapPoint(GetScreenWorldPosition(pos), layerMask)?.GetComponent<T>();
        }
        
        public bool IsInsideScreen(Vector3 worldPosition, out Vector3 screenPosition, bool ignoreLOD = true, float maxDistance = 0)
        {
            if (GetWorldScreenPosition(worldPosition, false) 
                    is { x: {} x and > 0, y: {} y and > 0, z: {} z and >= 0 } result 
                && x < Screen.width && y < Screen.width // 화면 안에 존재하는지 확인
                && !(!ignoreLOD && z > maxDistance)) // LOD 확인
            {
                screenPosition = result;
                return true;
            }

            screenPosition = Vector3.zero;
            return false;
        }
        
        public Vector3 GetWorldScreenPosition(Vector3 pos, bool ignoreDepthZ = true)
        {
            Vector3 worldScreenPosition = _mainCamera.WorldToScreenPoint(pos);
        
            if (ignoreDepthZ)
                worldScreenPosition.z = 0f;
        
            return worldScreenPosition;
        }
        
        public Vector3 GetMouseWorldPosition()
        {
            Vector2 screenPos = Input.mousePosition; // todo: InputSystem 기반으로 변경
            Vector3 mouseWorldPosition = _mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f)); // 10f is magic number
            mouseWorldPosition.z = 0f;
            return mouseWorldPosition;
        }
        
        public Vector3 GetScreenWorldPosition(Vector2 pos = new Vector2())
        {
            // Default: InputManager로부터 현재 포인터/마우스 위치 기준으로 World Space 좌표 반환
            if (pos == Vector2.zero)
                return GetMouseWorldPosition();
        
            Vector3 mouseWorldPosition = _mainCamera.ScreenToWorldPoint(new Vector3(pos.x, pos.y, 10f)); // 10f is magic number
            mouseWorldPosition.z = 0f;
            return mouseWorldPosition;
        }

        public bool GetMouseScreenPosition(RectTransform rect, Vector2 pos, out Vector2 result)
        {
            var canvas = rect != null ? rect.GetComponentInParent<Canvas>() : null;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                pos,
                camera,
                out result
            );
        }
    }
}

