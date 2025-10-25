using UnityEngine;

namespace TH.Utils
{
    public interface IFloatingTextSpawner
    {
        void Register(object source, FloatingTextEventType eventType);
        void UnRegister(object source, FloatingTextEventType eventType);
    }

    public enum FloatingTextEventType
    {
        Damage,
        Heal,
        GetXp,
    }
}

