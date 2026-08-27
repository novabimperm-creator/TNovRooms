using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>Помещения, объединённые одним номером: квартира или офис.</summary>
    public class RoomGroup
    {
        public string Number { get; }
        public List<Room> Rooms { get; }

        public RoomGroup(string number)
        {
            Number = number;
            Rooms = new List<Room>();
        }
    }

    public static class RoomGrouping
    {
        /// <summary>
        /// Группировка по номеру. Группы идут по возрастанию номера, а порядок помещений
        /// внутри группы сохраняется: от него зависит, какому помещению достанутся
        /// признаки вроде «Поквартир.Сетка».
        /// </summary>
        public static List<RoomGroup> ByNumber(IEnumerable<Room> rooms, Func<Room, string> numberSelector)
        {
            var map = new Dictionary<string, RoomGroup>();
            var order = new List<RoomGroup>();

            foreach (Room room in rooms)
            {
                string number = numberSelector(room);
                RoomGroup group;
                if (!map.TryGetValue(number, out group))
                {
                    group = new RoomGroup(number);
                    map[number] = group;
                    order.Add(group);
                }
                group.Rooms.Add(room);
            }

            var comparer = new AlphanumComparatorFastString();
            order.Sort((a, b) => comparer.Compare(a.Number, b.Number));
            return order;
        }
    }
}
