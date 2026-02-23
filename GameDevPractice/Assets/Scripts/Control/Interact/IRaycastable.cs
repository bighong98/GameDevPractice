using UnityEngine;

namespace TH.Control
{
    public interface IRaycastable
    {
        bool HandleRaycast(IRaycastHolder caller);
        CursorType GetCursorType();
    }
}

