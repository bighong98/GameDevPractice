using System.Collections.Generic;
using UnityEngine;

namespace RPG.Saving
{
    [System.Serializable]
    public class SaveFileData
    {
        public int lastSceneBuildIndex; // 세이브 시점 씬 인덱스
        public Dictionary<int, List<SavableEntry>> sceneEntries = new(); // 씬에 종속된 오브젝트 데이터
        public List<SavableEntry> globalEntries = new(); // 씬과 무관하게 유지되어야하는 데이터
    }
}

