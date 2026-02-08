using System;

namespace TH.UI.Service
{
    public interface IHUDCullingBindable
    {
        void ConfigureCulling(Func<ICullingTargetView, HUDCullingSystem.CullingHandle> register,
            Action<HUDCullingSystem.CullingHandle> unregister);
    }
}
