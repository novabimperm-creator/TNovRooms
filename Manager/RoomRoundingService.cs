using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;

namespace TNovRooms.Manager
{
    /// <summary>
    /// Расчёт округлённых площадей. Формулы и порядок применения коэффициентов
    /// повторяют RoomsRound, чтобы результаты нового и старого Округлятора совпадали.
    /// </summary>
    public class RoomRoundingService
    {
        private readonly string[] _namesK05;
        private readonly string[] _namesK03;

        public RoomRoundingService(string k05, string k03)
        {
            _namesK05 = ManagerUtils.SplitNames(k05);
            _namesK03 = ManagerUtils.SplitNames(k03);
        }

        /// <summary>
        /// Сравнение идёт с Room.Name ("Имя Номер"), как в исходном Округлятоpе.
        /// Список 0.3 проверяется вторым и перебивает 0.5.
        /// </summary>
        public double GetCoefficient(Room room)
        {
            double k = 1;
            if (ManagerUtils.NameMatchesAny(room, _namesK05)) k = 0.5;
            if (ManagerUtils.NameMatchesAny(room, _namesK03)) k = 0.3;
            return k;
        }

        public static double RoundArea(double areaM2)
        {
            return Math.Round(areaM2, 1);
        }

        public static double RoundAreaWithCoefficient(double roundedArea, double coefficient)
        {
            return Math.Round(roundedArea * coefficient + 0.000001, 1);
        }
    }
}
