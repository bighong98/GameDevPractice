using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

public class ResourceManager : Singleton<ResourceManager>
{
    // 실제로 메모리에 올려진 리소스(by addressable)
    private readonly Dictionary<string, UnityEngine.Object> resources = new Dictionary<string, UnityEngine.Object>();
    private readonly List<AsyncOperationHandle<UnityEngine.Object>> handles = new List<AsyncOperationHandle<Object>>();
    
    public event Action<bool> NotifyPreLoad;
    private bool preLoadState = false;
    public bool PreLoadState => preLoadState;

    private const string PreLoadLabel = "PreLoad";
    private const string SpriteAtlasSuffix = "(Clone)"; // 스프라이트 아틀라스 내부 리소스 접근용 문자열
    private int atlasSuffixLength; // 캐싱된 "(Clone)" 문자열 길이

    protected override void Awake()
    {
        base.Awake();
        if (IsInvalidInstance()) return; // 중복 인스턴스인 경우 Init() 실행x
        Init();
    }

    #region Initialization

    private enum PreLoadSequence
    {
        // Addressable Groups의 Label 이름과 동일해야함
        PreLoad1,
        PreLoad2,
        PreLoad3,
        // 마지막에 PreLoad 라벨의 에셋을 로드
    }
    
    private void Init()
    {
        atlasSuffixLength = SpriteAtlasSuffix.Length;
        // PreLoad();
        PreLoadAsync().Forget();
    }

    private void PreLoad()
    {
        // 프로그램 시작과 동시에 필요한(PreLoad 라벨이 붙은) 모든 리소스 로드
        LoadAllAsync<UnityEngine.Object>(PreLoadLabel, 
            (key, count, totalCount) =>
            {
                Util.Log($"{key} {count} / {totalCount}"); // 디버깅용 로그
                if (count == totalCount)
                {
                    NotifyPreLoad?.SafeInvoke(true); // 리소스 로딩 대기중인 클래스들에게 로딩 완료 이벤트 전달
                    preLoadState = true;
                }
            });
    }

    private async UniTaskVoid PreLoadAsync()
    {
        foreach (var label in Enum.GetNames(typeof(PreLoadSequence)))
        {
            await LoadAllAsyncAwaitable<UnityEngine.Object>(label, (key, count, totalCount) =>
            {
                // Util.Log($"{key} {count} / {totalCount}"); // 디버깅용 로그
            });
        }
        PreLoad();
    }

    public void SubscribePreLoad(Action<bool> callback)
    {
        if (preLoadState)
        {
            callback?.Invoke(true);
        }
        else
            NotifyPreLoad += callback;
    }

    #endregion

    #region Load from Cached Dicionary (resources<string, Object>)

    public T Load<T>(string key) where T : UnityEngine.Object
    {
        if (resources.TryGetValue(key, out Object resource)) // key로 resources 딕셔너리에서 검색
        {
            return resource as T;
        }

        return null; // 등록된 리소스가 없으면 null 반환
    }

    public GameObject Instantiate(string key, Transform parent = null)
    { // 원하는 리소스를 곧바로 씬에 올리고 싶은 경우 사용 (오브젝트 풀링 미적용)
        var origin = Load<GameObject>(key);
        if (origin == null)
        {
            Util.Log($"{nameof(ResourceManager)}.Instantiate: Failed to load prefab: {key}");
            return null;
        }

        GameObject clone = Object.Instantiate(origin, parent); // 원본의 사본 생성
        clone.name = origin.name; // 사본이 원본과 동일한 이름을 가지도록
        
        return clone;
    }

    public void Destroy(GameObject go)
    {
        if (go == null) return;
        
        Object.Destroy(go);
    }

    public bool AddResource(string key, UnityEngine.Object obj) // key 중복 불가
    {
        return resources.TryAdd(key, obj);
    }

    #endregion

    #region Load from Addressable
    
    
    private void LoadAsync<T>(string key, Action<T> callback = null) where T : UnityEngine.Object
    { // key를 사용해 어드레서블로부터 단일 리소스 비동기 로딩
        string loadKey = key;
        if (key.EndsWith(".sprite"))
        {
            loadKey = $"{key}[{key.Replace(".sprite", "")}]";
        }

        var asyncOperation = Addressables.LoadAssetAsync<T>(loadKey); // 어드레서블로부터 리소스 로딩
        asyncOperation.Completed += (op) =>
        { // 로딩이 완료된 후 callback 실행
            if (resources.TryGetValue(key, out Object resource)) // 중복 key를 사용하는 리소스가 있는 경우
            {
                callback?.Invoke(op.Result); // 콜백만 실행하고 저장x
                return;
            }

            resources.Add(key, op.Result); // 신규 리소스 딕셔너리에 저장
            callback?.Invoke(op.Result); // 콜백 실행
        };
    }

    private void LoadMultipleSpriteAsync(string key, Action<Sprite[]> callback = null)
    { // Sprite Mode: Multiple 전용. 반드시 key 끝에 ".multiSprite" 붙일 것 (대소문자 주의)
        var asyncOperation = Addressables.LoadAssetAsync<Sprite[]>(key);
        asyncOperation.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var sprite in op.Result)
                {
                    if (resources.ContainsKey(sprite.name))
                        continue;
                    resources.Add($"{key}[{sprite.name}]", sprite); // 포맷: {멀티 스프라이트 이름}[{내부 스프라이트 개별 이름}]
                }
                callback?.Invoke(op.Result); // 로딩 완료 후 콜백 실행
            }
            else
            {
                Util.Log($"{nameof(ResourceManager)}.LoadMultipleSpriteAsync: Failed to load Multiple Sprite[] with key: {key}");
                callback?.Invoke(null);
            }
        };
    }

    private void LoadSpriteAtlasAsync(string key, Action<SpriteAtlas> callback = null)
    { // Sprite Atlas 전용. 반드시 key 끝에 ".spriteAtlas" 붙일 것 (대소문자 주의)
        var asyncOperation = Addressables.LoadAssetAsync<SpriteAtlas>(key);
        asyncOperation.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                SpriteAtlas atlas = op.Result;
                Sprite[] sprites = new Sprite[atlas.spriteCount];

                for (int i = 0; i < atlas.GetSprites(sprites); i++)
                {
                    string subKey =
                        sprites[i].name.EndsWith(SpriteAtlasSuffix) // if (sprites[i].name.EndsWith("(Clone)")
                            ? $"{key}[{sprites[i].name[atlasSuffixLength]}]" // 이름 뒷부분 "(Clone)" 문자열 제거
                            : $"{key}[{sprites[i].name}]";

                    if (resources.ContainsKey(subKey)) continue; // 중복 키 사용중인 리소스가 존재하는 경우 스킵

                    resources[subKey] = sprites[i];
                }

                callback?.Invoke(op.Result);
            }
            else
            {
                Util.Log($"{nameof(ResourceManager)}.LoadSpriteAtlasAsync: Failed to load sprite atlas with key: {{key}}");
                callback?.Invoke(null);
            }
        };
    }

    // 동일한 라벨(label)이 붙은 모든 리소스 비동기 로딩
    // callback은 key, loadCount, totalCount 전달용 (로딩 상태 확인, 로딩바 등에 사용) 
    private void LoadAllAsync<T>(string label, Action<string, int, int> callback) where T : UnityEngine.Object
    { 
        var opHandle = Addressables.LoadResourceLocationsAsync(label, typeof(T));
        opHandle.ReleaseHandleOnCompletion();
        opHandle.Completed += (op) =>
        {
            int loadCount = 0;
            int totalCount = op.Result.Count;
            
            foreach (var result in op.Result)
            {
                if (result.PrimaryKey.EndsWith(".sprite"))
                {
                    LoadAsync<Sprite>(result.PrimaryKey, (obj) =>
                    {
                        loadCount++;
                        callback?.Invoke(result.PrimaryKey, loadCount, totalCount);
                    });
                }
                else if (result.PrimaryKey.Contains(".multiSprite"))
                {
                    LoadMultipleSpriteAsync(result.PrimaryKey, (_) =>
                    {
                        loadCount++;
                        callback?.Invoke(result.PrimaryKey, loadCount, totalCount);
                    });
                }
                else if (result.PrimaryKey.Contains(".spriteAtlas"))
                {
                    LoadSpriteAtlasAsync(result.PrimaryKey, (_) =>
                    {
                        loadCount++;
                        callback?.Invoke(result.PrimaryKey, loadCount, totalCount);
                    });
                }
                else
                {
                    LoadAsync<T>(result.PrimaryKey, (obj) =>
                    {
                        loadCount++;
                        callback?.Invoke(result.PrimaryKey, loadCount, totalCount);
                    });
                }
            }
        };
    }
    
    private async UniTask LoadAllAsyncAwaitable<T>(string label, Action<string, int, int> callback = null)
        where T : UnityEngine.Object
    {
        var handle = Addressables.LoadResourceLocationsAsync(label, typeof(T));
        await handle.Task;

        var results = handle.Result;
        int totalCount = results.Count;
        int loadCount = 0;

        foreach (var result in results)
        {
            var tcs = new UniTaskCompletionSource();

            string key = result.PrimaryKey;

            if (key.EndsWith(".sprite"))
            {
                LoadAsync<Sprite>(key, _ =>
                {
                    loadCount++;
                    callback?.Invoke(key, loadCount, totalCount);
                    tcs.TrySetResult();
                });
            }
            else if (key.Contains(".multiSprite"))
            {
                LoadMultipleSpriteAsync(key, _ =>
                {
                    loadCount++;
                    callback?.Invoke(key, loadCount, totalCount);
                    tcs.TrySetResult();
                });
            }
            else if (key.Contains(".spriteAtlas"))
            {
                LoadSpriteAtlasAsync(key, _ =>
                {
                    loadCount++;
                    callback?.Invoke(key, loadCount, totalCount);
                    tcs.TrySetResult();
                });
            }
            else
            {
                LoadAsync<T>(key, _ =>
                {
                    loadCount++;
                    callback?.Invoke(key, loadCount, totalCount);
                    tcs.TrySetResult();
                });
            }

            await tcs.Task;
        }
        Addressables.Release(handle);
    }

    #region Deprecated

    // private UniTask<T> LoadAsyncAwaitable<T>(string key, Action<T> callback = null) where T : UnityEngine.Object
    // {
    //     var tcs = new UniTaskCompletionSource<T>();
    //     
    //     LoadAsync<T>(key, (obj) =>
    //     {
    //         if (obj == null)
    //         {
    //             Util.LogError($"[LoadAsyncAwaitable] Failed to load {key}");
    //             tcs.TrySetResult(null);
    //             return;
    //         }
    //         
    //         callback?.Invoke(obj);
    //         tcs.TrySetResult(obj);
    //     });
    //
    //     return tcs.Task;
    // }

    #endregion

    #endregion
}
