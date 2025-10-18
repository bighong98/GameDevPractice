using System;
using UnityEngine;

namespace TH.UI
{
    public interface IClickableStorageUI
    {
        event Action<int> OnSlotClicked; // 주 상호작용 (터치패드 터치, 마우스 좌클릭 등)
    }

    public interface ISubClickableStorageUI
    {
        event Action<int> OnSlotSubClicked; // 보조 상호작용 (마우스 우클릭 등)
    }
}

