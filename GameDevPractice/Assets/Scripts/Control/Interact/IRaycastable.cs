using UnityEngine;

namespace TH.Control
{
    public interface IRaycastable
    {
        bool HandleRaycast(IPlayerController caller);
        CursorType GetCursorType();
    }
}

