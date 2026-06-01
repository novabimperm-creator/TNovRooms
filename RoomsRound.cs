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

namespace TNovRooms
{
    

    [Transaction(TransactionMode.Manual)]
    public class RoomsRound : IExternalCommand
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
            string DBCommandName = "Округлятор";
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


            //параметры
            Guid NRoomSqParamGuid = new Guid("4f890165-ec27-4a22-811a-07e010101ec5"); //N_Площадь.Округленная
            Guid NRoomSqKParamGuid = new Guid("e6b18cda-4550-4531-afae-96a9035f7fca"); //N_Площадь.ОкруглСКоэффициентом

            #region Сбор элементов
            Logger.Log( "Сбор элементов",1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            #endregion
            #region Проверка неразмещенных
            Logger.Log( "Ищем неразмещенные помещения", 1);
            int ec = 0; //счетчик неразмещенных помещений

            foreach (Room room in rooms) //проверка наличия неразмещенных помещений
            {
                double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                if (area == 0) { ec++; }
            }

            if (ec > 0) //если есть неразмещенные помещения - прерываем процесс
            {
                new InfoWindow280("В проекте присутствуют неразмещенные или избыточные помещения в количестве " +
                   ec + " шт. Удалите их плагином или через спецификацию.").ShowDialog();
                Logger.Log("В проекте присутствуют неразмещенные или избыточные помещения в количестве " + ec + " шт. Завершение работы", 3);
                return Result.Failed;
            }
            #endregion
            #region Диалог
            Logger.Log("Диалоговое окно", 1);
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
            catch (Exception ex) { Logger.Log( "Ошибка при сериализации: " + ex.Message, 4); }

            string names1 = viewModel.k05; string names2 = viewModel.k03;

            //получаем имена помещений, удаляем возможные пробелы в начале и конце имен
            string[] n1 = names1.Split(','); for (int i = 0; i < n1.Length; i++) {n1[i] = n1[i].Trim(); }
            string[] n2 = names2.Split(','); for (int i = 0; i < n2.Length; i++) { n2[i] = n2[i].Trim(); }
            #endregion
            #region Список в работу
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
            #endregion

            bool unhandledError = false;
            int roomsCount =rooms1.Count();
            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                try { 
                    transaction.Start("TNov - Округлятор");
                    Logger.Log( "Открываем транзакцию",1);

                    Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.IsBackground = true;
                    thread.Start();
                    Thread.Sleep(100);

                    int PBCount = 0;
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.value.Text = PBCount.ToString()));
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Maximum = (double)roomsCount));
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.maxvalue.Text = roomsCount.ToString()));


                    foreach (Room room in rooms1) //проверка наличия неразмещенных помещений
                    {
                        PBCount++;
                        this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.value.Text = PBCount.ToString()));

                        Logger.Log( "Помещение "+room.Id.ToString(),2);

                        double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() * 0.3048 * 0.3048;
                        double areaR = Math.Round(area, 1); Logger.Log( "   площадь: " + areaR.ToString(),2);
                        string name = room.Name;
                        double k = 1;
                        foreach (string n in n1) { if (name.Contains(n)) { k = 0.5; } }
                        foreach (string n in n2) { if (name.Contains(n)) { k = 0.3; } }
                        double areaRK = Math.Round((areaR * k + 0.000001), 1); 
                        Logger.Log( "   площадь с коэфф: " + areaR.ToString(), 2);
                        room.get_Parameter(NRoomSqParamGuid)?.Set(areaR);
                        room.get_Parameter(NRoomSqKParamGuid)?.Set(areaRK);
                        Logger.Log( "   "+"параметры назначены успешно",2);
                    }

                    transaction.Commit();
                    Logger.Log( "Закрываем транзакцию",1);
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
