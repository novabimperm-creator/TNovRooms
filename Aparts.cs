using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;

using System.Linq;
using Autodesk.Revit.DB.Architecture;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Threading;
using System.Threading;
using Autodesk.Revit.UI.Selection;
using TNovCommon;
using TNovRooms.Manager;
using System.Xml.Linq;

namespace TNovRooms
{
    


    [Transaction(TransactionMode.Manual)]
    public class Aparts : IExternalCommand
    {
        private TNovProgressBar apartsProgressBar;
        private void ThreadStartingPoint()
        {
            this.apartsProgressBar = new TNovProgressBar();
            this.apartsProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Квартирография";
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

            #region Настройки логов
            // создание log - файла
            Logger.Initialize(DBCommandName, dateTime, TNovVersion);

            var viewModel0 = new AppVersionViewModel();

            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath0));
            if (viewModel0.extendedLogs)

            {
                var qViewModel = new QuestionWindowViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new QuestionWindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
            }
            #endregion


            #region Параметры
            //GUID параметров и формулы расчёта лежат в TNovRooms.Manager:
            //те же сервисы использует Менеджер помещений
            Guid NRoomApartLivingParamGuid = ApartParams.GetLivingRoomGuid(commandData); //N_Кв.Комната.Жилая
            #endregion

            #region Сбор элементов
            Logger.Log( "Сбор элементов",1);

            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            List<Room> arooms = new List<Room>();

            Logger.Log( "Ищем неразмещенные помещения",1);
            int ec = 0; //счетчик неразмещенных помещений

            foreach (Room room in rooms) //проверка наличия неразмещенных помещений
            {
                if (room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() == 0) ec++; 
            }

            if (ec > 0) //если есть неразмещенные помещения - прерываем процесс
            {
                new InfoWindow280("В проекте присутствуют неразмещенные или избыточные помещения в количестве " + 
                    ec + " шт. Удалите их плагином или через спецификацию.").ShowDialog();
                Logger.Log("В проекте присутствуют неразмещенные или избыточные помещения в количестве " + ec + " шт. Завершение работы", 3);
                return Result.Failed;
            }

            Logger.Log( "Ищем квартиры",1);

            foreach (Room room in rooms) //проверка наличия квартир
            {
                if (ApartParams.IsApartmentRoom(room)) arooms.Add(room);
            }

            if (arooms==null || arooms.Count==0) //если нет квартир - прерываем процесс
            {
                new InfoWindow280("В проекте отсутствуют помещения с включенным параметром N_Квартира. Заполните его в спецификации.").ShowDialog();
                Logger.Log( "Квартиры отсутствуют. Завершение работы.",3);
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/kvartirografiya/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                return Result.Failed;
            }
            #endregion

            #region Диалог
            Logger.Log( "Диалоговое окно",1);
            var viewModel = new RoomsViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Офисография", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<RoomsViewModel>(File.ReadAllText(jsonpath));
                Logger.Log( "Десериализация прошла успешно",1);
            }
            var wpfview = new RoomsWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log( "Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log( "Ошибка при сериализации: " + ex.Message,4); }

            #endregion

            #region Финальный сбор элементов

            bool recalc = viewModel.recalc;

            RoomRoundingService rounding = new RoomRoundingService(viewModel.k05, viewModel.k03);
            ApartCalculationService apartService = new ApartCalculationService(rounding, NRoomApartLivingParamGuid);

            List<Element> rooms1 = new List<Element>();

            //Выбор элементов

            Logger.Log( "Финальный список помещений",1);

            if (viewModel.selection == 2)
            {
                Selection elemselection = uidoc.Selection;


                ISelectionFilter _filter = new RoomSelectionFilter();
                try
                {
                    Reference reference = RevitAPI.UiDocument.Selection.PickObject(ObjectType.Element, _filter, $"Выберите помещение");
                    rooms1.Add(doc.GetElement(reference));
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException e)
                {
                    Logger.Log( "Ошибка: " + e.Message,4);
                    return Result.Cancelled;
                }

            }
            else
            {
                foreach (var r in rooms)
                {
                    rooms1.Add(r); //Коллекция всех помещений
                }
            }

            //обработка сценария "одно помещение + его квартира"
            if (viewModel.selection == 2)
            {
                List<Room> newARooms = new List<Room>();
                foreach (Room room in rooms1) //проверка что помещение принадлежит квартире
                {
                    if (ApartParams.IsApartmentRoom(room))
                    {
                        string apartNum = ApartParams.GetNumber(room);
                        foreach (var aroom in arooms)
                        {
                            if (ApartParams.GetNumber(aroom) == apartNum) newARooms.Add(aroom);
                        }
                        arooms = newARooms;
                    }
                    else 
                    {
                        Logger.Log("Выбранное помещение - не квартирное. Завершение работы", 3);
                        return Result.Succeeded; //выбранное помещение оказалось не квартирным
                    }
                }
            }

            #endregion

            bool unhandledError = false;

            #region Округлятор
            //Округлятор (только квартиры)
            if (recalc) //если активна галочка Перерасчета - запускаем транзакцию
            {
                using (Transaction transaction = new Transaction(doc))
                {
                    try { 
                    transaction.Start("TNov - Округлятор");
                    Logger.Log( "Открываем транзакцию 1 (округлятор)",1);
                    foreach (Room room in arooms) 
                    {
                        double areaR = RoomRoundingService.RoundArea(RoomParams.GetAreaM2(room));
                        double areaRK = RoomRoundingService.RoundAreaWithCoefficient(areaR, rounding.GetCoefficient(room));
                        room.get_Parameter(RoomParams.RoundedArea)?.Set(areaR);
                        room.get_Parameter(RoomParams.RoundedAreaK)?.Set(areaRK);
                        Logger.Log( "   Помещение " + room.Id + " : успешно",2);
                    }

                    transaction.Commit(); Logger.Log( "Закрываем транзакцию 1",1);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log("Ошибка: " + ex.Message, 4);
                        new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                        unhandledError = true;
                    }
                }
            }
            #endregion

            #region Проверка нумерации
            //Проверка заполненности сквозных номеров квартир
            Logger.Log( "Квартирография. Проверяем заполненность Кв.Номер",1);

            
            foreach (Room aroom in arooms)
            {
                string apart = ApartParams.GetNumber(aroom);
                if (apart == "") //если у некоторых помещений квартир не заполнен параметр N_Кв.Номер - прерываем процесс
                {
                    new InfoWindow280("В проекте присутствуют помещения квартир с незаполненным параметром N_Кв.Номер. Запустите Нумератор квартир.").ShowDialog();
                    Logger.Log("Не у всех помещений с галочкой Квартира заполнен параметр Кв.Номер. Завершение работы.", 3);
                    string commandText = @"https://portal.talan.group/knowledge/proektirovanie/kvartirografiya/";
                    var proc = new System.Diagnostics.Process();
                    proc.StartInfo.FileName = commandText;
                    proc.StartInfo.UseShellExecute = true;
                    proc.Start();
                    return Result.Failed;
                }
            }
            #endregion

            #region Основной код
            //Квартирография. Группировка и расчёт - в общем сервисе
            List<RoomGroup> aparts = ApartCalculationService.Group(arooms);

            int apartsCount = aparts.Count;

            using (Transaction transaction2 = new Transaction(doc))
            {
                try { 
                transaction2.Start("TNov - Квартирография");
                Logger.Log( "Открываем транзакцию 2 (квартирография)",1);

                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.value.Text = PBCount.ToString()));
                this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Maximum = (double)apartsCount));
                this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.maxvalue.Text = apartsCount.ToString()));

                foreach (RoomGroup apart in aparts) //проходим по каждой квартире в списке квартир
                {
                    Logger.Log("Квартира " + apart.Number, 2);

                    foreach (Room aroom in apart.Rooms)
                    {
                        Logger.Log("   Помещение " + aroom.Id.ToString() + " имя: " + aroom.Name
                                   + " площадь:" + RoomParams.GetRoundedArea(aroom).ToString(), 2);
                    }

                    //расчёт и запись всех параметров квартиры, включая Поквартир.Сетка
                    apartService.Apply(apart.Rooms, apartService.Calculate(apart.Rooms));

                    //Прогресс-бар: +1
                    PBCount++;
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.value.Text = "Квартиры " + PBCount.ToString()));

                }
                

                transaction2.Commit(); Logger.Log("Закрываем транзакцию 2", 1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    unhandledError = true;
                }
                finally
                {
                    CloseProgressBarSafely();
                }
                
            }
            #endregion
            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибками.", 4);
                return Result.Succeeded;
            }
            Logger.Log( "Завершение работы.",5);
            return Result.Succeeded;
        }
        public static bool OldTemplateProject(ExternalCommandData commandData) //устаревший класс, используется локально в некоторых функциях
        {
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            ProjectInfo projectInfo = doc.ProjectInformation;
            Autodesk.Revit.DB.Parameter template = projectInfo.LookupParameter("N_Орг.ВерсияШаблона");
            string templateversion = "v";
            if (template == null) return true;
            else { templateversion = template.AsValueString(); }
            templateversion = templateversion.Replace(" (Talan)", "");
            templateversion = templateversion.Replace("(Talan)", "");
            templateversion = templateversion.Replace(" (UDS)", "");
            templateversion = templateversion.Replace("(UDS)", "");
            if (templateversion.Contains("v")) return true;
            else
            {
                string[] versionparts = templateversion.Split('.');
                double versionMath = Convert.ToDouble(versionparts[0]) * 10 + Convert.ToDouble(versionparts[1]);
                if (versionMath < 20223) return true;
            }
            string docName = doc.Title.ToString(); //для разделов инженерных сетей - всегда "старый" шаблон
            if (docName.Contains("-ВК") || docName.Contains("_ВК")) return true;
            if (docName.Contains("-ОВ") || docName.Contains("_ОВ")) return true;
            if (docName.Contains("-ЭО") || docName.Contains("_ЭО")) return true;
            if (docName.Contains("-ЭЛ") || docName.Contains("_ЭЛ")) return true;
            if (docName.Contains("-ЭЭ") || docName.Contains("_ЭЭ")) return true;
            if (docName.Contains("-ЭС") || docName.Contains("_ЭС")) return true;
            if (docName.Contains("-СС") || docName.Contains("_СС")) return true;
            if (docName.Contains("-ССВ") || docName.Contains("_ССВ")) return true;
            if (docName.Contains("-АПС") || docName.Contains("_АПС")) return true;
            if (docName.Contains("Задани") || docName.Contains("задани") || docName.Contains("-ЗД") || docName.Contains("_ЗД") || docName.Contains("ЗАДАНИЕ")) return true;

            return false;
        }
        private void CloseProgressBarSafely()
        {
            if (apartsProgressBar != null &&
                apartsProgressBar.Dispatcher != null &&
                !apartsProgressBar.Dispatcher.HasShutdownStarted)
            {
                apartsProgressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (apartsProgressBar.IsLoaded)
                        apartsProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }
    }
}
