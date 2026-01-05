using System;
using System.Collections.Generic;
using System.IO;
using TH.Utils;
using Unity.Serialization.Json;
using UnityEngine;

namespace TH.SaveLoad
{
    public sealed class SaveFileHandler : ISaveFileHandler
    {
        #region ISaveFileHandler

        public SaveFileData LoadFile(string saveFile)
        {
            this.Log($"LoadFile() 시작 - saveFile: {saveFile}");
            string path = GetPathFromSaveFile(saveFile);
            this.Log($"파일 경로: {path}");
            if (!File.Exists(path))
            {
                this.Log($"파일 없음 - 새 SaveFileData 반환");
                return new SaveFileData();
            }
            
            this.Log($"[SaveSystem] 파일 존재 - 읽기 시도");
            try
            {
                // json -> 런타임 데이터로 파싱 시도
                string json = File.ReadAllText(path);
                var data = JsonSerialization.FromJson<SaveFileData>(json);
                // Json 역직렬화 후 null 체크 및 초기화
                data.globalData ??= new List<SavableEntry>();
                data.sceneData ??= new Dictionary<string, List<SavableEntry>>();
                return data; 
            }
            catch (Exception e)
            {
                // 세이브파일 파싱 실패 시 빈 세이브 파일 생성 및 반환
                Debug.LogError($"[SaveSystem] Failed to load file {path}: {e.Message}");
                return new SaveFileData();
            }
        }

        public void SaveFile(string saveFile, SaveFileData data)
        {
            this.Log($"SaveFile() 시작 - saveFile: {saveFile}");
            string path = GetPathFromSaveFile(saveFile);
            this.Log($"저장 경로: {path}");
            // 디렉토리 확보
            var dir  = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            
            var tmp = Path.Combine(dir ?? "", $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            var bak = path + ".bak";

            string json;
            this.Log($"JSON 직렬화 시작");
            try
            {
                // json 직렬화 시도
                json = JsonSerialization.ToJson(
                    data,
                    new JsonSerializationParameters
                    {
                        DisableSerializedReferences = true
                    });
            }
            catch (Exception e)
            {
                Logg.LogError($"[{nameof(SaveSystem)}.{nameof(SaveFile)}()] JsonSerialization failed {e}");
                return; // 데이터 직렬화 실패 시 중지
            }
            
            this.Log($"JSON 직렬화 완료 - 길이: {json.Length} chars");
            this.Log($"임시 파일 쓰기 시작");
            try
            {
                using var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None);
                using var sw = new StreamWriter(fs);
                sw.Write(json);
                sw.Flush();
                fs.Flush(true);
            }
            catch (Exception e)
            {
                Logg.LogError($"[{nameof(SaveSystem)}.{nameof(SaveFile)}()] Writing tmp failed: {tmp}, {e}");
                return; // tmp 파일 생성 실패 시 중지
            }
            
            this.Log($"[SaveSystem] 임시 파일 쓰기 완료");
            // 3) Replace 시도
            this.Log($"[SaveSystem] 파일 교체 시도");
            // (path = tmp)
            try
            {
                if (File.Exists(path)) // 성공 시: bak = path, path = tmp 으로 교체
                    File.Replace(tmp, path, bak);   
                else File.Move(tmp, path); // 실패 시 : path에 저장
            }
            catch (Exception e)
            {
                Logg.Log($"[SaveSystem.SaveFile] Replace fallback: {e.Message}", Logg.LoggingMode.Completed);
                try
                {
                    // 백업 시도
                    if (File.Exists(path))
                    {
                        // 백업 실패 시 throw 하지 않고 그대로 overwrite 시도
                        try { File.Copy(path, bak, overwrite: true); } catch { }
                        try { File.Delete(path); } catch { }
                    }

                    // Move가 막히면 Copy(overwrite)
                    try { File.Move(tmp, path); }
                    catch { File.Copy(tmp, path, overwrite: true); File.Delete(tmp); }
                }
                catch (Exception fbEx) { Logg.LogError($"[SaveSystem.SaveFile] Fallback failed: {fbEx}"); }
            }
            this.Log($"[SaveSystem] SaveFile() 완료");
        }

        #endregion
        
        #region SaveFile name/path

        // 추후 세이브 시스템 확장을 고려하여 세이브 파일명 캐시 관리

        private string lastUsedSaveFileName;
        
        public string GetSaveFileName()
        {
            return string.IsNullOrEmpty(lastUsedSaveFileName) 
                ? Constants.DefaultSaveFile : lastUsedSaveFileName;
        }

        private void CacheSaveFileName(string saveFileName)
        {
            if (string.IsNullOrEmpty(saveFileName))
            {
                Logg.LogWarning($"[{GetType().Name}] saveFileName is null or empty");
                return;
            }
            lastUsedSaveFileName = saveFileName;
        }
        
        // 경로 생성 (임시)
        public string GetPathFromSaveFile(string saveFile)
        {
            return Path.Combine(Application.persistentDataPath, saveFile + ".sav");
        }
        
        
        #endregion
    }
}

