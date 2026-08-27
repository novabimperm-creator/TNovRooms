using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>Итоговые значения по офису во внутренних единицах Revit (фут²).</summary>
    public class OfficeTotals
    {
        public double AreaTotal { get; set; }
        public double AreaUseful { get; set; }
        public double AreaCalculated { get; set; }
    }

    /// <summary>
    /// Расчёт и запись параметров офисографии. Формулы повторяют команду Offices,
    /// которая переведена на этот же сервис — расхождение результатов невозможно.
    /// </summary>
    public class OfficeCalculationService
    {
        private readonly string[] _excludedFromUseful;
        private readonly string[] _excludedFromCalculated;

        /// <param name="names1">Исключить из полезной и расчетной площади.</param>
        /// <param name="names2">Исключить из расчетной площади.</param>
        public OfficeCalculationService(string names1, string names2)
        {
            _excludedFromUseful = ManagerUtils.SplitNames(names1);
            _excludedFromCalculated = ManagerUtils.SplitNames(names2);
        }

        public static List<RoomGroup> Group(IEnumerable<Room> officeRooms)
        {
            return RoomGrouping.ByNumber(officeRooms, OfficeParams.GetNumber);
        }

        public bool IsExcludedFromUseful(Room room)
        {
            return ManagerUtils.NameMatchesAny(room, _excludedFromUseful);
        }

        public bool IsExcludedFromCalculated(Room room)
        {
            return ManagerUtils.NameMatchesAny(room, _excludedFromCalculated);
        }

        public OfficeTotals Calculate(IList<Room> rooms)
        {
            var totals = new OfficeTotals();

            foreach (Room room in rooms)
            {
                double sq = ManagerUtils.SqMetersToInternal(RoomParams.GetRoundedArea(room));
                totals.AreaTotal += sq;

                // первый список исключает помещение и из полезной, и из расчетной площади
                if (IsExcludedFromUseful(room)) continue;
                totals.AreaUseful += sq;

                if (IsExcludedFromCalculated(room)) continue;
                totals.AreaCalculated += sq;
            }

            return totals;
        }

        /// <summary>
        /// Запись значений во все помещения офиса. Требует открытой транзакции.
        /// Возвращает количество помещений, записанных без ошибок.
        /// </summary>
        public int Apply(IList<Room> rooms, OfficeTotals totals)
        {
            int written = 0;

            foreach (Room room in rooms)
            {
                bool ok = true;
                ok &= SetArea(room, OfficeParams.AreaTotal, totals.AreaTotal, "N_Офис.Площадь.Общая");
                ok &= SetArea(room, OfficeParams.AreaUseful, totals.AreaUseful, "N_Офис.Площадь.Полезная");
                ok &= SetArea(room, OfficeParams.AreaCalculated, totals.AreaCalculated, "N_Офис.Площадь.Расчетная");
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
