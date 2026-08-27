using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>
    /// Общее состояние менеджера помещений: документ, настройки, помещения и список бэкапов.
    /// Создаётся один раз в команде и передаётся во все вкладки.
    /// </summary>
    public class RoomsManagerContext
    {
        /// <summary>
        /// Ключ файла настроек. Совпадает с ключом Округлятора/Офисографии, чтобы
        /// списки имён для коэффициентов оставались общими для всех функций.
        /// </summary>
        private const string SettingsKey = "Офисография";

        public RoomsManagerContext(UIDocument uiDoc, TNovConfig config, string docName, ExternalCommandData commandData)
        {
            UiDoc = uiDoc;
            Doc = uiDoc.Document;
            Config = config;
            DocName = docName;
            CommandData = commandData;
            Settings = new RoomsViewModel();
            LivingRoomGuid = ApartParams.GetLivingRoomGuid(commandData);
        }

        public UIDocument UiDoc { get; }
        public Document Doc { get; }
        public TNovConfig Config { get; }
        public string DocName { get; }
        public ExternalCommandData CommandData { get; }

        /// <summary>GUID параметра «N_Кв.Комната.Жилая» зависит от версии шаблона проекта.</summary>
        public Guid LivingRoomGuid { get; }

        /// <summary>Номер первой квартиры для сквозной нумерации. Живёт в пределах запуска плагина.</summary>
        public string ApartFirstNumber { get; set; } = "1";

        /// <summary>Первый номер для перенумерации кладовых.</summary>
        public string StorageFirstNumber { get; set; } = "1";

        public RoomsViewModel Settings { get; private set; }
        public List<Room> Rooms { get; private set; } = new List<Room>();
        public List<RoomAreaBackup> Backups { get; private set; } = new List<RoomAreaBackup>();

        public RoomAreaBackup LatestBackup => Backups.Count > 0 ? Backups[0] : null;

        public bool UnplacedResolved { get; set; }
        public bool DepartmentsResolved { get; set; }
        public bool StagesResolved => UnplacedResolved && DepartmentsResolved;

        public RoomRoundingService CreateRoundingService()
        {
            return new RoomRoundingService(Settings.k05, Settings.k03);
        }

        public ApartCalculationService CreateApartService()
        {
            return new ApartCalculationService(CreateRoundingService(), LivingRoomGuid);
        }

        public OfficeCalculationService CreateOfficeService()
        {
            return new OfficeCalculationService(Settings.names1, Settings.names2);
        }

        /// <summary>Помещения с включённым параметром N_Квартира.</summary>
        public List<Room> GetApartmentRooms()
        {
            return Rooms.Where(ApartParams.IsApartmentRoom).ToList();
        }

        /// <summary>Помещения с заполненным параметром N_Офис.Номер.</summary>
        public List<Room> GetOfficeRooms()
        {
            return Rooms.Where(OfficeParams.IsOfficeRoom).ToList();
        }

        /// <summary>Кладовые: имя «Кладовая», вне квартир и офисов.</summary>
        public List<Room> GetStorageRooms()
        {
            return Rooms.Where(RoomParams.IsStorageRoom).ToList();
        }

        /// <summary>
        /// Порядок кладовых, заданный перетаскиванием. Живёт в пределах запуска плагина,
        /// чтобы не сбрасываться при обновлении списка и при выходе в ручной нумератор.
        /// </summary>
        public List<long> StorageOrder { get; set; } = new List<long>();

        public void ReloadRooms()
        {
            Rooms = new FilteredElementCollector(Doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .ToList();
            Logger.Log("Собрано помещений: " + Rooms.Count, 1);
        }

        public List<Room> GetUnplacedRooms()
        {
            return Rooms.Where(RoomParams.IsUnplaced).ToList();
        }

        public List<Room> GetRoomsWithoutDepartment()
        {
            return Rooms.Where(r => !RoomParams.HasDepartment(r)).ToList();
        }

        /// <summary>Все значения «Назначение», которые уже встречаются в модели.</summary>
        public List<string> GetDepartmentValues()
        {
            return Rooms.Select(RoomParams.GetDepartment)
                        .Where(v => v.Length > 0)
                        .Distinct()
                        .OrderBy(v => v, StringComparer.CurrentCulture)
                        .ToList();
        }

        /// <summary>
        /// Список бэкапов читается заранее, а содержимое последнего сразу подгружается —
        /// сверка площадей в списке помещений не должна ждать сетевого файла.
        /// </summary>
        public void ReloadBackups()
        {
            Backups = RoomAreaBackupStore.LoadList(Config, DocName);
            RoomAreaBackup latest = LatestBackup;
            if (latest != null)
            {
                latest.Load();
                Logger.Log("Последний бэкап: " + latest.DisplayName + ", записей: " + latest.EntryCount, 1);
            }
            Logger.Log("Найдено бэкапов площадей: " + Backups.Count, 1);
        }

        public void LoadSettings()
        {
            new json(SettingsKey, true, out bool exist, out string jsonPath);
            _settingsPath = jsonPath;
            if (!exist) return;

            try
            {
                var loaded = JsonConvert.DeserializeObject<RoomsViewModel>(File.ReadAllText(jsonPath));
                if (loaded != null) Settings = loaded;
                Logger.Log("Настройки загружены: " + jsonPath, 1);
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка при чтении настроек: " + ex.Message, 4);
            }
        }

        public void SaveSettings()
        {
            if (string.IsNullOrEmpty(_settingsPath)) return;
            try
            {
                File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(Settings));
                Logger.Log("Настройки сохранены", 1);
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка при сохранении настроек: " + ex.Message, 4);
            }
        }

        private string _settingsPath;
    }
}
