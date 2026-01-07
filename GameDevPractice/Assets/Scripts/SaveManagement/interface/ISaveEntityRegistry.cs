using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using TH.Resource;

namespace TH.SaveLoad
{
    /// <summary>
    /// ISavableEntity 등록/해제용 인터페이스.
    /// ISavableEntity 구현체는 이 인터페이스에만 의존하여 자가 등록을 수행합니다.
    /// </summary>
    public interface ISaveEntityRegistry
    {
        void RegisterEntity(ISavableEntity entity, bool saveImmediately = false, CancellationToken token = default);
        void UnRegisterEntity(ISavableEntity entity, CancellationToken token = default);
        
        void SetCatalogAccessor(Func<SceneCatalogSO> catalogAccessor);
        
        void ProcessPendingRegistrations(SceneEntry currentSceneEntry);
        
        bool TryGetCachedState(string id, out Dictionary<string, object> stateDict);
        void UpdateStateCache(string id, Dictionary<string, object> stateDict);
        
        bool TryGetSceneSavableEntries(SceneEntry targetSceneEntry, out ICollection<ISavableEntity> entityCollection);
        ICollection<ISavableEntity> GetGlobalEntities();
    }
}
