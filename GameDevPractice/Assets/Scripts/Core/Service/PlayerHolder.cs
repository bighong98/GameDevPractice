using System;
using TH.Control;

namespace TH.Core.Service
{
    public class PlayerHolder : IPlayerHolder
    {
        private object currentPlayerInstance;
        public object GetPlayerInstance { get { if (!currentPlayerInstance.IsNotNull()) {
                    currentPlayerInstance = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        } return currentPlayerInstance; }}

        public event Action<object> OnPlayerInstanceUpdated;

        public void SetPlayer(object player)
        {
            if (currentPlayerInstance == player) return;
            
            currentPlayerInstance = player;
            OnPlayerInstanceUpdated?.Invoke(player);
        }
    }
}
