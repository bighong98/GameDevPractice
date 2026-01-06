using System.Collections.Generic;

namespace TH.SaveLoad
{
    public interface ISaveFileHandler
    {
        #region Core Save/Load
        
        /// <summary>
        /// 세이브 파일 로드
        /// </summary>
        SaveFileData LoadFile(string saveFile);
        
        /// <summary>
        /// 세이브 파일 저장
        /// </summary>
        void SaveFile(string saveFile, SaveFileData data);
        
        #endregion
        
        #region Save File Management
        
        /// <summary>
        /// 현재 사용 중인 세이브 파일명 반환
        /// </summary>
        string GetSaveFileName();
        
        /// <summary>
        /// 세이브 파일명으로부터 전체 경로 반환
        /// </summary>
        string GetPathFromSaveFile(string saveFile);
        
        /// <summary>
        /// 존재하는 모든 세이브 파일 목록 (읽기 전용)
        /// </summary>
        IReadOnlyList<SaveFileInfo> SaveFiles { get; }
        
        /// <summary>
        /// 세이브 파일 목록 갱신
        /// </summary>
        void RefreshSaveFileList();
        
        /// <summary>
        /// 가장 최근에 수정된 세이브 파일 반환
        /// </summary>
        /// <returns>최근 세이브 파일 정보, 없으면 null</returns>
        SaveFileInfo? GetMostRecentSaveFile();
        
        /// <summary>
        /// 특정 세이브 파일 존재 여부 확인
        /// </summary>
        bool SaveFileExists(string saveFileName);
        
        /// <summary>
        /// 세이브 파일 삭제
        /// </summary>
        /// <returns>삭제 성공 여부</returns>
        bool DeleteSaveFile(string saveFileName);
        
        /// <summary>
        /// 다음 사용 가능한 세이브 슬롯 번호 반환
        /// </summary>
        int GetNextAvailableSlotNumber();
        
        /// <summary>
        /// 슬롯 번호로 세이브 파일명 생성
        /// </summary>
        string GetSaveFileNameFromSlot(int slotNumber);
        
        #endregion
    }
}

