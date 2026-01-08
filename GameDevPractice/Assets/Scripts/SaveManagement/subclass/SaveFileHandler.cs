using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TH.Utils;
using Unity.Serialization.Json;
using UnityEngine;

namespace TH.SaveLoad
{
    public sealed class SaveFileHandler : ISaveFileHandler
    {
        #region Constants
        
        private const string SaveDirectoryName = "Saves";
        private const string SaveFilePrefix = "Save_";
        private const string SaveFileExtension = ".sav";
        private const string DefaultSaveFile = "Save_001";
        
        // 슬롯 번호 추출용 정규식 (Save_001 -> 1)
        private static readonly Regex SlotNumberRegex = new Regex(@"Save_(\d{3})", RegexOptions.Compiled);
        
        #endregion
        
        private readonly string _saveRootDirectory;
        private readonly List<SaveFileInfo> _saveFileList;
        private string _lastUsedSaveFileName;
        
        // 존재하는 모든 세이브 파일 목록 (읽기 전용)
        public IReadOnlyList<SaveFileInfo> SaveFiles => _saveFileList;
        
        public SaveFileHandler()
        {
            _saveRootDirectory = Path.Combine(Application.persistentDataPath, SaveDirectoryName);
            _saveFileList = new List<SaveFileInfo>();
            
            InitializeDirectory();
            RefreshSaveFileList();
            
            this.Log($"SaveFileHandler 초기화 완료 - 루트 경로: {_saveRootDirectory}, 발견된 세이브 파일: {_saveFileList.Count}개");
        }
        
        #region Directory Initialization
        
        /// <summary>
        /// 세이브 디렉토리 초기화 - 없으면 생성
        /// </summary>
        private void InitializeDirectory()
        {
            try
            {
                if (Directory.Exists(_saveRootDirectory)) return;
                
                Directory.CreateDirectory(_saveRootDirectory);
                this.Log($"세이브 루트 디렉토리 생성: {_saveRootDirectory}");
            }
            catch (Exception e) { Logg.LogError($"[SaveFileHandler] 디렉토리 초기화 실패: {e.Message}"); }
        }
        
        /// <summary>
        /// 특정 슬롯의 디렉토리 확보
        /// </summary>
        private void EnsureSlotDirectory(string saveFileName)
        {
            string slotDir = GetSlotDirectory(saveFileName);
            if (!Directory.Exists(slotDir))
            {
                Directory.CreateDirectory(slotDir);
                this.Log($"슬롯 디렉토리 생성: {slotDir}");
            }
        }
        
        #endregion
        
        #region ISaveFileHandler - Save/Load File I/O

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
                
                // 로드 성공 시 캐시 업데이트
                CacheSaveFileName(saveFile);
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
                        try { File.Copy(path, bak, overwrite: true); } catch (Exception copyException) { Logg.LogWarning(copyException); }
                        try { File.Delete(path); } catch (Exception deleteException) { Logg.LogWarning(deleteException); }
                    }

                    // Move가 막히면 Copy(overwrite)
                    try { File.Move(tmp, path); }
                    catch { File.Copy(tmp, path, overwrite: true); File.Delete(tmp); }
                }
                catch (Exception fbEx) { Logg.LogError($"[SaveSystem.SaveFile] Fallback failed: {fbEx}"); }
            }
            this.Log($"[SaveSystem] SaveFile() 완료");
            
            // 저장 완료 후 캐시 및 목록 갱신
            CacheSaveFileName(saveFile);
            RefreshSaveFileList();
        }

        #endregion
        
        #region Save File Management
        
        /// <summary>
        /// 현재 사용 중인 세이브 파일명 반환
        /// </summary>
        public string GetSaveFileName()
        {
            return string.IsNullOrEmpty(_lastUsedSaveFileName) 
                ? DefaultSaveFile : _lastUsedSaveFileName;
        }
        
        /// <summary>
        /// 세이브 파일명 캐시
        /// </summary>
        private void CacheSaveFileName(string saveFileName)
        {
            if (string.IsNullOrEmpty(saveFileName))
            {
                Logg.LogWarning($"[{GetType().Name}] saveFileName is null or empty");
                return;
            }
            _lastUsedSaveFileName = saveFileName;
        }
        
        /// <summary>
        /// 세이브 파일명으로부터 전체 경로 반환
        /// 구조: Saves/{saveFileName}/{saveFileName}.sav
        /// </summary>
        public string GetPathFromSaveFile(string saveFile)
        {
            return Path.Combine(GetSlotDirectory(saveFile), saveFile + SaveFileExtension);
        }
        
        /// <summary>
        /// 슬롯 디렉토리 경로 반환
        /// </summary>
        private string GetSlotDirectory(string saveFileName)
        {
            return Path.Combine(_saveRootDirectory, saveFileName);
        }
        
        /// <summary>
        /// 세이브 파일 목록 갱신 - 디스크에서 실제 세이브 파일 검색
        /// </summary>
        public void RefreshSaveFileList()
        {
            _saveFileList.Clear();
            
            if (!Directory.Exists(_saveRootDirectory))
            {
                this.Log("세이브 루트 디렉토리 없음");
                return;
            }
            
            try
            {
                // 각 슬롯 폴더 탐색
                var slotDirectories = Directory.GetDirectories(_saveRootDirectory);
                
                foreach (var slotDir in slotDirectories)
                {
                    string slotName = Path.GetFileName(slotDir);
                    string saveFilePath = Path.Combine(slotDir, slotName + SaveFileExtension);

                    if (!File.Exists(saveFilePath)) continue;
                    
                    var fileInfo = new FileInfo(saveFilePath);
                    var saveInfo = new SaveFileInfo(
                        fileName: slotName,
                        saveDate: fileInfo.LastWriteTime,
                        filePath: saveFilePath
                    );
                        
                    _saveFileList.Add(saveInfo);
                }
                
                // 슬롯 번호 오름차순 정렬
                _saveFileList.Sort((a, b) => 
                    ExtractSlotNumber(a.FileName).CompareTo(ExtractSlotNumber(b.FileName)));
                
                this.Log($"세이브 파일 목록 갱신 완료: {_saveFileList.Count}개 발견");
            }
            catch (Exception e)
            {
                Logg.LogError($"[SaveFileHandler] 세이브 파일 목록 갱신 실패: {e.Message}");
            }
        }
        
        /// <summary>
        /// 가장 최근에 수정된 세이브 파일 반환
        /// </summary>
        public SaveFileInfo? GetMostRecentSaveFile()
        {
            if (_saveFileList.Count == 0)
                return null;
            
            return _saveFileList.OrderByDescending(s => s.SaveDate).FirstOrDefault();
        }
        
        /// <summary>
        /// 특정 세이브 파일 존재 여부 확인
        /// </summary>
        public bool SaveFileExists(string saveFileName)
        {
            string path = GetPathFromSaveFile(saveFileName);
            return File.Exists(path);
        }
        
        /// <summary>
        /// 세이브 파일 삭제 (슬롯 폴더 전체 삭제)
        /// </summary>
        public bool DeleteSaveFile(string saveFileName)
        {
            try
            {
                string slotDir = GetSlotDirectory(saveFileName);
                
                if (Directory.Exists(slotDir))
                {
                    Directory.Delete(slotDir, recursive: true);
                    RefreshSaveFileList();
                    this.Log($"세이브 파일 삭제 완료: {saveFileName}");
                    return true;
                }
                
                this.Log($"삭제할 세이브 파일 없음: {saveFileName}");
                return false;
            }
            catch (Exception e)
            {
                Logg.LogError($"[SaveFileHandler] 세이브 파일 삭제 실패: {saveFileName}, {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// 다음 사용 가능한 슬롯 번호 반환
        /// </summary>
        public int GetNextAvailableSlotNumber()
        {
            if (_saveFileList.Count == 0)
                return 1;
            
            // 기존 슬롯 번호 중 최대값 + 1
            int maxSlot = _saveFileList.Max(s => ExtractSlotNumber(s.FileName));
            return maxSlot + 1;
        }
        
        /// <summary>
        /// 슬롯 번호로 세이브 파일명 생성 (Save_001 형식)
        /// </summary>
        public string GetSaveFileNameFromSlot(int slotNumber)
        {
            return $"{SaveFilePrefix}{slotNumber:D3}";
        }
        
        /// <summary>
        /// 세이브 파일명에서 슬롯 번호 추출
        /// </summary>
        private int ExtractSlotNumber(string saveFileName)
        {
            var match = SlotNumberRegex.Match(saveFileName);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int slotNumber))
            {
                return slotNumber;
            }
            return 0; // 파싱 실패 시 0 반환
        }


        /// <summary>
        /// 지정된 SceneEntry로 초기화된 새 세이브 파일 생성
        /// </summary>
        /// <param name="defaultSceneEntry">초기 lastSceneEntry로 설정할 씬 엔트리</param>
        /// <returns>생성된 세이브 파일명</returns>
        public string CreateEmptySaveFile(SceneEntry defaultSceneEntry)
        {
            // 다음 사용 가능한 슬롯 번호 확보
            int slotNumber = GetNextAvailableSlotNumber();
            string saveFileName = GetSaveFileNameFromSlot(slotNumber);
            
            // 슬롯 디렉토리 확보
            EnsureSlotDirectory(saveFileName);
            
            // 새 SaveFileData 생성 (lastSceneEntry = defaultSceneEntry)
            var data = new SaveFileData
            {
                lastSceneEntry = defaultSceneEntry,
                sceneData = new Dictionary<string, List<SavableEntry>>(),
                globalData = new List<SavableEntry>()
            };
            
            // 파일 저장
            SaveFile(saveFileName, data);
            
            this.Log($"새 세이브 파일 생성 완료: {saveFileName}, defaultScene: {defaultSceneEntry?.key ?? "null"}", Logg.LoggingMode.Completed);
            
            return saveFileName;
        }

        
        #endregion
        
    }
}

