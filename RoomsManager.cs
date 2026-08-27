using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.IO;
using TNovCommon;
using TNovRooms.Manager;

namespace TNovRooms
{
    /// <summary>
    /// Общий интерфейс для функций по помещениям. Перед доступом к функциям
    /// прогоняет обязательные проверки: неразмещённые помещения и параметр «Назначение».
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RoomsManager : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Менеджер помещений";
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            string docName = doc.Title.ToString(); docName = docName.Replace(",", " ");
            string userName = rvtApp.Username; userName = userName.Replace(",", "");
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
            docName = docName.Replace(",", "");
            #endregion

            TNovConfig config = TNovConfigLoad.LoadConfig(DBCommandName, TNovVersion);
            if (config == null) return Result.Failed;

            #region Настройки логов
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

            try
            {
                string jsonpath0 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "TNovClient/TNovSettings.json");
                var viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath0));
                if (viewModel0 != null && viewModel0.extendedLogs)
                {
                    var qViewModel = new QuestionWindowViewModel
                    {
                        headtxt = "Включены расширенные логи. " +
                                  "Плагин будет работать медленнее, но соберет больше данных. " +
                                  "Выключить расширенные логи для ускорения работы?"
                    };
                    var qwpfview = new QuestionWindow280(qViewModel);
                    qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                    bool? qok = qwpfview.ShowDialog();
                    if (qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
                }
            }
            catch (Exception ex) { Logger.Log("Не удалось прочитать TNovSettings.json: " + ex.Message, 4); }
            #endregion

            var context = new RoomsManagerContext(uidoc, config, docName, commandData);
            context.LoadSettings();
            context.ReloadRooms();

            if (context.Rooms.Count == 0)
            {
                new InfoWindow280("В модели нет помещений. Завершение работы.").ShowDialog();
                Logger.Log("Помещений в модели нет. Завершение работы.", 3);
                return Result.Cancelled;
            }

            context.ReloadBackups();
            context.UnplacedResolved = context.GetUnplacedRooms().Count == 0;
            context.DepartmentsResolved = context.GetRoomsWithoutDepartment().Count == 0;
            Logger.Log("Проверки на старте: неразмещённые пройдены — " + context.UnplacedResolved
                       + ", «Назначение» заполнено — " + context.DepartmentsResolved, 1);

            ShowManager(context);

            context.SaveSettings();

            if (!context.StagesResolved)
            {
                Logger.Log("Обязательные проверки не пройдены. Завершение работы.", 3);
                return Result.Cancelled;
            }

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }

        /// <summary>
        /// Ручные нумераторы выбирают помещения в модели, поэтому менеджер на это время
        /// закрывается, а после нумерации открывается снова на своей вкладке.
        /// </summary>
        private static void ShowManager(RoomsManagerContext context)
        {
            ManagerTab tab = ManagerTab.Rounding;

            while (true)
            {
                var window = new RoomsManagerWindow(context, tab);
                window.ShowDialog();

                switch (window.ManualNumberingRequest)
                {
                    case ManualNumberingKind.ApartAtLevel:
                        RunManualApartNumbering();
                        tab = ManagerTab.Aparts;
                        break;

                    case ManualNumberingKind.RoomNumber:
                        RunManualRoomNumbering();
                        tab = ManagerTab.Storages;
                        break;

                    default:
                        return;
                }

                context.ReloadRooms();
            }
        }

        /// <summary>
        /// Существующее окно ручного нумератора. Его галочка перезаполнения сквозных номеров
        /// скрыта: в менеджере это отдельная кнопка со своим полем первого номера.
        /// </summary>
        private static void RunManualApartNumbering()
        {
            Logger.Log("Открываем ручной нумератор квартир", 1);

            var viewModel = new ApartsNumAtLevelViewModel { recalcnums = false };
            var view = new ApartsNumAtLevelWPF(viewModel);
            view.checkBox1.Visibility = System.Windows.Visibility.Collapsed;
            viewModel.CloseRequest += (s, e) => view.Close();
            viewModel.HideRequest += (s, e) => view.Hide();
            viewModel.ShowRequest += (s, e) => view.ShowDialog();
            view.ShowDialog();

            Logger.Log("Ручной нумератор закрыт", 1);
        }

        /// <summary>Существующее окно ручного нумератора помещений: заполняет системный «Номер».</summary>
        private static void RunManualRoomNumbering()
        {
            Logger.Log("Открываем ручной нумератор помещений", 1);

            var viewModel = new RoomsNumViewModel();
            var view = new RoomsNumWPF(viewModel);
            viewModel.CloseRequest += (s, e) => view.Close();
            viewModel.HideRequest += (s, e) => view.Hide();
            viewModel.ShowRequest += (s, e) => view.ShowDialog();
            view.ShowDialog();

            Logger.Log("Ручной нумератор закрыт", 1);
        }
    }
}
