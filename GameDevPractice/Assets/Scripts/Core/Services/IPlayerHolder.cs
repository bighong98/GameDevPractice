using System;

namespace TH.Core.Service
{
    public interface IPlayerHolder
    {
        event Action<object> OnPlayerInstanceUpdated;
        object GetPlayerInstance { get; }
        void SetPlayer(object player);
    }
}

