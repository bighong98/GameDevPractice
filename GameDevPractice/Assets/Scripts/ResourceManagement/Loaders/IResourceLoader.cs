using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TH.Resource
{
    public interface IResourceLoader
    {
        // 비동기 리소스 로드 (await 지원)
        UniTask<T> LoadAsync<T>(string key, CancellationToken token = default) where T : Object;
        UniTask<T> LoadAsync<T>(AssetReference assetRef, CancellationToken token = default) where T : Object;
        // 동기 리소스 로드 (캐싱된 리소스가 있는 경우 true 반환)
        bool TryLoad<T>(string key, out T resource) where T : Object;
        bool TryLoad<T>(AssetReference assetRef, out T resource) where T : Object;
        UniTask<Sprite> LoadSpriteFromAtlasAsync(string atlasTagOrAddress, string spriteName, CancellationToken token = default);
        bool TryLoadSpriteFromAtlas(string atlasTagOrAddress, string spriteName, out Sprite sprite);
        // 라벨 별 리소스 일괄 로드 대기
        // 가급적 OnLabelResourcesLoadedAll에 직접 등록하기보다 waitForPreLoad() 사용 권장 (초기화 순서 꼬임 방지)
        void WaitForPreLoad(string label, Action callback);
        event Action<string> OnLabelResourcesLoadedAll;
        // 라벨 별 리소스 일괄 로드 상태 확인
        bool IsLoadedAll(string label);

        UniTask PreLoadAsync();
        
        // 라벨 별 리소스 일괄 로드 진척도 확인
        // IProgressSubscription 객체 반환 (호출자 측에서 필요할 때에 .Dispose 호출 목적)
        // label: 진척도 추적 필요한 라벨 이름
        // onProgress: 진척도 전달용 콜백
        // fireCurrent: true -> 구독과 동시에 현재 진척도 즉시 확인
        // 전체 PreLoad 진행도 구독 (모든 라벨의 가중치 기반 합산)
        IBroadcastSubscription SubscribeGlobalPreLoadProgress(Action<(float, string)> onProgress, bool fireCurrent = true);
        // 현재 진행 중인 모든 리소스 일괄 로드 진척도 총합 확인
        // 정확한 계산 대신 내부 가중치 적용
        IBroadcastSubscription SubscribePreLoadProgress(string label, Action<(float, string)> onProgress, bool fireCurrent = true); }
    }

