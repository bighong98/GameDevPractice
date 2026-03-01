using System.Collections.Generic;
using UnityEngine;

namespace TH.SaveLoad
{
    public sealed class SaveDirtyTracker : ISaveDirtyTracker
    {
        private readonly HashSet<string> scene = new();
        private readonly HashSet<string> global = new();
        private readonly HashSet<string> tombstonesScene = new();
        private readonly HashSet<string> tombstonesGlobal = new();

        private int lastMarkedFrame = -1;

        public void MarkDirty(string id, bool isGlobal)
        {
            if (Time.frameCount == lastMarkedFrame) return;
            lastMarkedFrame = Time.frameCount;

            if (isGlobal) global.Add(id);
            else scene.Add(id);
        }

        public void MarkDeleted(string id, bool isGlobal)
        {
            if (isGlobal) tombstonesGlobal.Add(id);
            else tombstonesScene.Add(id);
        }

        public bool IsEmpty =>
            scene.Count == 0 && global.Count == 0 && tombstonesGlobal.Count == 0 && tombstonesScene.Count == 0;
    
        public void ClearAll()
        {
            scene.Clear(); 
            global.Clear();
            tombstonesScene.Clear(); 
            tombstonesGlobal.Clear();
        }
    
        public IEnumerable<string> SceneDirty => scene;
        public IEnumerable<string> GlobalDirty => global;
        public IEnumerable<string> SceneTombstones => tombstonesScene;
        public IEnumerable<string> GlobalTombstones => tombstonesGlobal;
    }

}
