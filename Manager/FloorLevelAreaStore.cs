using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>Загрузка и сохранение привязок «уровень → зона» в JSON проекта.</summary>
    public static class FloorLevelAreaStore
    {
        private const string JsonKey = "N_Площадь этажа";

        public static FloorLevelAreaData Load()
        {
            new json(JsonKey, true, out bool exist, out string jsonPath);
            if (!exist) return new FloorLevelAreaData();

            try
            {
                var loaded = JsonConvert.DeserializeObject<FloorLevelAreaData>(File.ReadAllText(jsonPath));
                if (loaded?.Entries != null)
                {
                    Logger.Log("Загружены привязки площадей этажей: " + loaded.Entries.Count + " записей", 1);
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка при чтении площадей этажей: " + ex.Message, 4);
            }

            return new FloorLevelAreaData();
        }

        public static void Save(FloorLevelAreaData data)
        {
            new json(JsonKey, true, out bool _, out string jsonPath);
            try
            {
                File.WriteAllText(jsonPath, JsonConvert.SerializeObject(data));
                Logger.Log("Привязки площадей этажей сохранены: " + data.Entries.Count + " записей", 1);
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка при сохранении площадей этажей: " + ex.Message, 4);
                new InfoWindow280("Не удалось сохранить привязки площадей этажей:\n" + ex.Message).ShowDialog();
            }
        }

        public static FloorLevelAreaEntry FindEntry(FloorLevelAreaData data, long levelId)
        {
            return data.Entries.FirstOrDefault(e => e.LevelId == levelId);
        }

        public static void Upsert(FloorLevelAreaData data, long levelId, string levelName, long zoneId, double floorArea)
        {
            FloorLevelAreaEntry entry = FindEntry(data, levelId);
            if (entry == null)
            {
                entry = new FloorLevelAreaEntry { LevelId = levelId, LevelName = levelName };
                data.Entries.Add(entry);
            }

            entry.LevelName = levelName;
            entry.ZoneId = zoneId;
            entry.FloorArea = floorArea;
        }

        public static void Remove(FloorLevelAreaData data, long levelId)
        {
            data.Entries.RemoveAll(e => e.LevelId == levelId);
        }
    }
}
