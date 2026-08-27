using Autodesk.Revit.DB.Architecture;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace TNovRooms.Manager
{
    public enum BackupMatchState
    {
        NoBackup,
        NotInBackup,
        Match,
        Differs
    }

    /// <summary>Строка списка помещений во вкладке «Округлятор».</summary>
    public class RoomRow : INotifyPropertyChanged
    {
        /// <summary>
        /// Порог расхождения площадей. 0.1 из задания минус защита от погрешности double:
        /// иначе разница ровно 0.1 иногда вычисляется как 0.09999999999999998.
        /// </summary>
        public const double AreaErrorThreshold = 0.0999;

        /// <summary>Округлённые площади хранятся с одним знаком, для сравнения хватает 0.005.</summary>
        public const double RoundedCompareTolerance = 0.005;

        private double _roundedArea;
        private double _roundedAreaK;
        private BackupMatchState _backupState;
        private double _backupArea;
        private double _backupAreaK;

        public RoomRow(Room room)
        {
            Room = room;
            RoomId = ManagerUtils.IdValue(room.Id);
        }

        public Room Room { get; }
        public long RoomId { get; }

        public string IdText => RoomId.ToString(CultureInfo.InvariantCulture);
        public string Number { get; private set; }
        public string Name { get; private set; }
        public string Department { get; private set; }

        public double AreaM2 { get; private set; }
        public double Coefficient { get; private set; }
        public bool HasParams { get; private set; }

        public double RoundedArea => _roundedArea;
        public double RoundedAreaK => _roundedAreaK;

        public double ExpectedRoundedArea { get; private set; }
        public double ExpectedRoundedAreaK { get; private set; }

        public string AreaText => ManagerUtils.AreaToText(AreaM2);
        public string RoundedAreaText => HasParams ? ManagerUtils.AreaToText(_roundedArea) : "—";
        public string RoundedAreaKText => HasParams ? ManagerUtils.AreaToText(_roundedAreaK) : "—";
        public string CoefficientText => ManagerUtils.CoefficientToText(Coefficient);

        public bool HasError { get; private set; }
        public string ErrorText { get; private set; }

        public BackupMatchState BackupState => _backupState;
        public bool BackupDiffers => _backupState == BackupMatchState.Differs;
        public string BackupText { get; private set; }

        /// <summary>Перечитывает значения из модели и пересобирает статусы.</summary>
        public void Refresh(RoomRoundingService rounding)
        {
            Number = RoomParams.GetRoomNumber(Room);
            Name = RoomParams.GetRoomName(Room);
            Department = RoomParams.GetDepartment(Room);
            AreaM2 = RoomParams.GetAreaM2(Room);
            HasParams = RoomParams.HasRoundingParams(Room);
            Coefficient = rounding.GetCoefficient(Room);

            ExpectedRoundedArea = RoomRoundingService.RoundArea(AreaM2);
            ExpectedRoundedAreaK = RoomRoundingService.RoundAreaWithCoefficient(ExpectedRoundedArea, Coefficient);

            _roundedArea = HasParams ? RoomParams.GetRoundedArea(Room) : 0;
            _roundedAreaK = HasParams ? RoomParams.GetRoundedAreaK(Room) : 0;

            UpdateError();
            UpdateBackupText();
            RaiseAllChanged();
        }

        private void UpdateError()
        {
            if (!HasParams)
            {
                HasError = true;
                ErrorText = "Нет общих параметров площадей";
                return;
            }

            double delta = Math.Abs(AreaM2 - _roundedArea);
            if (delta >= AreaErrorThreshold)
            {
                HasError = true;
                ErrorText = "Расхождение с «Площадь»: " + ManagerUtils.AreaToText(delta) + " м²";
                return;
            }

            if (Math.Abs(_roundedAreaK - ExpectedRoundedAreaK) >= RoundedCompareTolerance)
            {
                HasError = true;
                ErrorText = "«ОкруглСКоэффициентом» ≠ " + ManagerUtils.AreaToText(ExpectedRoundedAreaK);
                return;
            }

            HasError = false;
            ErrorText = "ОК";
        }

        public void ApplyBackup(RoomAreaBackup backup)
        {
            if (backup == null)
            {
                _backupState = BackupMatchState.NoBackup;
            }
            else if (!backup.TryGetEntry(RoomId, out RoomAreaBackupEntry entry))
            {
                _backupState = BackupMatchState.NotInBackup;
            }
            else
            {
                _backupArea = entry.Area;
                _backupAreaK = entry.AreaK;
                bool same = Math.Abs(_backupArea - _roundedArea) < RoundedCompareTolerance
                            && Math.Abs(_backupAreaK - _roundedAreaK) < RoundedCompareTolerance;
                _backupState = same ? BackupMatchState.Match : BackupMatchState.Differs;
            }

            UpdateBackupText();
            OnPropertyChanged(nameof(BackupText));
            OnPropertyChanged(nameof(BackupDiffers));
            OnPropertyChanged(nameof(BackupState));
        }

        private void UpdateBackupText()
        {
            switch (_backupState)
            {
                case BackupMatchState.NoBackup:
                    BackupText = "нет бэкапов";
                    break;
                case BackupMatchState.NotInBackup:
                    BackupText = "нет в бэкапе";
                    break;
                case BackupMatchState.Match:
                    BackupText = "соответствует";
                    break;
                default:
                    BackupText = "было " + ManagerUtils.AreaToText(_backupArea)
                                 + " / " + ManagerUtils.AreaToText(_backupAreaK);
                    break;
            }
        }

        private void RaiseAllChanged()
        {
            OnPropertyChanged(nameof(Number));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Department));
            OnPropertyChanged(nameof(AreaText));
            OnPropertyChanged(nameof(RoundedAreaText));
            OnPropertyChanged(nameof(RoundedAreaKText));
            OnPropertyChanged(nameof(CoefficientText));
            OnPropertyChanged(nameof(HasError));
            OnPropertyChanged(nameof(ErrorText));
            OnPropertyChanged(nameof(BackupText));
            OnPropertyChanged(nameof(BackupDiffers));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
