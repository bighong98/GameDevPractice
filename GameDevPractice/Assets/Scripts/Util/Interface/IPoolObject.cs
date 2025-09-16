using UnityEngine;

namespace TH.Core.Pool
{
    public interface IPoolObject
    {
        GameObject Origin { get; set; } // 풀에서 최초 생성시 초기화됨. 외부에서 수정하지 않는 것을 권장함  
        PoolKey PoolKey { get; set; } // 오브젝트 풀에서 사용되는 {prefab, instanceType}을 저장
        void OnCreateFromPool(); // 오브젝트 풀로부터 최초 생성 시 실행 (순서: Awake -> OnEnable -> OnCreateFromPool -> OnGetFromPool)
        void OnGetFromPool(); // Get() 호출 시 항상 실행 (순서: Awake -> OnEnable -> OnGetFromPool)
        void OnReleaseFromPool(); // ObjectPool.Release()나 PoolingManager.ReleaseFromPool() 호출 시 항상 실행
        void OnDestroyFromPool(); // 소속 풀에서 Clear(), Dispose()가 발생했을 때 실행
        void ReleaseSelf(); // 외부에서 풀에 반환시킬 목적. 다음과 같이 구현 -> PoolingManager.Instance.ReleaseFromPool(this);
    }
}
