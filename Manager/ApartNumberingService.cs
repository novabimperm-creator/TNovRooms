using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>Что и какими значениями будет заполнено при сквозной нумерации.</summary>
    public class ApartNumberingPlan
    {
        public List<Room> Rooms { get; } = new List<Room>();
        public List<int> Values { get; } = new List<int>();
        public int ApartCount { get; set; }
        public int FirstNumber { get; set; }
        public int LastNumber { get; set; }

        /// <summary>Квартиры, у которых сквозной номер изменится.</summary>
        public int ChangedCount { get; set; }
    }

    /// <summary>
    /// Сквозная нумерация квартир. Порядок обхода повторяет команду ApartsNum,
    /// которая переведена на этот же сервис.
    /// </summary>
    public static class ApartNumberingService
    {
        /// <summary>
        /// Помещения сортируются по этажу и номеру квартиры на этаже, затем группируются
        /// по паре (этаж, номер на этаже): каждая такая пара — одна квартира.
        /// </summary>
        public static ApartNumberingPlan Build(IEnumerable<Room> apartmentRooms, int firstNumber)
        {
            var sorted = apartmentRooms
                .Select(room => new
                {
                    Room = room,
                    Level = RoomParams.GetLevelNumber(room),
                    NumAtLevel = ApartParams.GetNumberAtLevel(room)
                })
                .OrderBy(x => x.Level * 1000 + x.NumAtLevel)
                .ToList();

            var groups = new Dictionary<string, List<Room>>();
            var order = new List<string>();

            foreach (var item in sorted)
            {
                string key = item.Level.ToString("R", CultureInfo.InvariantCulture) + "|" + item.NumAtLevel;
                List<Room> group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new List<Room>();
                    groups[key] = group;
                    order.Add(key);
                }
                group.Add(item.Room);
            }

            var plan = new ApartNumberingPlan { FirstNumber = firstNumber };
            int current = firstNumber;

            foreach (string key in order)
            {
                bool changed = false;
                foreach (Room room in groups[key])
                {
                    plan.Rooms.Add(room);
                    plan.Values.Add(current);
                    if (ApartParams.GetNumber(room) != current.ToString()) changed = true;
                }
                if (changed) plan.ChangedCount++;
                current++;
            }

            plan.ApartCount = order.Count;
            plan.LastNumber = order.Count == 0 ? firstNumber : current - 1;
            return plan;
        }

        /// <summary>
        /// Запись сквозных номеров. Требует открытой транзакции.
        /// Возвращает количество помещений, записанных без ошибок.
        /// </summary>
        public static int Apply(ApartNumberingPlan plan)
        {
            int written = 0;

            for (int i = 0; i < plan.Rooms.Count; i++)
            {
                Room room = plan.Rooms[i];
                try
                {
                    room.get_Parameter(RoomParams.ApartmentNumber)?.Set(plan.Values[i].ToString());
                    written++;
                    Logger.Log("   Помещение " + room.Id + " успешно", 2);
                }
                catch (Exception ex)
                {
                    Logger.Log("   Помещение " + room.Id + " ошибка: " + ex.Message, 4);
                }
            }

            return written;
        }
    }
}
