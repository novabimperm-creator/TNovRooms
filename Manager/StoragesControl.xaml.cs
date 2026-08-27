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
using System.Windows.Input;
using System.Windows.Media;
using TNovCommon;

namespace TNovRooms.Manager
{
    public partial class StoragesControl : UserControl
    {
        private const string AllLabel = "Все";

        private readonly RoomsManagerContext _context;
        private readonly RoomsManagerWindow _window;

        private ObservableCollection<StorageRow> _rows;
        private ListCollectionView _rowsView;
        private bool _suppressEvents;

        private System.Windows.Point _dragStart;
        private StorageRow _dragRow;

        public StoragesControl(RoomsManagerContext context, RoomsManagerWindow window)
        {
            InitializeComponent();
            _context = context;
            _window = window;
            DataContext = _context;

            _suppressEvents = true;
            TbFirstNumber.Text = _context.StorageFirstNumber;
            _suppressEvents = false;

            BuildRows();
            BuildLevelFilter();
            ApplyFilter();
            UpdateSummary();
        }

        #region Список кладовых

        private void BuildRows()
        {
            RoomRoundingService rounding = _context.CreateRoundingService();
            var rows = _context.GetStorageRooms().Select(r => new StorageRow(r)).ToList();
            foreach (StorageRow row in rows) row.Refresh(rounding);

            var comparer = new AlphanumComparatorFastString();
            var ordered = rows.OrderBy(SavedOrderIndex)
                              .ThenBy(r => r.Level)
                              .ThenBy(r => r.Number, comparer)
                              .ToList();

            AddDuplicateErrors(ordered);
            foreach (StorageRow row in ordered) row.ApplyErrors();

            _rows = new ObservableCollection<StorageRow>(ordered);
            _rowsView = new ListCollectionView(_rows);
            LvStorages.ItemsSource = _rowsView;
            UpdatePreview();
        }

        /// <summary>
        /// Кладовые, которые пользователь уже перетаскивал, встают в сохранённый порядок.
        /// Новые для этого запуска уходят в конец и сортируются по этажу и номеру.
        /// </summary>
        private int SavedOrderIndex(StorageRow row)
        {
            int index = _context.StorageOrder.IndexOf(row.RoomId);
            return index < 0 ? int.MaxValue : index;
        }

        private static void AddDuplicateErrors(List<StorageRow> rows)
        {
            foreach (var pair in rows.Where(r => r.Number.Length > 0)
                                     .GroupBy(r => r.Number)
                                     .Where(g => g.Count() > 1))
            {
                foreach (StorageRow row in pair)
                    row.AddError("Номер «" + pair.Key + "» дублируется у кладовых: " + pair.Count());
            }
        }

        private void RefreshRows()
        {
            RoomRoundingService rounding = _context.CreateRoundingService();
            foreach (StorageRow row in _rows) row.Refresh(rounding);
            AddDuplicateErrors(_rows.ToList());
            foreach (StorageRow row in _rows) row.ApplyErrors();
            _rowsView.Refresh();
            UpdatePreview();
            UpdateSummary();
        }

        /// <summary>Проставляет в колонку «Новый №» номера, которые даст перенумерация.</summary>
        private void UpdatePreview()
        {
            int first = GetFirstNumber();
            for (int i = 0; i < _rows.Count; i++) _rows[i].SetPreview(first + i);
        }

        private int GetFirstNumber()
        {
            int first;
            return int.TryParse(TbFirstNumber.Text, out first) ? first : 1;
        }

        private void BuildLevelFilter()
        {
            string previous = CbLevelFilter.SelectedItem as string;

            var values = new List<string> { AllLabel };
            values.AddRange(_rows.Select(r => r.Level)
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
                    var row = o as StorageRow;
                    if (row == null) return false;
                    if (errorsOnly && !row.HasError) return false;
                    return level == AllLabel || row.LevelText == level;
                };
            }
            _rowsView.Refresh();
        }

        private void UpdateSummary()
        {
            if (_rows.Count == 0)
            {
                TbSummary.Text = "В модели нет помещений с именем «" + RoomParams.StorageRoomName
                                 + "» вне квартир и офисов.";
                BtnRenumber.IsEnabled = false;
                return;
            }

            BtnRenumber.IsEnabled = true;

            int errors = _rows.Count(r => r.HasError);
            string errorPart = errors == 0 ? "замечаний не найдено" : "кладовых с замечаниями: " + errors;

            TbSummary.Text = "Показано кладовых: " + _rowsView.Cast<StorageRow>().Count() + " из " + _rows.Count
                             + "; нумерация пойдёт по всему списку с номера " + GetFirstNumber()
                             + " по " + (GetFirstNumber() + _rows.Count - 1)
                             + "; " + errorPart + ".";
        }

        #endregion

        #region Перетаскивание строк

        private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            _dragRow = GetRowUnder(e.OriginalSource as DependencyObject);
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragRow == null) return;

            var position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            StorageRow row = _dragRow;
            _dragRow = null;
            DragDrop.DoDragDrop(LvStorages, row, DragDropEffects.Move);
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(StorageRow)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            var source = e.Data.GetData(typeof(StorageRow)) as StorageRow;
            if (source == null) return;

            int from = _rows.IndexOf(source);
            if (from < 0) return;

            StorageRow target = GetRowUnder(e.OriginalSource as DependencyObject);
            // сброс на пустое место под списком означает перенос в конец
            int to = target == null ? _rows.Count - 1 : _rows.IndexOf(target);
            if (to < 0 || to == from) return;

            _rows.Move(from, to);
            SaveOrder();
            UpdatePreview();
            // при активном фильтре представление после Move корректнее пересобрать целиком
            _rowsView.Refresh();
            LvStorages.SelectedItem = source;
            LvStorages.ScrollIntoView(source);
        }

        private StorageRow GetRowUnder(DependencyObject source)
        {
            while (source != null && !(source is DataGridRow))
            {
                DependencyObject parent = source is Visual ? VisualTreeHelper.GetParent(source) : null;
                source = parent ?? LogicalTreeHelper.GetParent(source);
            }
            var gridRow = source as DataGridRow;
            return gridRow == null ? null : gridRow.Item as StorageRow;
        }

        private void SaveOrder()
        {
            _context.StorageOrder = _rows.Select(r => r.RoomId).ToList();
        }

        #endregion

        #region Обработчики

        private void FirstNumber_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressEvents) return;
            _context.StorageFirstNumber = TbFirstNumber.Text;
            if (_rows == null) return;
            UpdatePreview();
            UpdateSummary();
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

        private void ResetOrder_Click(object sender, RoutedEventArgs e)
        {
            _context.StorageOrder = new List<long>();
            BuildRows();
            ApplyFilter();
            UpdateSummary();
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
            _window.RequestManualNumbering(ManualNumberingKind.RoomNumber);
        }

        #endregion

        #region Перенумерация

        private void Renumber_Click(object sender, RoutedEventArgs e)
        {
            if (_rows.Count == 0)
            {
                new InfoWindow280("В модели нет кладовых — нумеровать нечего.").ShowDialog();
                return;
            }

            int first;
            if (!int.TryParse(TbFirstNumber.Text, out first))
            {
                new InfoWindow280("Первый номер должен быть целым числом.").ShowDialog();
                return;
            }

            bool reround = ChkReround.IsChecked == true;
            if (reround && _context.Backups.Count > 0 && !ConfirmReround()) return;

            if (!RunRenumber(first)) return;

            int rerounded = 0;
            int skipped = 0;
            if (reround && !RunRounding(out rerounded, out skipped)) return;

            RefreshRows();

            if (reround)
            {
                string skippedPart = skipped == 0 ? "" : " Пропущено без общих параметров: " + skipped + ".";
                BackupPrompt.OfferSave(_context, _window,
                    "Переокруглено кладовых: " + rerounded + "." + skippedPart);
            }
            else
            {
                new InfoWindow280("Номера кладовых заполнены: " + _rows.Count
                                  + " шт., с " + first + " по " + (first + _rows.Count - 1) + ".").ShowDialog();
            }
        }

        private bool ConfirmReround()
        {
            string headtxt = "Внимание! Для модели уже сохранено резервных копий площадей: " + _context.Backups.Count
                             + " (последняя — «" + _context.LatestBackup.DisplayName + "»). "
                             + "Пересчёт перезапишет " + RoomParams.RoundedAreaTitle + " и "
                             + RoomParams.RoundedAreaKTitle + " у кладовых, "
                             + "и площади могут разойтись с согласованными. Продолжить?";

            var qViewModel = new QuestionWindowViewModel { headtxt = headtxt };
            var qwpfview = new QuestionWindow280(qViewModel);
            qViewModel.CloseRequest += (s, args) => qwpfview.Close();
            bool? ok = qwpfview.ShowDialog();
            if (ok == true) return true;

            Logger.Log("Перенумерация кладовых отменена пользователем", 3);
            return false;
        }

        /// <summary>
        /// Номера пишутся по порядку строк списка. По ходу нумерации номер может совпасть
        /// с номером другого помещения, поэтому предупреждения Revit гасятся. Взят
        /// WarningSkipper, а не WarningResolver: он только закрывает предупреждения и
        /// никогда не удаляет элементы.
        /// </summary>
        private bool RunRenumber(int first)
        {
            using (var transaction = new Transaction(_context.Doc))
            {
                try
                {
                    transaction.Start("TNov - Номера кладовых");
                    FailureHandlingOptions failOptions = transaction.GetFailureHandlingOptions();
                    failOptions.SetFailuresPreprocessor(new WarningSkipper());
                    transaction.SetFailureHandlingOptions(failOptions);
                    Logger.Log("Открываем транзакцию (номера кладовых), кладовых: " + _rows.Count, 1);

                    int value = first;
                    foreach (StorageRow row in _rows)
                    {
                        row.Room.get_Parameter(BuiltInParameter.ROOM_NUMBER)?.Set(value.ToString(CultureInfo.InvariantCulture));
                        Logger.Log("   Помещение " + row.Room.Id + " номер " + value, 2);
                        value++;
                    }

                    transaction.Commit();
                    Logger.Log("Пронумеровано кладовых: " + _rows.Count, 1);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при заполнении номеров: " + ex.Message).ShowDialog();
                    return false;
                }
            }
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
                    Logger.Log("Открываем транзакцию (переокругление площадей кладовых)", 1);

                    foreach (StorageRow row in _rows)
                    {
                        if (!row.HasParams) { skipped++; continue; }

                        double areaR = RoomRoundingService.RoundArea(RoomParams.GetAreaM2(row.Room));
                        double areaRK = RoomRoundingService.RoundAreaWithCoefficient(areaR, rounding.GetCoefficient(row.Room));
                        row.Room.get_Parameter(RoomParams.RoundedArea).Set(areaR);
                        row.Room.get_Parameter(RoomParams.RoundedAreaK).Set(areaRK);
                        changed++;
                    }

                    transaction.Commit();
                    Logger.Log("Переокруглено кладовых: " + changed + ", пропущено: " + skipped, 1);
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при пересчёте площадей: " + ex.Message).ShowDialog();
                    return false;
                }
            }
        }

        #endregion
    }
}
