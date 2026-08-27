namespace TNovRooms.Manager
{
    /// <summary>Элемент выпадающего списка зон (Revit Area) для привязки к уровню.</summary>
    public class FloorZoneOption
    {
        public static FloorZoneOption None { get; } = new FloorZoneOption
        {
            ZoneId = 0,
            DisplayName = "(не выбрано)",
            AreaM2 = 0,
            LevelName = ""
        };

        public long ZoneId { get; set; }
        public string DisplayName { get; set; }
        public double AreaM2 { get; set; }
        public string LevelName { get; set; }

        public bool IsNone => ZoneId == 0;
    }
}
