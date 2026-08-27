using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace TNovRooms.Manager
{
    /// <summary>Строка списка кладовых. Порядок строк задаёт будущую нумерацию.</summary>
    public class StorageRow : INotifyPropertyChanged
    {
        private readonly List<string> _errors = new List<string>();

        public StorageRow(Room room)
        {
            Room = room;
            RoomId = ManagerUtils.IdValue(room.Id);
        }

        public Room Room { get; }
        public long RoomId { get; }

        public string IdText { get { return RoomId.ToString(CultureInfo.InvariantCulture); } }
        public string Number { get; private set; }
        public double Level { get; private set; }
        public string LevelText { get; private set; }
        public double AreaM2 { get; private set; }
        public double RoundedArea { get; private set; }
        public double RoundedAreaK { get; private set; }
        public bool HasParams { get; private set; }

        public string AreaText { get { return ManagerUtils.AreaToText(AreaM2); } }
        public string RoundedAreaText { get { return HasParams ? ManagerUtils.AreaToText(RoundedArea) : "—"; } }
        public string RoundedAreaKText { get { return HasParams ? ManagerUtils.AreaToText(RoundedAreaK) : "—"; } }

        /// <summary>Номер, который получит кладовая при нажатии «Перенумеровать».</summary>
        public string NewNumberText { get; private set; }

        public bool HasError { get; private set; }
        public string ErrorText { get; private set; }

        /// <summary>
        /// Коэффициент у кладовых всегда 1, но берём его из общей службы округления:
        /// если имя всё же попадёт в списки коэффициентов, проверка не разойдётся с Округлятором.
        /// </summary>
        public void Refresh(RoomRoundingService rounding)
        {
            _errors.Clear();

            Number = RoomParams.GetRoomNumber(Room);
            Level = RoomParams.GetLevelDisplayNumber(Room);
            LevelText = Level.ToString("0.###", CultureInfo.CurrentCulture);
            AreaM2 = RoomParams.GetAreaM2(Room);

            HasParams = Room.get_Parameter(RoomParams.RoundedArea) != null
                        && Room.get_Parameter(RoomParams.RoundedAreaK) != null;
            RoundedArea = HasParams ? RoomParams.GetRoundedArea(Room) : 0;
            RoundedAreaK = HasParams ? RoomParams.GetRoundedAreaK(Room) : 0;

            double expectedK = RoomRoundingService.RoundAreaWithCoefficient(
                RoomRoundingService.RoundArea(AreaM2), rounding.GetCoefficient(Room));

            if (Number.Length == 0) _errors.Add("Не заполнен номер помещения");

            if (!HasParams)
            {
                _errors.Add("Нет общих параметров площадей");
            }
            else
            {
                double delta = Math.Abs(AreaM2 - RoundedArea);
                if (delta >= RoomRow.AreaErrorThreshold)
                    _errors.Add("Расхождение с «Площадь»: " + ManagerUtils.AreaToText(delta) + " м²");

                if (Math.Abs(RoundedAreaK - expectedK) >= RoomRow.RoundedCompareTolerance)
                    _errors.Add("«ОкруглСКоэффициентом» ≠ " + ManagerUtils.AreaToText(expectedK));
            }
        }

        public void SetPreview(int number)
        {
            NewNumberText = number.ToString(CultureInfo.InvariantCulture);
            OnPropertyChanged(nameof(NewNumberText));
        }

        public void AddError(string text)
        {
            _errors.Add(text);
        }

        public void ApplyErrors()
        {
            HasError = _errors.Count > 0;
            ErrorText = HasError ? string.Join("; ", _errors.ToArray()) : "ОК";
            OnPropertyChanged(null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
