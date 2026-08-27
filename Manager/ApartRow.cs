using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace TNovRooms.Manager
{
    /// <summary>Строка списка квартирографии: одна квартира с агрегированными параметрами.</summary>
    public class ApartRow : INotifyPropertyChanged
    {
        /// <summary>
        /// Площади складываются из значений, округлённых до 0.1 м², поэтому расхождение
        /// больше этого допуска означает, что квартирографию действительно надо пересчитать.
        /// </summary>
        public const double AreaTolerance = 0.005;

        private readonly List<string> _errors = new List<string>();

        public RoomGroup Group { get; }
        public List<Room> Rooms { get; }

        public string Number { get; }
        public string NumberDisplay { get; }
        public string NumberAtLevelText { get; private set; }
        public string LevelText { get; private set; }
        public int RoomsTotal { get; }

        public double LevelSortKey { get; private set; }
        public int NumberAtLevelSortKey { get; private set; }

        /// <summary>Номера этажей помещений квартиры для показа и фильтра. Обычно один.</summary>
        public List<double> Levels { get; private set; } = new List<double>();

        /// <summary>Ключ квартиры для поиска дублей: этаж + номер на этаже.</summary>
        public List<string> PlacementKeys { get; private set; }

        public int LivingRoomsCount { get; private set; }
        public double AreaTotal { get; private set; }
        public double AreaTotalK { get; private set; }
        public double Area { get; private set; }
        public double AreaBalcony { get; private set; }
        public double AreaBalconyK { get; private set; }
        public double AreaLiving { get; private set; }

        public string LivingRoomsCountText { get { return LivingRoomsCount.ToString(); } }
        public string AreaTotalText { get { return ManagerUtils.AreaToText(AreaTotal); } }
        public string AreaTotalKText { get { return ManagerUtils.AreaToText(AreaTotalK); } }
        public string AreaText { get { return ManagerUtils.AreaToText(Area); } }
        public string AreaBalconyText { get { return ManagerUtils.AreaToText(AreaBalcony); } }
        public string AreaBalconyKText { get { return ManagerUtils.AreaToText(AreaBalconyK); } }
        public string AreaLivingText { get { return ManagerUtils.AreaToText(AreaLiving); } }

        public bool HasError { get; private set; }
        public string ErrorText { get; private set; }

        public ApartRow(RoomGroup group)
        {
            Group = group;
            Rooms = group.Rooms;
            Number = group.Number;
            NumberDisplay = Number.Length == 0 ? "(без номера)" : Number;
            RoomsTotal = Rooms.Count;
        }

        /// <summary>
        /// Читает значения из модели и сравнивает их с пересчитанными.
        /// Межквартирные проверки добавляются извне через AddError.
        /// </summary>
        public void Refresh(ApartCalculationService service)
        {
            _errors.Clear();

            var levels = new List<double>();
            var numbersAtLevel = new List<int>();
            var placements = new List<string>();
            int withoutRounding = 0;
            int specGridCount = 0;
            bool differentTotals = false;
            double firstTotal = ApartParams.GetAreaM2(Rooms[0], ApartParams.AreaTotal);

            foreach (Room room in Rooms)
            {
                double level = RoomParams.GetLevelDisplayNumber(room);
                int numAtLevel = ApartParams.GetNumberAtLevel(room);
                if (!levels.Contains(level)) levels.Add(level);
                if (!numbersAtLevel.Contains(numAtLevel)) numbersAtLevel.Add(numAtLevel);

                // ключ размещения строится на сыром значении - как в сервисе сквозной нумерации
                string placement = RoomParams.GetLevelNumber(room).ToString("R", CultureInfo.InvariantCulture)
                                   + "|" + numAtLevel;
                if (!placements.Contains(placement)) placements.Add(placement);

                if (room.get_Parameter(RoomParams.RoundedArea) == null
                    || room.get_Parameter(RoomParams.RoundedAreaK) == null) withoutRounding++;

                Parameter specGrid = room.LookupParameter(ApartParams.SpecGridTitle);
                if (specGrid != null && specGrid.AsInteger() == 1) specGridCount++;

                // площади квартиры записываются во все её помещения — значения должны совпадать
                if (System.Math.Abs(ApartParams.GetAreaM2(room, ApartParams.AreaTotal) - firstTotal) >= AreaTolerance)
                    differentTotals = true;
            }

            levels.Sort();
            numbersAtLevel.Sort();
            Levels = levels;
            PlacementKeys = placements;
            LevelSortKey = levels.Count == 0 ? 0 : levels[0];
            NumberAtLevelSortKey = numbersAtLevel.Count == 0 ? 0 : numbersAtLevel[0];

            LevelText = string.Join(", ", levels.Select(LevelToText).ToArray());
            NumberAtLevelText = string.Join(", ", numbersAtLevel.Select(n => n.ToString()).ToArray());

            Room first = Rooms[0];
            LivingRoomsCount = ApartParams.GetRoomsCount(first);
            AreaTotal = ApartParams.GetAreaM2(first, ApartParams.AreaTotal);
            AreaTotalK = ApartParams.GetAreaM2(first, ApartParams.AreaTotalK);
            Area = ApartParams.GetAreaM2(first, ApartParams.Area);
            AreaBalcony = ApartParams.GetAreaM2(first, ApartParams.AreaBalcony);
            AreaBalconyK = ApartParams.GetAreaM2(first, ApartParams.AreaBalconyK);
            AreaLiving = ApartParams.GetAreaM2(first, ApartParams.AreaLiving);

            if (Number.Length == 0) _errors.Add("Не заполнен " + ApartParams.NumberTitle);
            if (!ApartParams.HasApartParams(first)) _errors.Add("Нет общих параметров квартирографии");
            if (!ApartParams.HasSpecGrid(first)) _errors.Add("Нет параметра " + ApartParams.SpecGridTitle);
            if (withoutRounding > 0) _errors.Add("Нет параметров округления площади: " + withoutRounding + " пом.");
            if (differentTotals) _errors.Add("Площади различаются у помещений одной квартиры");
            if (specGridCount != 1)
                _errors.Add("Признак " + ApartParams.SpecGridTitle + " включён у " + specGridCount + " пом. вместо одного");
            if (numbersAtLevel.Contains(0)) _errors.Add("Не заполнен " + ApartParams.NumberAtLevelTitle);
            if (numbersAtLevel.Count > 1)
                _errors.Add("Один сквозной номер у разных квартир (номера на этаже: " + NumberAtLevelText + ")");
            if (levels.Count > 1) _errors.Add("Помещения на разных этажах: " + LevelText);

            ApartTotals computed = service.Calculate(Rooms);
            if (computed.LivingRoomsCount == 0) _errors.Add("Нет жилых комнат");
            AddStaleErrors(computed);
        }

        private void AddStaleErrors(ApartTotals computed)
        {
            var stale = new List<string>();
            AddStale(stale, "Общая", AreaTotal, computed.AreaTotal);
            AddStale(stale, "Общая с к", AreaTotalK, computed.AreaTotalK);
            AddStale(stale, "Площадь", Area, computed.Area);
            AddStale(stale, "Балконы", AreaBalcony, computed.AreaBalcony);
            AddStale(stale, "Балконы с к", AreaBalconyK, computed.AreaBalconyK);
            AddStale(stale, "Жилая", AreaLiving, computed.AreaLiving);

            if (LivingRoomsCount != computed.LivingRoomsCount)
                stale.Add("Комнат " + LivingRoomsCount + " → " + computed.LivingRoomsCount);

            if (stale.Count > 0) _errors.Add("Требуется пересчёт: " + string.Join(", ", stale.ToArray()));
        }

        private static void AddStale(List<string> target, string title, double modelValue, double computedInternal)
        {
            double computed = ManagerUtils.InternalToSqMeters(computedInternal);
            if (System.Math.Abs(computed - modelValue) < AreaTolerance) return;
            target.Add(title + " " + ManagerUtils.AreaToText(modelValue) + " → " + ManagerUtils.AreaToText(computed));
        }

        public void AddError(string text)
        {
            _errors.Add(text);
        }

        public void ApplyErrors()
        {
            HasError = _errors.Count > 0;
            ErrorText = HasError ? string.Join("; ", _errors.ToArray()) : "OK";
            OnPropertyChanged(null);
        }

        private static string LevelToText(double level)
        {
            return level.ToString("0.###", CultureInfo.CurrentCulture);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
