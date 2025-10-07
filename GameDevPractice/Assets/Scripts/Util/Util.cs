using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using Debug = UnityEngine.Debug; // [InputSystem]
using Random = UnityEngine.Random;

public static class Util
{
    // private static Camera mainCamera;
    private static bool _isQuitting = false;
    public static bool IsQuitting { get { return _isQuitting; } }
    
    // public static void SetMainCameraForUtilClass() // UIManager.Init()에서 호출됨
    // {
    //     if (mainCamera == null)
    //         mainCamera = Camera.main;
    // }

    #region Position Conversion (WorldSpace <-> Screen, etc) (deprecated)

    // private static Vector3 GetMouseWorldPosition(bool nullCheck = true)
    // {
    //     if (nullCheck && mainCamera == null)
    //         mainCamera = Camera.main;
    //
    //     // Vector2 screenPos = InputManager.Instance.PointerPos;
    //     Vector2 screenPos = Input.mousePosition;
    //     Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f)); // 10f is magic number
    //     mouseWorldPosition.z = 0f;
    //     return mouseWorldPosition;
    // }

    // public static Vector3 GetScreenWorldPosition(Vector2 pos = new Vector2(), bool nullCheck = true)
    // {
    //     // Default: InputManager로부터 현재 포인터/마우스 위치 기준으로 World Space 좌표 반환
    //     if (pos == Vector2.zero)
    //         return GetMouseWorldPosition(nullCheck);
    //     
    //     if (nullCheck && mainCamera == null)
    //         mainCamera = Camera.main;
    //     
    //     Vector3 mouseWorldPosition = mainCamera.ScreenToWorldPoint(new Vector3(pos.x, pos.y, 10f)); // 10f is magic number
    //     mouseWorldPosition.z = 0f;
    //     return mouseWorldPosition;
    // }

    // public static Vector3 GetWorldScreenPosition(Vector3 pos, bool ignoreDepthZ = true, bool nullCheck = true)
    // {
    //     if (nullCheck && mainCamera == null)
    //         mainCamera = Camera.main;
    //     Vector3 worldScreenPosition = mainCamera.WorldToScreenPoint(pos);
    //     
    //     if (ignoreDepthZ)
    //         worldScreenPosition.z = 0f;
    //     
    //     return worldScreenPosition;
    // }

    // public static bool GetMouseScreenPosition(RectTransform rect, Vector2 pos, out Vector2 result)
    // {
    //     return RectTransformUtility.ScreenPointToLocalPointInRectangle(
    //         rect,
    //         pos,
    //         null,
    //         out result
    //     );
    // }

    // public static bool IsInsideScreen(Vector3 worldPosition, out Vector3 screenPosition, bool ignoreLOD = true, float maxDistance = 0)
    // {
    //     if (GetWorldScreenPosition(worldPosition, false, false) 
    //             is { x: {} x and > 0, y: {} y and > 0, z: {} z and >= 0 } result 
    //         && x < Screen.width && y < Screen.width // 화면 안에 존재하는지 확인
    //         && !(!ignoreLOD && z > maxDistance)) // LOD 확인
    //     {
    //         screenPosition = result;
    //         return true;
    //     }
    //
    //     screenPosition = Vector3.zero;
    //     return false;
    // }
    
    

    #endregion
    
    public static T GetOrAddComponent<T>(GameObject go) where T : UnityEngine.Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
            component = go.AddComponent<T>();
        return component;
    }

    #region Compare (numerical type)

    public static bool IsEqualFloat(float a, float b)
    {
        float diff = Mathf.Abs(a - b);
        float tolerance = Mathf.Abs(a * .0001f);

        return diff <= tolerance;
    }

    #endregion

    #region Hierarchy

    public static GameObject FindChild(GameObject go, string name = null, bool recursive = false)
    {
        Transform trs = FindChild<Transform>(go, name, recursive);
        if (trs == null)
            return null;
        return trs.gameObject;
    }

    public static T FindChild<T>(GameObject go, string name = null, bool recursive = false) where T : UnityEngine.Object
    {
        if (go == null)
            return null;

        if (!recursive)
        {
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform trs = go.transform.GetChild(i);
                if (string.IsNullOrEmpty(name) || trs.name == name)
                {
                    T component = trs.GetComponent<T>();
                    if (component != null)
                        return component;
                }
            }
        }
        else
        {
            foreach (T component in go.GetComponentsInChildren<T>())
            {
                if (string.IsNullOrEmpty(name) || component.name == name)
                    return component;
            }
        }
        return null;
    }

    public static GameObject FindChildContainName(GameObject go, string name = null, bool recursive = false)
    {
        Transform trs = FindChildContainName<Transform>(go, name, recursive);
        if (trs == null)
            return null;
        return trs.gameObject;
    }

    public static T FindChildContainName<T>(GameObject go, string name = null, bool recursive = false, bool caseSensitive = true) where T : UnityEngine.Object
    {
        if (go == null)
            return null;

        if (!recursive)
        {
            for (int i = 0; i < go.transform.childCount; i++)
            {
                Transform trs = go.transform.GetChild(i);
                if (string.IsNullOrEmpty(name) || IsNameMatch(name, trs.name, caseSensitive))
                {
                    T component = trs.GetComponent<T>();
                    if (component != null)
                        return component;
                }
            }
        }
        else
        {
            foreach (T component in go.GetComponentsInChildren<T>())
            {
                if (string.IsNullOrEmpty(name) || IsNameMatch(name, component.name, caseSensitive))
                    return component;
            }
        }
        return null;
    }

    private static bool IsNameMatch(string targetName, string search, bool caseSensitive)
    {
        if (string.IsNullOrEmpty(search)) return true;
        return caseSensitive ? search.Contains(targetName) : search.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0;
    }
    
    #endregion

    #region Vector
    
    public static Vector2 GetVectorTwo(Vector3 vector)
    {
        return new Vector2(vector.x, vector.y);
    }

    public static Vector3 GetRandomDir()
    {
        return new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
    }

    public static bool CompareDistance(Vector3 standard, Vector3 former, Vector3 latter)
    {
        return (standard - former).sqrMagnitude < (standard - latter).sqrMagnitude;
    }

    public static float GetAngleFromVector(Vector3 vec)
    {
        float radian = Mathf.Atan2(vec.y, vec.x);
        return radian * Mathf.Rad2Deg;
    }

    public static void SafeInvoke(Action<bool> action, bool arg)
    {
        if (action == null) return;
        foreach (var d in action.GetInvocationList())
        {
            try { (d as Action<bool>)?.Invoke(arg); }
            catch (Exception e) {
                Debug.LogError($"[{nameof(Util)}.{nameof(SafeInvoke)}]: Error while invoking {d.Method.Name}: {e}");
            }
        }
    }

    #endregion

    #region Raycast (deprecated)

    // private static readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
    // public static T RaycastAndGetFirstUIComponent<T>(PointerEventData pointerEventData, List<RaycastResult> raycastResults) where T : Component
    // {
    //     raycastResults.Clear();
    //     EventSystem.current.RaycastAll(pointerEventData, raycastResults);
    //
    //     if (raycastResults.Count == 0) return null;
    //     // Util.Log($"{nameof(RaycastAndGetFirstUIComponent)}: {raycastResults[0]}", LoggingMode.Completed);
    //     return raycastResults[0].gameObject.GetComponent<T>();
    // }
    //
    // public static T RaycastAndGetFirstPhysicsComponent<T>(Vector2 pos, int layerMask) where T : Component
    // {
    //     return Physics2D.OverlapPoint(GetScreenWorldPosition(pos), layerMask)?.GetComponent<T>();
    // }

    #endregion

    #region UniTask

    public static void ClearCTS(CancellationTokenSource tokenSource)
    {
        if (!tokenSource?.IsCancellationRequested ?? false)
            tokenSource.Cancel();
        tokenSource?.Dispose();
    }

    #endregion

    #region Addressables

    // public static async UniTask<T> ExtractAssetRefAsync<T>(AssetReferenceT<T> reference) where T : UnityEngine.Object
    // {
    //     if (reference == null)
    //     {
    //         Debug.LogError($"[ExtractAssetReference] reference is null.");
    //         return null;
    //     }
    //
    //     if (!reference.RuntimeKeyIsValid())
    //     {
    //         Debug.LogError($"[ExtractAssetReference] Invalid RuntimeKey for AssetReference<{typeof(T).Name}>. Asset: {reference.Asset?.name}");
    //         return null;
    //     }
    //
    //     var handle = reference.OperationHandle.IsValid()
    //         ? reference.OperationHandle
    //         : reference.LoadAssetAsync();
    //
    //     await handle.Task;
    //
    //     if (handle.Status != AsyncOperationStatus.Succeeded)
    //     {
    //         Debug.LogError($"[ExtractAssetReference] Load failed for AssetReference<{typeof(T).Name}> with key: {reference.RuntimeKey}");
    //         return null;
    //     }
    //
    //     return handle.Result as T;
    // }

#if UNITY_EDITOR
    public static string GetAddressKeyInEditor(AssetReference assetRef)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null || string.IsNullOrEmpty(assetRef.AssetGUID)) return null;

        var entry = settings.FindAssetEntry(assetRef.AssetGUID);
        return entry?.address;
    }
#endif

    #endregion
    
    #region Debug

    // public enum LoggingMode
    // {
    //     Completed,
    //     InProgress,
    //     Focussed,
    // }
    // enum LogLevel
    // {
    //     None,
    //     OnlyFocussing,
    //     OnlyInProgress,
    //     All,
    // }
    // private static readonly LogLevel CurrLogLevel = LogLevel.OnlyInProgress;

    // [System.Diagnostics.Conditional("UNITY_EDITOR")]
    // public static void Log(object msg, LoggingMode mode)
    // {
    //     switch (mode)
    //     {
    //         case LoggingMode.InProgress when CurrLogLevel is LogLevel.All or LogLevel.OnlyInProgress:
    //         case LoggingMode.Focussed when CurrLogLevel is not LogLevel.None:
    //             Log(msg);
    //             break;
    //         default:
    //             if (CurrLogLevel is LogLevel.All) Log(msg);
    //             break;
    //     }
    // }
    // [System.Diagnostics.Conditional("UNITY_EDITOR")]
    // public static void Log(object msg) => UnityEngine.Debug.Log(msg);
    // [System.Diagnostics.Conditional("UNITY_EDITOR")]
    // public static void LogError(object msg) => UnityEngine.Debug.LogError(msg);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitForUtil()
    {
        Application.quitting += () =>
        {
            _isQuitting = true;
        };
    }
    
    #endregion
}