using System.Collections.Generic;
using TH.SaveLoad;
using UnityEngine;
using UnityEngine.Serialization;

namespace RPG.Saving
{
    [System.Serializable]
    public class SaveFileData
    {
        public SceneEntry lastSceneEntry;
        public Dictionary<string, List<SavableEntry>> sceneData = new(); // 씬에 종속된 오브젝트 데이터
        public List<SavableEntry> globalData = new(); // 씬과 무관하게 유지되어야하는 데이터
    }
}

