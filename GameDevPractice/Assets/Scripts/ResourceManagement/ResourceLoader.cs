using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;
using Cysharp.Threading.Tasks;

namespace TH.Resource
{
    public class ResourceLoader : IResourceLoader
    {
        private readonly Dictionary<string, AsyncOperationHandle> resourceKeys = new Dictionary<string, AsyncOperationHandle>();
        private readonly Dictionary<AssetReference, AsyncOperationHandle> resourceAssetRefs = new Dictionary<AssetReference, AsyncOperationHandle>();
        private readonly Dictionary<string, bool> loadStatus = new Dictionary<string, bool>();
        
        public event Action<string> NotifyResourceLoad;

        enum PreLoadLabels
        {
            PreLoad1,
            PreLoad2,
            PreLoad3,
        }

        private const string PreLoadLabel = "PreLoad";
        private const string SpriteAtlasSuffix = "(Clone)"; // 스프라이트 아틀라스 내부 리소스 접근용 문자열
        private int atlasSuffixLength; // 캐싱된 "(Clone)" 문자열 길이

        public ResourceLoader()
        {
            PreLoad();
        }
        
        private void PreLoad()
        {
            PreLoadAsync().ContinueWith(() =>
            {
                LoadAllAsync<UnityEngine.Object>(PreLoadLabel, 
                    (key, count, totalCount) =>
                    {
                        Util.Log($"[{PreLoadLabel} - {key}] {count} / {totalCount}", Util.LoggingMode.Completed); // 디버깅용 로그
                        if (count == totalCount)
                        {
                            NotifyResourceLoad?.Invoke(PreLoadLabel);// 리소스 로딩 대기중인 클래스들에게 로딩 완료 이벤트 전달
                        }
                    });
            });
        }

        private async UniTask PreLoadAsync()
        {
            foreach (var label in Enum.GetNames(typeof(PreLoadLabels)))
            {
                await LoadAllAsyncAwaitable<UnityEngine.Object>(label, (key, count, totalCount) =>
                {
                    if (count == totalCount)
                    {
                        NotifyResourceLoad?.Invoke(label);// 리소스 로딩 대기중인 클래스들에게 로딩 완료 이벤트 전달
                        loadStatus[label] = true;
                    }
                });
            }
        }

        private void LoadAsync<T>(string key, Action<T> callback = null) where T : UnityEngine.Object
        {
            string loadKey = key;
            if (key.EndsWith(".sprite"))
            {
                loadKey = $"{key}[{key.Replace(".sprite", "")}]";
            }

            var asyncOperation = Addressables.LoadAssetAsync<T>(loadKey); // 어드레서블로부터 리소스 로딩
            asyncOperation.Completed += (op) =>
            {
                // 로딩이 완료된 후 callback 실행
                if (resourceKeys.TryGetValue(key, out AsyncOperationHandle resource)) // 중복 key를 사용하는 리소스가 있는 경우
                {
                    callback?.Invoke(op.Result); // 콜백만 실행하고 저장x
                    return;
                }

                // resourceKeys.Add(key, op.Result); // 신규 리소스 딕셔너리에 저장
                resourceKeys.Add(key, op); // 신규 리소스 딕셔너리에 저장
                callback?.Invoke(op.Result); // 콜백 실행
            };
        }

        private void LoadAllAsync<T>(string label, Action<string, int, int> callback = null) where T : UnityEngine.Object
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

        private void LoadMultipleSpriteAsync(string key, Action<Sprite[]> callback = null)
        {
            // Sprite Mode: Multiple 전용. 반드시 key 끝에 ".multiSprite" 붙일 것 (대소문자 주의)
            // var asyncOperation = Addressables.LoadAssetAsync<Sprite[]>(key);
            // asyncOperation.Completed += (op) =>
            // {
            //     if (op.Status == AsyncOperationStatus.Succeeded)
            //     {
            //         foreach (var sprite in op.Result)
            //         {
            //             if (resourceKeys.ContainsKey(sprite.name))
            //                 continue;
            //             resourceKeys.Add($"{key}[{sprite.name}]", sprite); // 포맷: {멀티 스프라이트 이름}[{내부 스프라이트 개별 이름}]
            //         }
            //
            //         callback?.Invoke(op.Result); // 로딩 완료 후 콜백 실행
            //     }
            //     else
            //     {
            //         Util.Log(
            //             $"{nameof(ResourceManager)}.LoadMultipleSpriteAsync: Failed to load Multiple Sprite[] with key: {key}");
            //         callback?.Invoke(null);
            //     }
            // };
        }

        private void LoadSpriteAtlasAsync(string key, Action<SpriteAtlas> callback = null)
        {
            // Sprite Atlas 전용. 반드시 key 끝에 ".spriteAtlas" 붙일 것 (대소문자 주의)
            // var asyncOperation = Addressables.LoadAssetAsync<SpriteAtlas>(key);
            // asyncOperation.Completed += (op) =>
            // {
            //     if (op.Status == AsyncOperationStatus.Succeeded)
            //     {
            //         SpriteAtlas atlas = op.Result;
            //         Sprite[] sprites = new Sprite[atlas.spriteCount];
            //
            //         for (int i = 0; i < atlas.GetSprites(sprites); i++)
            //         {
            //             string subKey =
            //                 sprites[i].name.EndsWith(SpriteAtlasSuffix) // if (sprites[i].name.EndsWith("(Clone)")
            //                     ? $"{key}[{sprites[i].name[atlasSuffixLength]}]" // 이름 뒷부분 "(Clone)" 문자열 제거
            //                     : $"{key}[{sprites[i].name}]";
            //
            //             if (resourceKeys.ContainsKey(subKey)) continue; // 중복 키 사용중인 리소스가 존재하는 경우 스킵
            //
            //             resourceKeys[subKey] = sprites[i];
            //         }
            //
            //         callback?.Invoke(op.Result);
            //     }
            //     else
            //     {
            //         Util.Log(
            //             $"{nameof(ResourceManager)}.LoadSpriteAtlasAsync: Failed to load sprite atlas with key: {{key}}");
            //         callback?.Invoke(null);
            //     }
            // };
        }

        public async UniTask<T> LoadAsync<T>(AssetReference assetRef) where T : UnityEngine.Object
        {
            // if (!(assetRef?.RuntimeKeyIsValid() ?? false))
            // {
            //     Debug.LogError($"[{nameof(LoadAsync)}] AssetReference is null or runtime key is invalid {assetRef?.SubObjectName}");
            //     return null;
            // }
            //
            // if (!resourceAssetRefs.TryGetValue(assetRef, out var result) ||
            //     !result.IsValid())
            // {
            //     resourceAssetRefs[assetRef] = assetRef.LoadAssetAsync<T>();
            // }
            
            if (assetRef == null)
            {
                Debug.LogError($"[{nameof(LoadAsync)}] reference is null.");
                return null;
            }

            if (!assetRef.RuntimeKeyIsValid())
            {
                Debug.LogError($"[{nameof(LoadAsync)}] Invalid RuntimeKey for AssetReference<{typeof(T).Name}>. Asset: {assetRef.Asset?.name}");
                return null;
            }

            var handle = assetRef.OperationHandle.IsValid()
                ? assetRef.OperationHandle
                : assetRef.LoadAssetAsync<T>();

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[{nameof(LoadAsync)}] Load failed for AssetReference<{typeof(T).Name}> with key: {assetRef.RuntimeKey}");
                return null;
            }

            return handle.Result as T;
        }

        public bool TryLoad<T>(string key, out T resource) where T : UnityEngine.Object
        {
            if (resourceKeys.TryGetValue(key, out var result)
                && result.Result is T cachedResource)
            {
                resource = cachedResource;
                return true;
            }
            resource = null;
            return false;
        }

        public bool TryLoad<T>(AssetReference assetRef, out T resource) where T : UnityEngine.Object
        {
            if (resourceAssetRefs.TryGetValue(assetRef, out var result)
                && result.Result is T cachedResource)
            {
                resource = cachedResource;
                return true;
            }

            resource = null;
            return false;
        }

        public bool IsLoadedAll(string label)
        {
            return loadStatus.TryGetValue(label, out var status) && status;
        }
    }
}

