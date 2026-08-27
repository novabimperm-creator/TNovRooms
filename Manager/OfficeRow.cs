using Autodesk.Revit.DB.Architecture;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace TNovRooms.Manager
{
    /// <summary>Строка списка офисографии: один офис с агрегированными параметрами.</summary>
    public class OfficeRow : INotifyPropertyChanged
    {
        /// <summary>
        /// Площади складываются из значений, округлённых до 0.1 м², поэтому расхождение
        /// больше этого допуска означает, что офисографию действительно надо пересчитать.
        /// </summary>
        public const double AreaTolerance = 0.005;

        private readonly List<string> _errors = new List<string>();

        public RoomGroup Group { get; }
        public List<Room> Rooms { get; }

        public string Number { get; }
        public string NumberDisplay { get; }
        public int RoomsTotal { get; }

        public string RoomNames { get; private set; }
        public string LevelText { get; private set; }

        public double AreaTotal { get; private set; }
        public double AreaUseful { get; private set; }
        public double AreaCalculated { get; private set; }

        public string AreaTotalText { get { return ManagerUtils.AreaToText(AreaTotal); } }
        public string AreaUsefulText { get { return ManagerUtils.AreaToText(AreaUseful); } }
        public string AreaCalculatedText { get { return ManagerUtils.AreaToText(AreaCalculated); } }

        public bool HasError { get; private set; }
        public string ErrorText { get; private set; }

        public OfficeRow(RoomGroup group)
        {
            Group = group;
            Rooms = group.Rooms;
            Number = group.Number;
            NumberDisplay = Number.Length == 0 ? "(без номера)" : Number;
            RoomsTotal = Rooms.Count;
        }

        public void Refresh(OfficeCalculationService service)
        {
            _errors.Clear();

            var levels = new List<double>();
            int withoutRounding = 0;
            bool differentTotals = false;
            double firstTotal = OfficeParams.GetAreaM2(Rooms[0], OfficeParams.AreaTotal);

            foreach (Room room in Rooms)
            {
                double level = RoomParams.GetLevelDisplayNumber(room);
                if (!levels.Contains(level)) levels.Add(level);

                if (room.get_Parameter(RoomParams.RoundedArea) == null
                    || room.get_Parameter(RoomParams.RoundedAreaK) == null) withoutRounding++;

                // площади офиса записываются во все его помещения — значения должны совпадать
                if (System.Math.Abs(OfficeParams.GetAreaM2(room, OfficeParams.AreaTotal) - firstTotal) >= AreaTolerance)
                    differentTotals = true;
            }

            levels.Sort();
            LevelText = string.Join(", ", levels.Select(v => v.ToString("0.###", CultureInfo.CurrentCulture)).ToArray());
            RoomNames = string.Join(", ", Rooms.Select(RoomParams.GetRoomName).Distinct().ToArray());

            Room first = Rooms[0];
            AreaTotal = OfficeParams.GetAreaM2(first, OfficeParams.AreaTotal);
            AreaUseful = OfficeParams.GetAreaM2(first, OfficeParams.AreaUseful);
            AreaCalculated = OfficeParams.GetAreaM2(first, OfficeParams.AreaCalculated);

            if (Number.Length == 0) _errors.Add("Не заполнен " + OfficeParams.NumberTitle);
            if (!OfficeParams.HasOfficeParams(first)) _errors.Add("Нет общих параметров офисографии");
            if (withoutRounding > 0) _errors.Add("Нет параметров округления площади: " + withoutRounding + " пом.");
            if (differentTotals) _errors.Add("Площади различаются у помещений одного офиса");
            if (levels.Count > 1) _errors.Add("Помещения на разных этажах: " + LevelText);

            OfficeTotals computed = service.Calculate(Rooms);
            if (ManagerUtils.InternalToSqMeters(computed.AreaUseful) < AreaTolerance)
                _errors.Add("Полезная площадь нулевая: все помещения попали в исключения");
            AddStaleErrors(computed);
        }

        private void AddStaleErrors(OfficeTotals computed)
        {
            var stale = new List<string>();
            AddStale(stale, "Общая", AreaTotal, computed.AreaTotal);
            AddStale(stale, "Полезная", AreaUseful, computed.AreaUseful);
            AddStale(stale, "Расчетная", AreaCalculated, computed.AreaCalculated);

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

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
