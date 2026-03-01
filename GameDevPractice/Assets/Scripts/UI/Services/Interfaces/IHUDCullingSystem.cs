using UnityEngine;

namespace TH.UI.Service
{
    public interface IHUDCullingSystem
    {
        HUDCullingSystem.CullingHandle Register(ICullingTargetView view, float sphereRadius = 0.5f);
        void Unregister(in HUDCullingSystem.CullingHandle handle);
        void Tick();
    }
}

