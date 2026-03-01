using System;

namespace TH.SaveLoad
{
    /// <summary>
    /// 세이브 파일 정보를 담는 읽기 전용 구조체
    /// </summary>
    public readonly struct SaveFileInfo : IEquatable<SaveFileInfo>, IComparable<SaveFileInfo>
    {
        /// <summary>
        /// 세이브 파일명 (확장자 제외, 예: "Save_001")
        /// </summary>
        public string FileName { get; }
        
        /// <summary>
        /// 세이브 일시 (년/월/일/시/분/초)
        /// </summary>
        public DateTime SaveDate { get; }
        
        /// <summary>
        /// 전체 파일 경로
        /// </summary>
        public string FilePath { get; }

        public SaveFileInfo(string fileName, DateTime saveDate, string filePath)
        {
            FileName = fileName;
            SaveDate = saveDate;
            FilePath = filePath;
        }

        /// <summary>
        /// SaveDate 기준 내림차순 정렬 (최신이 먼저)
        /// </summary>
        public int CompareTo(SaveFileInfo other)
        {
            return other.SaveDate.CompareTo(SaveDate);
        }

        public bool Equals(SaveFileInfo other)
        {
            return FileName == other.FileName && FilePath == other.FilePath;
        }

        public override bool Equals(object obj)
        {
            return obj is SaveFileInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(FileName, FilePath);
        }

        public override string ToString()
        {
            return $"[{FileName}] {SaveDate:yyyy/MM/dd HH:mm:ss}";
        }

        public static bool operator ==(SaveFileInfo left, SaveFileInfo right) => left.Equals(right);
        public static bool operator !=(SaveFileInfo left, SaveFileInfo right) => !left.Equals(right);
    }
}
