using UnityEngine;

namespace TH.SaveLoad
{
    public interface ISaveFileHandler
    {
        SaveFileData LoadFile(string saveFile);
        void SaveFile(string saveFile, SaveFileData data);

        string GetSaveFileName();
        string GetPathFromSaveFile(string saveFile);
    }
}

