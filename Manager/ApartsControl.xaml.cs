using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using TNovCommon;

namespace TNovRooms.Manager
{
    public partial class ApartsControl : UserControl
    {
        private const string AllLabel = "Все";

        private readonly RoomsManagerContext _context;
        private readonly RoomsManagerWindow _window;

        private ObservableCollection<ApartRow> _rows;
        private ListCollectionView _rowsView;
        private bool _suppressEvents;

        public ApartsControl(RoomsManagerContext context, RoomsManagerWindow window)
        {
            InitializeComponent();
            _context = context;
            _window = window;
            DataContext = _context;

            _suppressEvents = true;
            TbFirstNumber.Text = _context.ApartFirstNumber;
            _suppressEvents = false;

            BuildRows();
            BuildLevelFilter();
            ApplyFilter();
            UpdateSummary();
        }

        #region Список квартир

        private void BuildRows()
        {
            ApartCalculationService service = _context.CreateApartService();
            List<Room> apartRooms = _context.GetApartmentRooms();

            var rows = ApartCalculationService.Group(apartRooms).Select(g => new ApartRow(g)).ToList();
            foreach (ApartRow row in rows) row.Refresh(service);
            AddCrossErrors(rows);
            foreach (ApartRow row in rows) row.ApplyErrors();

            _rows = new ObservableCollection<ApartRow>(rows);
            _rowsView = new ListCollectionView(_rows);
            LvAparts.ItemsSource = _rowsView;
        }

        /// <summary>
        /// Одна физическая квартира (этаж + номер на этаже) должна иметь один сквозной номер.
        /// Обратный случай — один номер у разных квартир — ловится внутри строки.
        /// </summary>
        private static void AddCrossErrors(List<ApartRow> rows)
        {
            var byPlacement = new Dictionary<string, List<ApartRow>>();
            foreach (ApartRow row in rows)
            {
                foreach (string key in row.PlacementKeys)
                {
                    List<ApartRow> group;
                    if (!byPlacement.TryGetValue(key, out group))
                    {
                        group = new List<ApartRow>();
                        byPlacement[key] = group;
                    }
                    group.Add(row);
                }
            }

            foreach (var pair in byPlacement)
            {
                if (pair.Value.Count < 2) continue;
                string numbers = string.Join(", ", pair.Value.Select(r => r.NumberDisplay).ToArray());
                foreach (ApartRow row in pair.Value)
                    row.AddError("Одна квартира разбита на несколько сквозных номеров: " + numbers);
            }
        }

        private void RefreshRows()
        {
            ApartCalculationService service = _context.CreateApartService();
            foreach (ApartRow row in _rows) row.Refresh(service);
            AddCrossErrors(_rows.ToList());
            foreach (ApartRow row in _rows) row.ApplyErrors();
            _rowsView.Refresh();
            UpdateSummary();
        }

        private void BuildLevelFilter()
        {
            string previous = CbLevelFilter.SelectedItem as string;

            var values = new List<string> { AllLabel };
            values.AddRange(_rows.SelectMany(r => r.Levels)
                                 .Distinct()
                                 .OrderBy(v => v)
                                 .Select(v => v.ToString("0.###", CultureInfo.CurrentCulture)));

            _suppressEvents = true;
            CbLevelFilter.ItemsSource = values;
            CbLevelFilter.SelectedItem = previous != null && values.Contains(previous) ? previous : AllLabel;
            _suppressEvents = false;
        }

        private void ApplyFilter()
        {
            if (_rowsView == null) return;

            string level = CbLevelFilter.SelectedItem as string ?? AllLabel;
            bool errorsOnly = ChkErrorsOnly.IsChecked == true;

            if (level == AllLabel && !errorsOnly)
            {
                _rowsView.Filter = null;
            }
            else
            {
                _rowsView.Filter = o =>
                {
                    var row = o as ApartRow;
                    if (row == null) return false;
                    if (errorsOnly && !row.HasError) return false;
                    if (level == AllLabel) return true;
                    return row.Levels.Any(v => v.ToString("0.###", CultureInfo.CurrentCulture) == level);
                };
            }
            _rowsView.Refresh();
        }

        private List<ApartRow> GetVisibleRows()
        {
            return _rowsView.Cast<ApartRow>().ToList();
        }

        private void UpdateSummary()
        {
            List<ApartRow> visible = GetVisibleRows();
            int errors = _rows.Count(r => r.HasError);
            int withoutNumber = _rows.Count(r => r.Number.Length == 0);

            if (_rows.Count == 0)
            {
                TbSummary.Text = "В модели нет помещений с включённым параметром N_Квартира — заполните его в спецификации.";
                BtnRecalc.IsEnabled = false;
                BtnRenumber.IsEnabled = false;
                TbRenumberHint.Text = "";
                return;
            }

            BtnRecalc.IsEnabled = true;
            BtnRenumber.IsEnabled = true;

            string errorPart = errors == 0
                ? "замечаний не найдено"
                : "квартир с замечаниями: " + errors;

            TbSummary.Text = "Показано квартир: " + visible.Count + " из " + _rows.Count
                             + "; помещений квартир: " + _rows.Sum(r => r.RoomsTotal)
                             + "; " + errorPart + ".";

            TbRenumberHint.Text = withoutNumber == 0
                ? ""
                : "Есть квартиры без сквозного номера — пересчёт квартирографии заблокирован.";
        }

        #endregion

        #region Обработчики

        private void FirstNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents) return;
            _context.ApartFirstNumber = TbFirstNumber.Text;
        }

        private void LevelFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressEvents) return;
            ApplyFilter();
            UpdateSummary();
        }

        private void ErrorsOnly_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents || _rowsView == null) return;
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
            BuildLevelFilter();
            ApplyFilter();
            UpdateSummary();
        }

        private void ManualNumbering_Click(object sender, RoutedEventArgs e)
        {
            _window.RequestManualNumbering(ManualNumberingKind.ApartAtLevel);
        }

        #endregion

        #region Пересчёт квартирографии

        private void Recalculate_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0)
            {
                new InfoWindow280("В модели нет помещений квартир — нечего пересчитывать.").ShowDialog();
                return;
            }

            int withoutNumber = _rows.Count(r => r.Number.Length == 0);
            if (withoutNumber > 0)
            {
                ApartRow row = _rows.First(r => r.Number.Length == 0);
                new InfoWindow280("У " + row.RoomsTotal + " помещений квартир не заполнен " + ApartParams.NumberTitle + ". "
                                  + "Без сквозных номеров помещения нельзя собрать в квартиры. "
                                  + "Нажмите «Пересчитать сквозные номера» или заполните номера вручную.").ShowDialog();
                Logger.Log("Пересчёт квартирографии заблокирован: есть помещения без " + ApartParams.NumberTitle, 3);
                return;
            }

            bool reround = ChkReround.IsChecked == true;
            if (reround && _context.Backups.Count > 0 && !ConfirmReround()) return;

            int rerounded = 0;
            int skipped = 0;
            if (reround && !RunRounding(out rerounded, out skipped)) return;

            if (!RunApartCalculation()) return;

            BuildRows();
            BuildLevelFilter();
            ApplyFilter();
            UpdateSummary();

            if (reround)
            {
                string skippedPart = skipped == 0 ? "" : " Пропущено без общих параметров: " + skipped + ".";
                BackupPrompt.OfferSave(_context, _window,
                    "Переокруглено помещений квартир: " + rerounded + "." + skippedPart);
            }
        }

        private bool ConfirmReround()
        {
            string headtxt = "Внимание! Для модели уже сохранено резервных копий площадей: " + _context.Backups.Count
                             + " (последняя — «" + _context.LatestBackup.DisplayName + "»). "
                             + "Переокругление перезапишет " + RoomParams.RoundedAreaTitle + " и "
                             + RoomParams.RoundedAreaKTitle + " у помещений квартир, "
                             + "и площади могут разойтись с согласованными. Продолжить?";

            var qViewModel = new QuestionWindowViewModel { headtxt = headtxt };
            var qwpfview = new QuestionWindow280(qViewModel);
            qViewModel.CloseRequest += (s, args) => qwpfview.Close();
            bool? ok = qwpfview.ShowDialog();
            if (ok == true) return true;

            Logger.Log("Пересчёт квартирографии отменён пользователем", 3);
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
                    Logger.Log("Открываем транзакцию (переокругление площадей квартир)", 1);

                    foreach (ApartRow row in _rows)
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

        private bool RunApartCalculation()
        {
            ApartCalculationService service = _context.CreateApartService();

            using (var transaction = new Transaction(_context.Doc))
            {
                try
                {
                    transaction.Start("TNov - Квартирография");
                    Logger.Log("Открываем транзакцию (квартирография), квартир: " + _rows.Count, 1);

                    foreach (ApartRow row in _rows)
                    {
                        Logger.Log("Квартира " + row.NumberDisplay, 2);
                        service.Apply(row.Rooms, service.Calculate(row.Rooms));
                    }

                    transaction.Commit();
                    Logger.Log("Квартирография пересчитана", 1);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при пересчёте квартирографии: " + ex.Message).ShowDialog();
                    return false;
                }
            }
        }

        #endregion

        #region Сквозная нумерация

        private void Renumber_Click(object sender, RoutedEventArgs e)
        {
            int first;
            if (!int.TryParse(TbFirstNumber.Text, out first))
            {
                new InfoWindow280("Номер первой квартиры должен быть целым числом.").ShowDialog();
                return;
            }

            List<Room> apartRooms = _context.GetApartmentRooms();
            ApartNumberingPlan plan = ApartNumberingService.Build(apartRooms, first);
            if (plan.ApartCount == 0)
            {
                new InfoWindow280("В модели нет помещений квартир — нумеровать нечего.").ShowDialog();
                return;
            }

            int written;
            using (var transaction = new Transaction(_context.Doc))
            {
                try
                {
                    transaction.Start("TNov - Сквозные номера квартир");
                    Logger.Log("Открываем транзакцию (сквозные номера квартир), квартир: " + plan.ApartCount, 1);
                    written = ApartNumberingService.Apply(plan);
                    transaction.Commit();
                    Logger.Log("Заполнено помещений: " + written, 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при заполнении сквозных номеров: " + ex.Message).ShowDialog();
                    return;
                }
            }

            BuildRows();
            BuildLevelFilter();
            ApplyFilter();
            UpdateSummary();

            new InfoWindow280("Сквозные номера заполнены. Квартир: " + plan.ApartCount
                              + ", номера с " + plan.FirstNumber + " по " + plan.LastNumber
                              + ". Изменено квартир: " + plan.ChangedCount + ".").ShowDialog();
        }

        #endregion
    }
}
