using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>
    /// Вкладка «Коэффициенты»: привязка зон к уровням, ручной ввод
    /// и запись N_Площадь этажа в помещения.
    /// </summary>
    public partial class PlaceholderControl : UserControl
    {
        private readonly RoomsManagerContext _context;
        private readonly RoomsManagerWindow _window;

        private FloorLevelAreaData _data;
        private ObservableCollection<FloorLevelAreaRow> _rows;
        private Dictionary<long, Area> _zonesById = new Dictionary<long, Area>();
        private bool _parameterReady;
        private bool _suppressSelectionSave;

        public PlaceholderControl(RoomsManagerContext context, RoomsManagerWindow window)
        {
            InitializeComponent();
            _context = context;
            _window = window;
            ZoneOptions = new ObservableCollection<FloorZoneOption>();

            if (!EnsureSharedParameter())
            {
                LvLevels.IsEnabled = false;
                return;
            }

            _data = FloorLevelAreaStore.Load();
            BuildZoneOptions();
            BuildRows();
            UpdateSummary();
        }

        /// <summary>Общий список зон для ComboBox во всех строках.</summary>
        public ObservableCollection<FloorZoneOption> ZoneOptions { get; }

        private bool EnsureSharedParameter()
        {
            string error;
            _parameterReady = FloorAreaParameterService.EnsureBound(
                _context.Doc,
                _context.CommandData.Application.Application,
                out error);

            if (_parameterReady)
            {
                ErrorPanel.Visibility = System.Windows.Visibility.Collapsed;
                return true;
            }

            TbError.Text = error;
            ErrorPanel.Visibility = System.Windows.Visibility.Visible;
            TbSummary.Text = "Параметр «" + RoomParams.FloorAreaTitle + "» недоступен — таблица отключена.";
            new InfoWindow280(error).ShowDialog();
            return false;
        }

        private void BuildZoneOptions()
        {
            ZoneOptions.Clear();
            _zonesById.Clear();
            ZoneOptions.Add(FloorZoneOption.None);

            var areas = new FilteredElementCollector(_context.Doc)
                .OfCategory(BuiltInCategory.OST_Areas)
                .WhereElementIsNotElementType()
                .Cast<Area>()
                .Where(a => a.Area > 0)
                .OrderBy(a => a.Level?.Elevation ?? 0)
                .ThenBy(a => a.Name ?? "", StringComparer.CurrentCulture)
                .ThenBy(a => a.Number ?? "", StringComparer.CurrentCulture)
                .ToList();

            foreach (Area area in areas)
            {
                long zoneId = ManagerUtils.IdValue(area.Id);
                _zonesById[zoneId] = area;

                string zoneName = GetAreaName(area);
                string levelName = area.Level?.Name ?? "(без уровня)";
                double areaM2 = ManagerUtils.InternalToSqMeters(area.Area);

                ZoneOptions.Add(new FloorZoneOption
                {
                    ZoneId = zoneId,
                    AreaM2 = areaM2,
                    LevelName = levelName,
                    DisplayName = zoneName + " — " + levelName + " (" + ManagerUtils.AreaToText(areaM2) + " м²)"
                });
            }
        }

        private static string GetAreaName(Area area)
        {
            string name = area.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString();
            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();

            string number = area.Number;
            if (!string.IsNullOrWhiteSpace(number)) return "Зона " + number.Trim();

            return "Зона " + ManagerUtils.IdValue(area.Id);
        }

        private void BuildRows()
        {
            var levels = new FilteredElementCollector(_context.Doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ThenBy(l => l.Name, StringComparer.CurrentCulture)
                .ToList();

            var roomsByLevel = _context.Rooms
                .Where(r => r.Level != null)
                .GroupBy(r => ManagerUtils.IdValue(r.Level.Id))
                .ToDictionary(g => g.Key, g => g.ToList());

            _suppressSelectionSave = true;
            var rows = new List<FloorLevelAreaRow>();
            foreach (Level level in levels)
            {
                long levelId = ManagerUtils.IdValue(level.Id);
                roomsByLevel.TryGetValue(levelId, out List<Room> roomsOnLevel);

                var row = new FloorLevelAreaRow(level, roomsOnLevel?.Count ?? 0);
                row.SetRoomFloorArea(roomsOnLevel ?? new List<Room>());
                row.SelectedZone = ResolveSavedZone(levelId);
                row.ZoneSelectionChanged += Row_ZoneSelectionChanged;
                rows.Add(row);
            }
            _suppressSelectionSave = false;

            _rows = new ObservableCollection<FloorLevelAreaRow>(rows);
            LvLevels.ItemsSource = _rows;
        }

        private FloorZoneOption ResolveSavedZone(long levelId)
        {
            FloorLevelAreaEntry entry = FloorLevelAreaStore.FindEntry(_data, levelId);
            if (entry == null || entry.ZoneId == 0) return FloorZoneOption.None;

            FloorZoneOption option = ZoneOptions.FirstOrDefault(z => z.ZoneId == entry.ZoneId);
            return option ?? FloorZoneOption.None;
        }

        private void Row_ZoneSelectionChanged(object sender, EventArgs e)
        {
            if (_suppressSelectionSave || !(sender is FloorLevelAreaRow row)) return;
            PersistRow(row);
            FloorLevelAreaStore.Save(_data);
            UpdateSummary();
        }

        private void PersistRow(FloorLevelAreaRow row)
        {
            if (!row.HasZone && !row.TryGetFloorArea(out _))
            {
                FloorLevelAreaStore.Remove(_data, row.LevelId);
            }
            else
            {
                long zoneId = row.HasZone ? row.SelectedZone.ZoneId : 0;
                double floorArea = row.TryGetFloorArea(out double value) ? value : 0;
                FloorLevelAreaStore.Upsert(_data, row.LevelId, row.LevelName, zoneId, floorArea);
            }
        }

        private void UpdateSummary()
        {
            if (!_parameterReady || _rows == null) return;

            int withZone = _rows.Count(r => r.HasZone);
            int filled = _rows.Count(r => r.HasFloorArea);
            int errors = _rows.Count(r => r.HasError);
            int zones = ZoneOptions.Count - 1;

            string errorPart = errors == 0
                ? "расхождений нет"
                : "уровней с предупреждениями: " + errors;

            TbSummary.Text = "Уровней: " + _rows.Count
                             + "; зон: " + zones
                             + "; привязано: " + withZone
                             + "; заполнено: " + filled
                             + "; " + errorPart + ".";
        }

        private void Levels_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (!_parameterReady) return;
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (!(e.Row.Item is FloorLevelAreaRow row)) return;

            var textBox = e.EditingElement as TextBox;
            if (textBox != null)
                row.FloorAreaText = textBox.Text?.Trim() ?? "";

            string text = row.FloorAreaText.Trim();
            if (text.Length > 0 && !ManagerUtils.TryParseNumber(text, out _))
            {
                e.Cancel = true;
                new InfoWindow280("Некорректное числовое значение площади этажа.").ShowDialog();
                return;
            }

            if (row.TryGetFloorArea(out double value))
                row.FloorAreaText = ManagerUtils.AreaToText(value);

            PersistRow(row);
            FloorLevelAreaStore.Save(_data);
            UpdateSummary();
        }

        private void UpdateFromZones_Click(object sender, RoutedEventArgs e)
        {
            if (!_parameterReady) return;

            List<FloorLevelAreaRow> targets = _rows.Where(r => r.HasZone).ToList();
            if (targets.Count == 0)
            {
                new InfoWindow280("Ни для одного уровня не выбрана зона — обновлять нечего.").ShowDialog();
                return;
            }

            int updated = 0;
            foreach (FloorLevelAreaRow row in targets)
            {
                if (_zonesById.TryGetValue(row.SelectedZone.ZoneId, out Area area))
                    row.RefreshZoneArea(ManagerUtils.InternalToSqMeters(area.Area));

                row.ApplyZoneArea();
                PersistRow(row);
                updated++;
            }

            FloorLevelAreaStore.Save(_data);
            UpdateSummary();
            Logger.Log("Обновлено площадей этажа по зонам: " + updated, 1);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!_parameterReady) return;

            // Зафиксировать незавершённое редактирование ячейки.
            LvLevels.CommitEdit(DataGridEditingUnit.Cell, true);
            LvLevels.CommitEdit(DataGridEditingUnit.Row, true);

            List<FloorLevelAreaRow> targets = _rows.Where(r => r.HasFloorArea).ToList();
            if (targets.Count == 0)
            {
                new InfoWindow280("Нет заполненных значений «" + RoomParams.FloorAreaTitle
                                  + "» — записывать нечего.").ShowDialog();
                return;
            }

            int changedRooms = 0;
            int skippedLevels = 0;

            using (var transaction = new Transaction(_context.Doc))
            {
                try
                {
                    transaction.Start("TNov - N_Площадь этажа");
                    Logger.Log("Запись «" + RoomParams.FloorAreaTitle + "», уровней: " + targets.Count, 1);

                    foreach (FloorLevelAreaRow row in targets)
                    {
                        if (!row.TryGetFloorArea(out double areaM2))
                        {
                            skippedLevels++;
                            continue;
                        }

                        List<Room> roomsOnLevel = _context.Rooms
                            .Where(r => r.Level != null && ManagerUtils.IdValue(r.Level.Id) == row.LevelId)
                            .ToList();

                        if (roomsOnLevel.Count == 0)
                        {
                            skippedLevels++;
                            continue;
                        }

                        double internalArea = ManagerUtils.SqMetersToInternal(areaM2);
                        foreach (Room room in roomsOnLevel)
                        {
                            Parameter param = room.get_Parameter(RoomParams.FloorArea);
                            if (param == null || param.IsReadOnly) continue;
                            param.Set(internalArea);
                            changedRooms++;
                        }

                        row.ApplyWrittenArea(areaM2);
                        PersistRow(row);
                    }

                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка записи площади этажа: " + ex.Message, 4);
                    new InfoWindow280("Ошибка при записи параметра «" + RoomParams.FloorAreaTitle + "»:\n" + ex.Message)
                        .ShowDialog();
                    return;
                }
            }

            FloorLevelAreaStore.Save(_data);
            Logger.Log("Записано помещений: " + changedRooms + ", уровней пропущено: " + skippedLevels, 1);

            _window.DialogResult = _context.StagesResolved;
            _window.Close();
        }
    }
}
