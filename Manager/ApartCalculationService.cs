using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>Итоговые значения по квартире во внутренних единицах Revit (фут²).</summary>
    public class ApartTotals
    {
        public double AreaTotal { get; set; }
        public double AreaTotalK { get; set; }
        public double Area { get; set; }
        public double AreaBalcony { get; set; }
        public double AreaBalconyK { get; set; }
        public double AreaLiving { get; set; }
        public int LivingRoomsCount { get; set; }
    }

    /// <summary>
    /// Расчёт и запись параметров квартирографии. Формулы повторяют команду Aparts,
    /// которая переведена на этот же сервис — расхождение результатов невозможно.
    /// </summary>
    public class ApartCalculationService
    {
        private readonly RoomRoundingService _rounding;
        private readonly Guid _livingGuid;

        public ApartCalculationService(RoomRoundingService rounding, Guid livingGuid)
        {
            _rounding = rounding;
            _livingGuid = livingGuid;
        }

        /// <summary>Группировка помещений по сквозному номеру квартиры.</summary>
        public static List<RoomGroup> Group(IEnumerable<Room> apartmentRooms)
        {
            return RoomGrouping.ByNumber(apartmentRooms, ApartParams.GetNumber);
        }

        /// <summary>
        /// Помещение попадает в балконы, если его имя есть в списках коэффициентов 0.5 / 0.3.
        /// Это то же условие, по которому Округлятор назначает коэффициент.
        /// </summary>
        public bool IsBalcony(Room room)
        {
            return _rounding.GetCoefficient(room) != 1;
        }

        public bool IsLiving(Room room)
        {
            return ApartParams.IsLivingRoom(room, _livingGuid);
        }

        public ApartTotals Calculate(IList<Room> rooms)
        {
            var totals = new ApartTotals();

            foreach (Room room in rooms)
            {
                double sq = ManagerUtils.SqMetersToInternal(RoomParams.GetRoundedArea(room));
                double sqk = ManagerUtils.SqMetersToInternal(RoomParams.GetRoundedAreaK(room));

                totals.AreaTotal += sq;
                totals.AreaTotalK += sqk;

                if (IsBalcony(room))
                {
                    totals.AreaBalcony += sq;
                    totals.AreaBalconyK += sqk;
                }
                else
                {
                    totals.Area += sq;
                }

                if (IsLiving(room))
                {
                    totals.AreaLiving += sq;
                    totals.LivingRoomsCount++;
                }
            }

            return totals;
        }

        /// <summary>
        /// Запись значений во все помещения квартиры. Требует открытой транзакции.
        /// Возвращает количество помещений, записанных без ошибок.
        /// </summary>
        public int Apply(IList<Room> rooms, ApartTotals totals)
        {
            int written = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                bool ok = true;

                ok &= SetArea(room, ApartParams.AreaTotal, totals.AreaTotal, "N_Кв.Площадь.Общая");
                ok &= SetArea(room, ApartParams.AreaTotalK, totals.AreaTotalK, "N_Кв.Площадь.ОбщаяСКоэффициентом");
                ok &= SetArea(room, ApartParams.Area, totals.Area, "N_Кв.Площадь");
                ok &= SetArea(room, ApartParams.AreaBalcony, totals.AreaBalcony, "N_Кв.Площадь.Балконы");
                ok &= SetArea(room, ApartParams.AreaBalconyK, totals.AreaBalconyK, "N_Кв.Площадь.БалконыСКоэффициентом");
                ok &= SetArea(room, ApartParams.AreaLiving, totals.AreaLiving, "N_Кв.Площадь.Жилая");

                try
                {
                    room.get_Parameter(ApartParams.RoomsCount)?.Set(totals.LivingRoomsCount.ToString());
                }
                catch (Exception ex)
                {
                    ok = false;
                    Logger.Log("   Помещение " + room.Id + " параметр N_Кв.Комнаты.Количество ошибка: " + ex.Message, 4);
                }

                // Признак поквартирной сетки достаётся только первому помещению квартиры
                try
                {
                    room.LookupParameter(ApartParams.SpecGridTitle)?.Set(i == 0 ? 1 : 0);
                }
                catch (Exception ex)
                {
                    ok = false;
                    Logger.Log("   Помещение " + room.Id + " параметр " + ApartParams.SpecGridTitle + " ошибка: " + ex.Message, 4);
                }

                if (ok) written++;
            }

            return written;
        }

        private static bool SetArea(Room room, Guid guid, double value, string title)
        {
            try
            {
                room.get_Parameter(guid)?.Set(value);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log("   Помещение " + room.Id + " параметр " + title + " ошибка: " + ex.Message, 4);
                return false;
            }
        }
    }
}
