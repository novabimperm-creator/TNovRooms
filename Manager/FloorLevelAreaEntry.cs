using System.Collections.Generic;

namespace TNovRooms.Manager
{
    /// <summary>Привязка уровня к зоне общей площади — сериализуется в JSON проекта.</summary>
    public class FloorLevelAreaEntry
    {
        public long LevelId { get; set; }
        public string LevelName { get; set; }
        /// <summary>Id выбранной зоны (Area). 0 — зона не выбрана.</summary>
        public long ZoneId { get; set; }
        /// <summary>Площадь зоны в м² на момент записи (справочно).</summary>
        public double FloorArea { get; set; }
    }

    public class FloorLevelAreaData
    {
        public List<FloorLevelAreaEntry> Entries { get; set; } = new List<FloorLevelAreaEntry>();
    }
}
