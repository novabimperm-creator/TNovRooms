using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TNovCommon;

namespace TNovRooms.Manager
{
    public partial class OfficesControl : UserControl
    {
        private readonly RoomsManagerContext _context;
        private readonly RoomsManagerWindow _window;

        private ObservableCollection<OfficeRow> _rows;
        private ListCollectionView _rowsView;

        public OfficesControl(RoomsManagerContext context, RoomsManagerWindow window)
        {
            InitializeComponent();
            _context = context;
            _window = window;
            DataContext = _context;

            BuildRows();
            ApplyFilter();
            UpdateSummary();
        }

        #region Список офисов

        private void BuildRows()
        {
            OfficeCalculationService service = _context.CreateOfficeService();

            var rows = OfficeCalculationService.Group(_context.GetOfficeRooms())
                                               .Select(g => new OfficeRow(g))
                                               .ToList();
            foreach (OfficeRow row in rows)
            {
                row.Refresh(service);
                row.ApplyErrors();
            }

            _rows = new ObservableCollection<OfficeRow>(rows);
            _rowsView = new ListCollectionView(_rows);
            LvOffices.ItemsSource = _rowsView;
        }

        private void RefreshRows()
        {
            OfficeCalculationService service = _context.CreateOfficeService();
            foreach (OfficeRow row in _rows)
            {
                row.Refresh(service);
                row.ApplyErrors();
            }
            _rowsView.Refresh();
            UpdateSummary();
        }

        private void ApplyFilter()
        {
            if (_rowsView == null) return;

            if (ChkErrorsOnly.IsChecked == true)
                _rowsView.Filter = o => o is OfficeRow row && row.HasError;
            else
                _rowsView.Filter = null;

            _rowsView.Refresh();
        }

        private List<OfficeRow> GetVisibleRows()
        {
            return _rowsView.Cast<OfficeRow>().ToList();
        }

        private void UpdateSummary()
        {
            if (_rows.Count == 0)
            {
                TbSummary.Text = "В модели нет помещений с заполненным параметром "
                                 + OfficeParams.NumberTitle + " — заполните его в спецификации.";
                BtnRecalc.IsEnabled = false;
                return;
            }

            BtnRecalc.IsEnabled = true;

            int errors = _rows.Count(r => r.HasError);
            string errorPart = errors == 0
                ? "замечаний не найдено"
                : "офисов с замечаниями: " + errors;

            TbSummary.Text = "Показано офисов: " + GetVisibleRows().Count + " из " + _rows.Count
                             + "; помещений офисов: " + _rows.Sum(r => r.RoomsTotal)
                             + "; " + errorPart + ".";
        }

        #endregion

        #region Обработчики

        private void ErrorsOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (_rowsView == null) return;
            ApplyFilter();
            UpdateSummary();
        }

        private void Settings_Toggled(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = TglSettings.IsChecked == true
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }

        private void ApplySettings_Click(object sender, RoutedEventArgs e)
        {
            _context.SaveSettings();
            RefreshRows();
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            _context.ReloadRooms();
            _context.ReloadBackups();
            _window.UpdateBackupInfo();
            BuildRows();
            ApplyFilter();
            UpdateSummary();
        }

        #endregion

        #region Пересчёт офисографии

        private void Recalculate_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0)
            {
                new InfoWindow280("В модели нет помещений офисов — нечего пересчитывать.").ShowDialog();
                return;
            }

            bool reround = ChkReround.IsChecked == true;
            if (reround && _context.Backups.Count > 0 && !ConfirmReround()) return;

            int rerounded = 0;
            int skipped = 0;
            if (reround && !RunRounding(out rerounded, out skipped)) return;

            if (!RunOfficeCalculation()) return;

            BuildRows();
            ApplyFilter();
            UpdateSummary();

            if (reround)
            {
                string skippedPart = skipped == 0 ? "" : " Пропущено без общих параметров: " + skipped + ".";
                BackupPrompt.OfferSave(_context, _window,
                    "Переокруглено помещений офисов: " + rerounded + "." + skippedPart);
            }
        }

        private bool ConfirmReround()
        {
            string headtxt = "Внимание! Для модели уже сохранено резервных копий площадей: " + _context.Backups.Count
                             + " (последняя — «" + _context.LatestBackup.DisplayName + "»). "
                             + "Переокругление перезапишет " + RoomParams.RoundedAreaTitle + " и "
                             + RoomParams.RoundedAreaKTitle + " у помещений офисов, "
                             + "и площади могут разойтись с согласованными. Продолжить?";

            var qViewModel = new QuestionWindowViewModel { headtxt = headtxt };
            var qwpfview = new QuestionWindow280(qViewModel);
            qViewModel.CloseRequest += (s, args) => qwpfview.Close();
            bool? ok = qwpfview.ShowDialog();
            if (ok == true) return true;

            Logger.Log("Пересчёт офисографии отменён пользователем", 3);
            return false;
        }

        private bool RunRounding(out int changed, out int skipped)
        {
            RoomRoundingService rounding = _context.CreateRoundingService();
            changed = 0;
            skipped = 0;

            using (var transaction = new Transaction(_context.Doc))
            {
                try
                {
                    transaction.Start("TNov - Округлятор");
                    Logger.Log("Открываем транзакцию (переокругление площадей офисов)", 1);

                    foreach (OfficeRow row in _rows)
                    {
                        foreach (Room room in row.Rooms)
                        {
                            if (room.get_Parameter(RoomParams.RoundedArea) == null
                                || room.get_Parameter(RoomParams.RoundedAreaK) == null)
                            {
                                skipped++;
                                continue;
                            }

                            double areaR = RoomRoundingService.RoundArea(RoomParams.GetAreaM2(room));
                            double areaRK = RoomRoundingService.RoundAreaWithCoefficient(areaR, rounding.GetCoefficient(room));
                            room.get_Parameter(RoomParams.RoundedArea).Set(areaR);
                            room.get_Parameter(RoomParams.RoundedAreaK).Set(areaRK);
                            changed++;
                        }
                    }

                    transaction.Commit();
                    Logger.Log("Переокруглено помещений: " + changed + ", пропущено: " + skipped, 1);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при переокруглении площадей: " + ex.Message).ShowDialog();
                    return false;
                }
            }
        }

        private bool RunOfficeCalculation()
        {
            OfficeCalculationService service = _context.CreateOfficeService();

            using (var transaction = new Transaction(_context.Doc))
            {
                try
                {
                    transaction.Start("TNov - Офисография");
                    Logger.Log("Открываем транзакцию (офисография), офисов: " + _rows.Count, 1);

                    foreach (OfficeRow row in _rows)
                    {
                        Logger.Log("Офис " + row.NumberDisplay, 2);
                        service.Apply(row.Rooms, service.Calculate(row.Rooms));
                    }

                    transaction.Commit();
                    Logger.Log("Офисография пересчитана", 1);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при пересчёте офисографии: " + ex.Message).ShowDialog();
                    return false;
                }
            }
        }

        #endregion
    }
}
