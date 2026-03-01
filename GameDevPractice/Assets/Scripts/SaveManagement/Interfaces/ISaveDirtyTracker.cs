using System.Collections.Generic;
using UnityEngine;

namespace TH.SaveLoad
{
    public interface ISaveDirtyTracker
    {
        void MarkDirty(string id, bool isGlobal);
        void MarkDeleted(string id, bool isGlobal);
        void ClearAll();
        bool IsEmpty { get; }
        
        public IEnumerable<string> SceneDirty { get; }
        public IEnumerable<string> GlobalDirty { get; }
        public IEnumerable<string> SceneTombstones { get; }
        public IEnumerable<string> GlobalTombstones { get; }
    }
}


