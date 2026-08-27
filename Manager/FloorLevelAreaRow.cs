using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace TNovRooms.Manager
{
    public class FloorLevelAreaRow : INotifyPropertyChanged
    {
        private FloorZoneOption _selectedZone = FloorZoneOption.None;
        private string _floorAreaText = "";
        private bool _roomsHaveMixedArea;
        private bool _hasError;
        private string _errorText = "—";

        public FloorLevelAreaRow(Level level, int roomCount)
        {
            Level = level;
            LevelId = ManagerUtils.IdValue(level.Id);
            LevelName = level.Name ?? "";
            RoomCount = roomCount;
        }

        public Level Level { get; }
        public long LevelId { get; }
        public string LevelName { get; }
        public int RoomCount { get; private set; }

        public string RoomCountText
        {
            get { return RoomCount.ToString(CultureInfo.InvariantCulture); }
        }

        public FloorZoneOption SelectedZone
        {
            get { return _selectedZone; }
            set
            {
                FloorZoneOption next = value ?? FloorZoneOption.None;
                if (_selectedZone != null && _selectedZone.ZoneId == next.ZoneId) return;
                _selectedZone = next;
                OnPropertyChanged(nameof(SelectedZone));
                OnPropertyChanged(nameof(ZoneAreaText));
                OnPropertyChanged(nameof(HasZone));
                UpdateStatus();
                ZoneSelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool HasZone
        {
            get { return _selectedZone != null && !_selectedZone.IsNone; }
        }

        public string ZoneAreaText
        {
            get
            {
                if (!HasZone) return "—";
                return ManagerUtils.AreaToText(_selectedZone.AreaM2);
            }
        }

        /// <summary>Значение N_Площадь этажа для записи (редактируется вручную или заполняется из зоны).</summary>
        public string FloorAreaText
        {
            get { return _floorAreaText; }
            set
            {
                string next = value ?? "";
                if (_floorAreaText == next) return;
                _floorAreaText = next;
                _roomsHaveMixedArea = false;
                OnPropertyChanged(nameof(FloorAreaText));
                UpdateStatus();
            }
        }

        public bool HasFloorArea
        {
            get { return TryGetFloorArea(out _); }
        }

        public bool HasError
        {
            get { return _hasError; }
            private set
            {
                if (_hasError == value) return;
                _hasError = value;
                OnPropertyChanged(nameof(HasError));
            }
        }

        public string ErrorText
        {
            get { return _errorText; }
            private set
            {
                if (_errorText == value) return;
                _errorText = value;
                OnPropertyChanged(nameof(ErrorText));
            }
        }

        public event EventHandler ZoneSelectionChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public bool TryGetFloorArea(out double value)
        {
            return ManagerUtils.TryParseNumber(_floorAreaText, out value);
        }

        public void SetRoomFloorArea(IList<Room> roomsOnLevel)
        {
            RoomCount = roomsOnLevel?.Count ?? 0;
            _roomsHaveMixedArea = false;
            _floorAreaText = "";

            if (roomsOnLevel == null || roomsOnLevel.Count == 0)
            {
                OnPropertyChanged(nameof(RoomCount));
                OnPropertyChanged(nameof(RoomCountText));
                OnPropertyChanged(nameof(FloorAreaText));
                UpdateStatus();
                return;
            }

            var values = new List<double>();
            foreach (Room room in roomsOnLevel)
            {
                Parameter param = room.get_Parameter(RoomParams.FloorArea);
                if (param == null || !param.HasValue) continue;
                double m2 = ManagerUtils.InternalToSqMeters(param.AsDouble());
                if (!values.Any(v => Math.Abs(v - m2) < RoomRow.RoundedCompareTolerance))
                    values.Add(m2);
            }

            if (values.Count == 1)
            {
                _floorAreaText = ManagerUtils.AreaToText(values[0]);
            }
            else if (values.Count > 1)
            {
                _floorAreaText = ManagerUtils.AreaToText(values[0]);
                _roomsHaveMixedArea = true;
            }

            OnPropertyChanged(nameof(RoomCount));
            OnPropertyChanged(nameof(RoomCountText));
            OnPropertyChanged(nameof(FloorAreaText));
            UpdateStatus();
        }

        public void ApplyZoneArea()
        {
            if (!HasZone) return;
            _floorAreaText = ManagerUtils.AreaToText(_selectedZone.AreaM2);
            _roomsHaveMixedArea = false;
            OnPropertyChanged(nameof(FloorAreaText));
            UpdateStatus();
        }

        public void ApplyWrittenArea(double areaM2)
        {
            _floorAreaText = ManagerUtils.AreaToText(areaM2);
            _roomsHaveMixedArea = false;
            OnPropertyChanged(nameof(FloorAreaText));
            UpdateStatus();
        }

        public void RefreshZoneArea(double areaM2)
        {
            if (!HasZone) return;
            _selectedZone.AreaM2 = areaM2;
            OnPropertyChanged(nameof(ZoneAreaText));
            UpdateStatus();
        }

        public void UpdateStatus()
        {
            if (_roomsHaveMixedArea)
            {
                HasError = true;
                ErrorText = "Разные значения " + RoomParams.FloorAreaTitle + " у помещений";
                return;
            }

            if (!HasZone)
            {
                HasError = false;
                ErrorText = HasFloorArea ? "ОК" : "—";
                return;
            }

            if (RoomCount == 0)
            {
                HasError = true;
                ErrorText = "Нет помещений на уровне";
                return;
            }

            if (!TryGetFloorArea(out double floorArea))
            {
                HasError = true;
                ErrorText = "Параметр не заполнен";
                return;
            }

            double delta = Math.Abs(_selectedZone.AreaM2 - floorArea);
            if (delta >= RoomRow.AreaErrorThreshold)
            {
                HasError = true;
                ErrorText = "Расхождение с зоной: " + ManagerUtils.AreaToText(delta) + " м²";
                return;
            }

            HasError = false;
            ErrorText = "ОК";
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
