using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
// 추후 싱글톤을 경유해서 접근할 필요가 있는 오브젝트 관리 목적의 매니저
// 현재는 사용X

namespace TH.Deprecated
{
    public class ObjectManager : Singleton<ObjectManager>
    {
        protected override void Awake()
        {
            base.Awake();
            // if (IsInvalidInstance()) return;
            // Init();
        }

        protected override void InitOnce()
        {
        
        }

        protected override void InitOnceAfterPreLoad(bool isDone)
        {
        
        }

        protected override void Init()
        {
            // ResourceManager.Instance.SubscribePreLoad(InitAfterPreLoad);
            // GameSceneManager.Instance.RegisterCleanupTask(async () =>
            // {
            //     await Clear();  
            // });
        }

        protected override void InitAfterPreLoad(bool isDone)
        {
        
        }

        protected override UniTask Clear()
        {
            //todo: 씬 로드 전 정리할 작업 추가
            return base.Clear();
        }
    }
}

