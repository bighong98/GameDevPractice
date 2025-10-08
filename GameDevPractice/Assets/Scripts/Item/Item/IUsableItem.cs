using UnityEngine;

namespace TH.Item
{
    public interface IUsableItem
    {
        bool Use();
        bool Use(object user);
    }
}

