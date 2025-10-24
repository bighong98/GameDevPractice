using UnityEngine;

namespace TH.Utils
{
    public interface IFloatingTextSpawner
    {
        void Register(object sender, FloatingTextEventType eventType);
        void UnRegister(object sender, FloatingTextEventType eventType);
    }

    public enum FloatingTextEventType
    {
        Damage,
        Heal,
        GetXp,
    }
}

