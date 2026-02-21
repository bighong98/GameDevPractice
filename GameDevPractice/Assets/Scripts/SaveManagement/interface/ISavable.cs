using System;
using System.Collections;
using System.Collections.Generic;
using TH.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TH.SaveLoad
{
    public interface ISavable
    {
        object CaptureState(); // 세이브 데이터 반환
        bool RestoreState(object state); // 세이브 적용 여부 반환
        void ResetToDefaultState(); // 인스턴스 상태 기본값 초기화
    }

    public interface ISavableEntity : ISavable
    {
        string UniqueIdentifier { get; }
        bool IsGlobal { get; }
        bool IsRegistered {get; set;}
        Scene TargetScene { get; }
    }
}

