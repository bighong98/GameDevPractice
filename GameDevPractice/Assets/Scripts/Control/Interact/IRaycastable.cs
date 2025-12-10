using UnityEngine;

namespace TH.Control
{
    public interface IRaycastable
    {
        bool HandleRaycast(PlayerController caller);
        CursorType GetCursorType();
    }
}

