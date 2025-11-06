using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Saving
{
    public interface ISavable
    {
        object CaptureState(); // 세이브 데이터 반환
        bool RestoreState(object state); // 세이브 적용 여부 반환
    }

    public interface ISavableWithId : ISavable
    {
        string UniqueIdentifier { get; }
    }

    public interface ISavableDirtySignal
    {
        event Action OnDirty;
    }

    public interface ISavableDeleteSignal
    {
        event Action OnDeleted;
    }
}

