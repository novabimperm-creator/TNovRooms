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
    public class UnplacedRoomRow
    {
        public string IdText { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public string LevelName { get; set; }
    }

    /// <summary>
    /// Обязательные проверки перед доступом к функциям: сначала неразмещённые
    /// и избыточные помещения, затем заполненность параметра «Назначение».
    /// </summary>
    public partial class GateControl : UserControl
    {
        private readonly RoomsManagerContext _context;
        private readonly RoomsManagerWindow _window;

        private ObservableCollection<DepartmentGroupRow> _groups;
        private ListCollectionView _groupsView;

        public GateControl(RoomsManagerContext context, RoomsManagerWindow window)
        {
            InitializeComponent();
            _context = context;
            _window = window;
            DepartmentOptions = new ObservableCollection<string>();

            _unplacedStageShown = !_context.UnplacedResolved;
            if (_unplacedStageShown) ShowUnplacedStage();
            else ShowDepartmentStage();
        }

        /// <summary>Шаг с неразмещёнными помещениями пропускается, если их изначально не было.</summary>
        private readonly bool _unplacedStageShown;

        /// <summary>Значения «Назначение», уже встречающиеся в модели — для выпадающего списка.</summary>
        public ObservableCollection<string> DepartmentOptions { get; }

        #region Этап 1 — неразмещённые помещения

        private void ShowUnplacedStage()
        {
            PanelUnplaced.Visibility = System.Windows.Visibility.Visible;
            PanelDepartment.Visibility = System.Windows.Visibility.Collapsed;

            List<Room> unplaced = _context.GetUnplacedRooms();

            TbStageTitle.Text = "Проверка 1 из 2. Неразмещённые и избыточные помещения";
            TbStageSubtitle.Text = "Такие помещения имеют нулевую площадь и искажают все дальнейшие расчёты. "
                                   + "Пока они есть в модели, функции менеджера недоступны.";
            TbUnplacedInfo.Text = "Найдено помещений с нулевой площадью: " + unplaced.Count
                                  + ". Их нужно удалить, чтобы продолжить работу.";

            LvUnplaced.ItemsSource = unplaced.Select(r => new UnplacedRoomRow
            {
                IdText = ManagerUtils.IdValue(r.Id).ToString(),
                Number = RoomParams.GetRoomNumber(r),
                Name = RoomParams.GetRoomName(r),
                LevelName = r.Level == null ? "" : r.Level.Name
            }).ToList();
        }

        private void DeleteUnplaced_Click(object sender, RoutedEventArgs e)
        {
            List<Room> unplaced = _context.GetUnplacedRooms();
            if (unplaced.Count == 0)
            {
                _context.UnplacedResolved = true;
                GoAfterUnplaced();
                return;
            }

            var qViewModel = new QuestionWindowViewModel
            {
                headtxt = "В проекте " + unplaced.Count + " неразмещённых или избыточных помещений. "
                          + "Удалить их? Действие можно отменить через Ctrl+Z в Revit."
            };
            var qwpfview = new QuestionWindow280(qViewModel);
            qViewModel.CloseRequest += (s, args) => qwpfview.Close();
            bool? ok = qwpfview.ShowDialog();
            if (ok != true)
            {
                Logger.Log("Пользователь отказался удалять неразмещённые помещения. Завершение работы.", 3);
                _window.CancelByUser();
                return;
            }

            int deleted = 0;
            var failed = new List<string>();
            using (var transaction = new Transaction(_context.Doc))
            {
                try
                {
                    transaction.Start("TNov - Удалить лишние помещения");
                    Logger.Log("Открываем транзакцию (удаление неразмещённых)", 1);
                    foreach (Room room in unplaced)
                    {
                        ElementId roomId = room.Id;
                        try
                        {
                            _context.Doc.Delete(roomId);
                            deleted++;
                        }
                        catch (Exception ex)
                        {
                            failed.Add(ManagerUtils.IdValue(roomId).ToString());
                            Logger.Log("Помещение " + roomId + " не удалено: " + ex.Message, 4);
                        }
                    }
                    transaction.Commit();
                    Logger.Log("Удалено помещений: " + deleted, 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при удалении помещений: " + ex.Message).ShowDialog();
                    return;
                }
            }

            _context.ReloadRooms();

            if (failed.Count > 0)
            {
                new InfoWindow280("Не удалось удалить помещения (ID): " + string.Join(", ", failed)
                                  + ".\nУдалите их вручную через спецификацию и запустите плагин заново.").ShowDialog();
                ShowUnplacedStage();
                return;
            }

            _context.UnplacedResolved = true;
            GoAfterUnplaced();
        }

        private void GoAfterUnplaced()
        {
            _context.DepartmentsResolved = _context.GetRoomsWithoutDepartment().Count == 0;
            if (_context.DepartmentsResolved)
            {
                _window.OnStagesCompleted();
                return;
            }
            ShowDepartmentStage();
        }

        #endregion

        #region Этап 2 — параметр «Назначение»

        private void ShowDepartmentStage()
        {
            PanelUnplaced.Visibility = System.Windows.Visibility.Collapsed;
            PanelDepartment.Visibility = System.Windows.Visibility.Visible;

            TbStageTitle.Text = _unplacedStageShown
                ? "Проверка 2 из 2. Параметр «Назначение»"
                : "Обязательная проверка. Параметр «Назначение»";
            TbStageSubtitle.Text = "Помещения сгруппированы по именам. Задайте «Назначение» для каждой группы, "
                                   + "где параметр заполнен не у всех помещений.";
            TbDepartmentHint.Text = "Значение записывается всем помещениям группы, у которых параметр пуст.";

            BuildDepartmentOptions();
            BuildGroups();
            UpdateDepartmentInfo();
        }

        private void BuildDepartmentOptions()
        {
            DepartmentOptions.Clear();
            foreach (string value in _context.GetDepartmentValues())
            {
                DepartmentOptions.Add(value);
            }
        }

        private void BuildGroups()
        {
            var rows = _context.Rooms
                .GroupBy(RoomParams.GetRoomName)
                .Select(g => new DepartmentGroupRow(g.Key, g.ToList()))
                .OrderBy(g => g.IsComplete)
                .ThenBy(g => g.RoomName, StringComparer.CurrentCulture)
                .ToList();

            _groups = new ObservableCollection<DepartmentGroupRow>(rows);
            _groupsView = new ListCollectionView(_groups);
            ApplyGroupFilter();
            LvDepartments.ItemsSource = _groupsView;
        }

        private void ApplyGroupFilter()
        {
            if (_groupsView == null) return;
            bool onlyEmpty = ChkOnlyEmpty.IsChecked == true;
            _groupsView.Filter = onlyEmpty
                ? new Predicate<object>(o => o is DepartmentGroupRow row && !row.IsComplete)
                : null;
            _groupsView.Refresh();
        }

        private void OnlyEmpty_Changed(object sender, RoutedEventArgs e)
        {
            ApplyGroupFilter();
        }

        private void UpdateDepartmentInfo()
        {
            int emptyRooms = _context.GetRoomsWithoutDepartment().Count;
            int emptyGroups = _groups == null ? 0 : _groups.Count(g => !g.IsComplete);
            TbDepartmentInfo.Text = "Помещений без «Назначения»: " + emptyRooms
                                    + ". Групп по именам, требующих заполнения: " + emptyGroups
                                    + ". Всего помещений в модели: " + _context.Rooms.Count + ".";
        }

        private void ApplyDepartments_Click(object sender, RoutedEventArgs e)
        {
            bool overwrite = ChkOverwrite.IsChecked == true;

            var toApply = _groups
                .Where(g => !string.IsNullOrWhiteSpace(g.SelectedDepartment))
                .ToList();

            if (toApply.Count > 0)
            {
                int written = 0;
                int skipped = 0;
                using (var transaction = new Transaction(_context.Doc))
                {
                    try
                    {
                        transaction.Start("TNov - Назначение помещений");
                        Logger.Log("Открываем транзакцию (Назначение)", 1);
                        foreach (DepartmentGroupRow group in toApply)
                        {
                            string value = group.SelectedDepartment.Trim();
                            foreach (Room room in group.Rooms)
                            {
                                if (!overwrite && RoomParams.HasDepartment(room)) continue;
                                Parameter p = room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT);
                                if (p == null || p.IsReadOnly) { skipped++; continue; }
                                p.Set(value);
                                written++;
                            }
                        }
                        transaction.Commit();
                        Logger.Log("Записано значений «Назначение»: " + written + ", пропущено: " + skipped, 1);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Ошибка: " + ex.Message, 4);
                        new InfoWindow280("Ошибка при записи параметра «Назначение»: " + ex.Message).ShowDialog();
                        return;
                    }
                }

                if (skipped > 0)
                {
                    new InfoWindow280("Не удалось записать «Назначение» у " + skipped
                                      + " помещений: параметр недоступен для записи.").ShowDialog();
                }
            }

            int stillEmpty = _context.GetRoomsWithoutDepartment().Count;
            if (stillEmpty > 0)
            {
                BuildDepartmentOptions();
                foreach (DepartmentGroupRow group in _groups) group.Refresh();
                ApplyGroupFilter();
                UpdateDepartmentInfo();
                new InfoWindow280("Осталось помещений без «Назначения»: " + stillEmpty
                                  + ".\nЗаполните значения у оставшихся групп, чтобы продолжить.").ShowDialog();
                return;
            }

            _context.DepartmentsResolved = true;
            _window.OnStagesCompleted();
        }

        #endregion

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Logger.Log("Пользователь прервал обязательные проверки. Завершение работы.", 3);
            _window.CancelByUser();
        }
    }
}
