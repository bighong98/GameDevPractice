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

    #region UniTask

    public static void ClearCTS(CancellationTokenSource tokenSource)
    {
        if (!tokenSource?.IsCancellationRequested ?? false)
            tokenSource.Cancel();
        tokenSource?.Dispose();
    }
    private static readonly TimeSpan DefaultTimeoutSpan = TimeSpan.FromSeconds(5);
    
    // 모든 멀티캐스트 콜백 실행 보장/병렬 실행/예외 전파를 위한 유틸 매서드
    // token: 호출자 유효성 검사용 토큰
    // maxConcurrency: 한 번에 동시 실행 가능한 작업 개수
    // -> (예시) maxConcurrency: 10, 100개 작업 -> 한 번에 10개씩만, 작업 완료될 때마다 추가로 작업 집어넣어서 실행)
    // perCallbackTimeout: 무한 루프 등 방지 목적 콜백 별 시간 제한 (시간 제한 넘으면 강제로 cancel)
    // onException: 예외 발생 시 호출 콜백
    // throwAggregated: 개별 콜백 실패 시 예외 전파 옵션 (디버그 용도)
    // Extension.cs 에 정의된 확장 매서드 버전 사용 가능
    public static async UniTask InvokeAllCallbackAsync(Func<CancellationToken, UniTask> multicast,
        CancellationToken token,
        int maxConcurrency,
        TimeSpan? perCallbackTimeout = null,
        bool cancelAllOnFirstFailure = false,
        Action<Exception, Delegate> onException = null,
        bool throwAggregated = false)
    {
        if (multicast == null) return;
        // 멀티캐스트된 콜백들 분해
        var list = multicast.GetInvocationList();
        if (list.Length == 0) return;

        if (maxConcurrency < 1) maxConcurrency = 1;
        token.ThrowIfCancellationRequested();
        // SemaphoreSlim으로 동시 수행 가능한 작업 개수 제한 
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        // linkedCts: 전체 취소용 CTS -> token과 연결해서 사용
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        perCallbackTimeout ??= DefaultTimeoutSpan;
        var tasks = new UniTask<Exception>[list.Length];
        // 개별 콜백을 InvokeCallbackAsync()에 전달해서 실행 및 예외 검사
        for (int i = 0; i < list.Length; i++)
        {
            var d = list[i];
            tasks[i] = InvokeCallbackAsync(
                (Func<CancellationToken, UniTask>)d,
                d,
                semaphore,
                linkedCts,
                perCallbackTimeout,
                cancelAllOnFirstFailure,
                onException);
        }
        // 모든 콜백 병렬 실행 (maxConcurrency 만큼 묶어서)
        var results = await UniTask.WhenAll(tasks);

        if (!throwAggregated) return;
        
        // call back 실해 중 발생한 예외 전파
        // 예외가 없었다면 exceptions 리스트 생성x (-> for문 안에서 ??= 로 초기화)
        List<Exception> exceptions = null;
        for (int i = 0; i < results.Length; i++)
        {
            var ex = results[i];
            if (ex == null) continue;
            exceptions ??= new List<Exception>(maxConcurrency);
            exceptions.Add(ex);
        }

        if (exceptions is { Count: > 0 })
            throw new AggregateException(exceptions);
    }
    
    // InvokeAllCallbackAsync()의 개별 콜백 처리 매서드
    // callback: 멀티캐스트 내부 개별 콜백
    // originalDelegate: callback의 캐스팅 미적용 버전 델리게이트 객체 (예외 전파용)
    // semaphore: InvokeAllCallbackAsync()의 동시 실행 작업 제한용 세마포어
    // perCallbackTimeout: 개별 콜백에 의한 InvokeAllCallbackAsync 무한대기 방지용 시간 제한 TimeSpan
    // linkedCts: InvokeAllCallbackAsync()에서 사용하는 전체 콜백 공유 CTS 
    // cancelAllOnFirstFailure: true -> linkedCts.Cancel() 실행해서 전체 콜백 중단
    // onException: 콜백 실행 중 예외 발생 시 전달 델리게이트
    private static async UniTask<Exception> InvokeCallbackAsync(
        Func<CancellationToken, UniTask> callback,
        Delegate originalDelegate,
        SemaphoreSlim semaphore,
        CancellationTokenSource linkedCts,
        TimeSpan? perCallbackTimeout,
        bool cancelAllOnFirstFailure,
        Action<Exception, Delegate> onException)
    {
        
        await semaphore.WaitAsync(linkedCts.Token);

        try
        {
            UniTask work;
            try { work = callback(linkedCts.Token); }
            catch (Exception e)
            {
                onException?.Invoke(e, originalDelegate);
                if (cancelAllOnFirstFailure) linkedCts.Cancel();
                return e;
            }

            // 개별 콜백에 의한 InvokeAllCallbackAsync 무한 대기 방지
            // 시간 제한을 넘으면 해당 콜백을 무시하고 다음 작업 수행
            // -> 콜백을 강제로 중단하지 않음
            // -> 반드시 콜백이 전달받은 토큰 검사 로직을 포함해서 스스로 중단해야함
            if (perCallbackTimeout.HasValue)
            {
                await work.AttachExternalCancellation(linkedCts.Token)
                          .Timeout(perCallbackTimeout.Value);
            }
            else
            {
                await work.AttachExternalCancellation(linkedCts.Token);
            }

            return null;
        }
        catch (TimeoutException timeOverException)
        {
            onException?.Invoke(timeOverException, originalDelegate);
            if (cancelAllOnFirstFailure) linkedCts.Cancel();
            return timeOverException;
        }
        catch (OperationCanceledException opCancelException) when (linkedCts.IsCancellationRequested)
        {
            onException?.Invoke(opCancelException, originalDelegate);
            return opCancelException;
        }
        catch (Exception e)
        {
            onException?.Invoke(e, originalDelegate);
            if (cancelAllOnFirstFailure) linkedCts.Cancel();
            return e;
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    
    #endregion

    #region Addressables
    
#if UNITY_EDITOR
    public static string GetAddressKeyInEditor(AssetReference assetRef)
    {
        if (string.IsNullOrEmpty(assetRef.AssetGUID)) return null;
        return GetAddressKeyInEditor(assetRef.AssetGUID);
    }

    public static string GetAddressKeyInEditor(string assetGuid)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return null;
        
        var entry = settings.FindAssetEntry(assetGuid);
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