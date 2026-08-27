using Autodesk.Revit.DB;
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
    public partial class RoundingControl : UserControl
    {
        private const string AllLabel = "Все";
        private const string EmptyLabel = "(не заполнено)";

        private readonly RoomsManagerContext _context;
        private readonly RoomsManagerWindow _window;

        private ObservableCollection<RoomRow> _rows;
        private ListCollectionView _rowsView;
        private bool _suppressFilterEvent;

        public RoundingControl(RoomsManagerContext context, RoomsManagerWindow window)
        {
            InitializeComponent();
            _context = context;
            _window = window;
            DataContext = _context;

            BuildRows();
            BuildDepartmentFilter();
            ApplyFilter();
            UpdateSummary();
        }

        #region Список помещений

        private void BuildRows()
        {
            RoomRoundingService rounding = _context.CreateRoundingService();
            RoomAreaBackup latest = _context.LatestBackup;

            var rows = new List<RoomRow>();
            foreach (var room in _context.Rooms)
            {
                var row = new RoomRow(room);
                row.Refresh(rounding);
                row.ApplyBackup(latest);
                rows.Add(row);
            }

            _rows = new ObservableCollection<RoomRow>(
                rows.OrderBy(r => r.Name, StringComparer.CurrentCulture)
                    .ThenBy(r => r.Number, new AlphanumComparatorFastString()));

            _rowsView = new ListCollectionView(_rows);
            LvRooms.ItemsSource = _rowsView;
        }

        private void RefreshRows()
        {
            RoomRoundingService rounding = _context.CreateRoundingService();
            RoomAreaBackup latest = _context.LatestBackup;
            foreach (RoomRow row in _rows)
            {
                row.Refresh(rounding);
                row.ApplyBackup(latest);
            }
            _rowsView.Refresh();
            UpdateSummary();
        }

        private void BuildDepartmentFilter()
        {
            string previous = CbDepartmentFilter.SelectedItem as string;

            var values = new List<string> { AllLabel };
            values.AddRange(_rows.Select(r => r.Department)
                                 .Where(v => v.Length > 0)
                                 .Distinct()
                                 .OrderBy(v => v, StringComparer.CurrentCulture));
            if (_rows.Any(r => r.Department.Length == 0)) values.Add(EmptyLabel);

            _suppressFilterEvent = true;
            CbDepartmentFilter.ItemsSource = values;
            CbDepartmentFilter.SelectedItem = previous != null && values.Contains(previous) ? previous : AllLabel;
            _suppressFilterEvent = false;
        }

        private void ApplyFilter()
        {
            if (_rowsView == null) return;
            string selected = CbDepartmentFilter.SelectedItem as string ?? AllLabel;

            if (selected == AllLabel)
            {
                _rowsView.Filter = null;
            }
            else if (selected == EmptyLabel)
            {
                _rowsView.Filter = o => o is RoomRow row && row.Department.Length == 0;
            }
            else
            {
                _rowsView.Filter = o => o is RoomRow row && row.Department == selected;
            }
            _rowsView.Refresh();
        }

        private List<RoomRow> GetVisibleRows()
        {
            return _rowsView.Cast<RoomRow>().ToList();
        }

        private void UpdateSummary()
        {
            List<RoomRow> visible = GetVisibleRows();
            int errors = visible.Count(r => r.HasError);
            int differs = visible.Count(r => r.BackupState == BackupMatchState.Differs);
            int noParams = visible.Count(r => !r.HasParams);

            RoomAreaBackup latest = _context.LatestBackup;
            string backupPart = latest == null
                ? "бэкапов площадей для модели нет"
                : "последний бэкап: «" + latest.DisplayName + "», отличий от него: " + differs;

            string errorPart = errors == 0
                ? "расхождений не найдено"
                : "помещений с ошибками: " + errors;

            string paramsPart = noParams == 0
                ? ""
                : "; без общих параметров площадей: " + noParams;

            TbSummary.Text = "Показано помещений: " + visible.Count + " из " + _rows.Count
                             + "; " + errorPart + paramsPart + "; " + backupPart + ".";
        }

        #endregion

        #region Обработчики

        private void DepartmentFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressFilterEvent) return;
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
            BuildDepartmentFilter();
            ApplyFilter();
            UpdateSummary();
        }

        private void Recalculate_Click(object sender, RoutedEventArgs e)
        {
            List<RoomRow> visible = GetVisibleRows();
            if (visible.Count == 0)
            {
                new InfoWindow280("В списке нет помещений — нечего пересчитывать.").ShowDialog();
                return;
            }

            if (!ConfirmRecalculation(visible.Count)) return;

            RoomRoundingService rounding = _context.CreateRoundingService();
            int changed = 0;
            int skipped = 0;

            using (var transaction = new Transaction(_context.Doc))
            {
                try
                {
                    transaction.Start("TNov - Округлятор");
                    Logger.Log("Открываем транзакцию (пересчёт площадей), помещений: " + visible.Count, 1);

                    foreach (RoomRow row in visible)
                    {
                        if (!row.HasParams) { skipped++; continue; }

                        double area = RoomParams.GetAreaM2(row.Room);
                        double areaR = RoomRoundingService.RoundArea(area);
                        double k = rounding.GetCoefficient(row.Room);
                        double areaRK = RoomRoundingService.RoundAreaWithCoefficient(areaR, k);

                        row.Room.get_Parameter(RoomParams.RoundedArea)?.Set(areaR);
                        row.Room.get_Parameter(RoomParams.RoundedAreaK)?.Set(areaRK);
                        changed++;
                    }

                    transaction.Commit();
                    Logger.Log("Пересчитано помещений: " + changed + ", пропущено: " + skipped, 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при пересчёте площадей: " + ex.Message).ShowDialog();
                    return;
                }
            }

            RefreshRows();
            OfferBackup(changed, skipped);
        }

        private bool ConfirmRecalculation(int visibleCount)
        {
            string headtxt;
            if (_context.Backups.Count > 0)
            {
                headtxt = "Внимание! Для модели уже сохранено резервных копий площадей: " + _context.Backups.Count
                          + " (последняя — «" + _context.LatestBackup.DisplayName + "»). "
                          + "Пересчёт перезапишет " + RoomParams.RoundedAreaTitle + " и " + RoomParams.RoundedAreaKTitle
                          + " у " + visibleCount + " помещений, и площади могут разойтись с согласованными. Продолжить?";
            }
            else
            {
                headtxt = "Будут пересчитаны " + RoomParams.RoundedAreaTitle + " и " + RoomParams.RoundedAreaKTitle
                          + " у " + visibleCount + " помещений. Продолжить?";
            }

            var qViewModel = new QuestionWindowViewModel { headtxt = headtxt };
            var qwpfview = new QuestionWindow280(qViewModel);
            qViewModel.CloseRequest += (s, args) => qwpfview.Close();
            bool? ok = qwpfview.ShowDialog();
            if (ok == true) return true;

            Logger.Log("Пересчёт отменён пользователем", 3);
            return false;
        }

        private void OfferBackup(int changed, int skipped)
        {
            string skippedPart = skipped == 0
                ? ""
                : " Пропущено без общих параметров: " + skipped + ".";

            if (BackupPrompt.OfferSave(_context, _window, "Пересчитано помещений: " + changed + "." + skippedPart))
                RefreshRows();
        }

        #endregion
    }
}
