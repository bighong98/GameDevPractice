using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TH.SaveLoad
{
    public interface ISaveSystem
    {
        UniTask LoadLastScene(string saveFile);
        
        UniTask SaveAsync(string saveFile);
        UniTask DeleteAsync(string saveFile);
        UniTask LoadAsync(string saveFile);
    }
}

