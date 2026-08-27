using Autodesk.Revit.DB.Architecture;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TNovRooms.Manager
{
    /// <summary>
    /// Группа помещений с одинаковым именем на этапе заполнения параметра «Назначение».
    /// </summary>
    public class DepartmentGroupRow : INotifyPropertyChanged
    {
        private string _selectedDepartment;

        public DepartmentGroupRow(string roomName, List<Room> rooms)
        {
            RoomName = roomName;
            Rooms = rooms;
            Refresh();
        }

        public string RoomName { get; }
        public List<Room> Rooms { get; }

        public int TotalCount => Rooms.Count;
        public int EmptyCount { get; private set; }
        public bool IsComplete => EmptyCount == 0;

        public string CountText { get; private set; }
        public string CurrentValuesText { get; private set; }
        public string StatusText { get; private set; }

        /// <summary>Значение, которое пользователь выбрал или ввёл для этой группы.</summary>
        public string SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                _selectedDepartment = value;
                OnPropertyChanged();
            }
        }

        public void Refresh()
        {
            EmptyCount = Rooms.Count(r => !RoomParams.HasDepartment(r));

            var values = Rooms.Select(RoomParams.GetDepartment)
                              .Where(v => v.Length > 0)
                              .Distinct()
                              .OrderBy(v => v)
                              .ToList();

            CountText = TotalCount.ToString();
            CurrentValuesText = values.Count == 0 ? "—" : string.Join(", ", values);
            StatusText = IsComplete
                ? "заполнено"
                : "не заполнено: " + EmptyCount + " из " + TotalCount;

            // Если в группе уже есть единственное значение — предлагаем его по умолчанию.
            if (string.IsNullOrWhiteSpace(_selectedDepartment) && values.Count == 1)
            {
                _selectedDepartment = values[0];
            }

            OnPropertyChanged(nameof(EmptyCount));
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(CurrentValuesText));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(SelectedDepartment));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
