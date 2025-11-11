using System;
using System.Collections;
using System.Collections.Generic;
using TH.Utils;
using UnityEngine;

namespace RPG.Saving
{
    public interface ISavable
    {
        object CaptureState(); // 세이브 데이터 반환
        bool RestoreState(object state); // 세이브 적용 여부 반환
    }

    public interface ISavableEntity : ISavable
    {
        string UniqueIdentifier { get; }
        bool IsGlobal { get; }
    }
}

