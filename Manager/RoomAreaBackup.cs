using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace TNovRooms.Manager
{
    public class RoomAreaBackupEntry
    {
        public string Category { get; set; }
        public long RoomId { get; set; }
        public double Area { get; set; }
        public double AreaK { get; set; }
    }

    /// <summary>
    /// Один файл бэкапа площадей: {дата},{модель},{имя}.txt со строками "категория|id|S|Sк".
    /// </summary>
    public class RoomAreaBackup
    {
        private static readonly string[] DateFormats =
        {
            "dd.MM.yyyy HH-mm-ss",
            "dd.MM.yyyy H-mm-ss",
            "yyyy-MM-dd HH-mm-ss",
            "MM/dd/yyyy HH-mm-ss",
            "M/d/yyyy h-mm-ss tt"
        };

        private Dictionary<long, RoomAreaBackupEntry> _entries;

        public string FilePath { get; }
        public string FileName { get; }
        public string DisplayName { get; }
        public DateTime Timestamp { get; }

        public RoomAreaBackup(string filePath, string docName)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            DisplayName = FileName.Replace("," + docName + ",", " ").Replace(".txt", "").Trim();
            Timestamp = ReadTimestamp(filePath, FileName);
        }

        public bool IsLoaded => _entries != null;

        public int EntryCount => _entries == null ? 0 : _entries.Count;

        private static DateTime ReadTimestamp(string filePath, string fileName)
        {
            string head = fileName.Split(',')[0];
            foreach (string format in DateFormats)
            {
                if (DateTime.TryParseExact(head, format, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime parsed))
                {
                    return parsed;
                }
            }
            try { return File.GetLastWriteTime(filePath); }
            catch (Exception) { return DateTime.MinValue; }
        }

        public void Load()
        {
            if (_entries != null) return;
            _entries = new Dictionary<long, RoomAreaBackupEntry>();

            string[] lines;
            try { lines = File.ReadAllLines(FilePath); }
            catch (Exception) { return; }

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 4) continue;
                if (!long.TryParse(parts[1].Trim(), out long roomId) || roomId == 0) continue;

                ManagerUtils.TryParseNumber(parts[2], out double area);
                ManagerUtils.TryParseNumber(parts[3], out double areaK);

                _entries[roomId] = new RoomAreaBackupEntry
                {
                    Category = parts[0],
                    RoomId = roomId,
                    Area = area,
                    AreaK = areaK
                };
            }
        }

        public bool TryGetEntry(long roomId, out RoomAreaBackupEntry entry)
        {
            Load();
            return _entries.TryGetValue(roomId, out entry);
        }
    }
}
