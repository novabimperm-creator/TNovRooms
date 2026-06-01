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
using System.Windows.Input;
using Autodesk.Revit.UI.Selection;
using TNovCommon;

namespace TNovRooms
{
    


    [Transaction(TransactionMode.Manual)]
    public class Offices : IExternalCommand
    {
        private TNovProgressBar officesProgressBar;
        private void ThreadStartingPoint()
        {
            this.officesProgressBar = new TNovProgressBar();
            this.officesProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Офисография";
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
            Guid NRoomSqParamGuid = new Guid("4f890165-ec27-4a22-811a-07e010101ec5"); //N_Площадь.Округленная
            Guid NRoomSqKParamGuid = new Guid("e6b18cda-4550-4531-afae-96a9035f7fca"); //N_Площадь.ОкруглСКоэффициентом
            Guid NRoomOfficeNumber = new Guid("e73bb005-9ad8-489c-bc1f-fd8c3b521ec3"); //N_Офис.Номер
            Guid NRoomOfficeSqO = new Guid("835dbef4-b314-4a24-9c12-814abcf6b66f"); //N_Офис.Площадь.Общая
            Guid NRoomOfficeSqP = new Guid("8afe9673-011e-49d5-a8a4-57fc14cc3b1d");// //N_Офис.Площадь.Полезная
            Guid NRoomOfficeSqR = new Guid("72d42023-d485-49e3-8b7d-2ddda6791f28");// //N_Офис.Площадь.Расчетная
            #endregion

            #region Сбор элементов
            Logger.Log("Сбор элементов",1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            List<Room> orooms = new List<Room>();

            Logger.Log("Ищем неразмещенные помещения",1);
            int ec = 0; //счетчик неразмещенных помещений

            foreach (Room room in rooms) //проверка наличия неразмещенных помещений
            {
                if (room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() == 0) ec++; 
            }

            if (ec > 0) //если есть неразмещенные помещения - прерываем процесс
            {
                new InfoWindow280("В проекте присутствуют неразмещенные или избыточные помещения в количестве " +
                    ec + " шт. Удалите их плагином или через спецификацию.").ShowDialog();
                Logger.Log("В проекте присутствуют неразмещенные или избыточные помещения в количестве " +ec + " шт. Завершение работы",3);
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/ofisografiya/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                return Result.Failed;
            }

            Logger.Log("Ищем офисы",1);
            int officescount = 0; //счетчик количества помещений с заполненным параметром N_Офис.Номер
            
            foreach (Room room in rooms) //проверка наличия офисов
            {
                Parameter offnumParam = room.get_Parameter(NRoomOfficeNumber);
                if (offnumParam!=null&&offnumParam.HasValue)
                {
                    string offNumValue = offnumParam.AsString();
                    bool isOffice = Double.TryParse(offNumValue, out double num);
                    if (isOffice || offnumParam.AsString().Length > 0) { officescount++; orooms.Add(room); }
                }  
            }
            
            if (officescount == 0) //если нет офисов - прерываем процесс
            {
                new InfoWindow280("В проекте отсутствуют помещения с включенным параметром N_Офис.Номер. Заполните его в спецификации.").ShowDialog();
                Logger.Log("Офисы отсутствуют. Завершение работы.", 3);
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/ofisografiya/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                return Result.Failed;
            }
            #endregion

            #region Диалог

            Logger.Log("Диалоговое окно",1);
            var viewModel = new RoomsViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in DBCommandName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<RoomsViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new RoomsWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            string names1 = viewModel.names1; string names2 = viewModel.names2;
            string names3 = viewModel.k05; string names4 = viewModel.k03; bool recalc = viewModel.recalc;

            //получаем имена помещений, удаляем возможные пробелы в начале и конце имен
            string[] n1 = names1.Split(','); for (int i = 0; i < n1.Length; i++) { n1[i] = n1[i].Trim(); }
            string[] n2 = names2.Split(','); for (int i = 0; i < n2.Length; i++) { n2[i] = n2[i].Trim(); }
            string[] n3 = names3.Split(','); for (int i = 0; i < n3.Length; i++) { n3[i] = n3[i].Trim(); }
            string[] n4 = names4.Split(','); for (int i = 0; i < n4.Length; i++) { n4[i] = n4[i].Trim(); }

            #endregion

            #region Итоговый список
            //Выбор элементов

            List<Element> rooms1 = new List<Element>();

            Logger.Log("Финальный список помещений", 1);

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
                    Logger.Log("Ошибка: " + e.Message, 4);
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

            //обработка сценария "одно помещение + его офис"
            if (viewModel.selection == 2)
            {
                Logger.Log("Обработка выделенного помещения", 1);
                List<Room> newORooms = new List<Room>();
                foreach (Room room in rooms1) //проверка что помещение принадлежит офису
                {
                    bool isOfficeRoom = false;

                    Parameter offnumParam = room.get_Parameter(NRoomOfficeNumber);
                    if (offnumParam != null && offnumParam.HasValue)
                    {
                        string offNumValue = offnumParam.AsString();
                        bool isOffice = Double.TryParse(offNumValue, out double num);
                        if (isOffice || offnumParam.AsString().Length > 0) isOfficeRoom = true;
                    }

                    if (isOfficeRoom)
                    {
                        string officeNum = room.get_Parameter(NRoomOfficeNumber).AsValueString();
                        foreach (var oroom in orooms)
                        {
                            string officeNum1 = oroom.get_Parameter(NRoomOfficeNumber).AsValueString();
                            if (officeNum1 == officeNum)
                            {
                                newORooms.Add(oroom);
                                Logger.Log("   Помещение " + oroom.Id + " добавлено в список на обработку", 2);
                            }
                        }
                        orooms = newORooms;
                    }
                    else
                    {
                        Logger.Log("Помещение - не офисное. Завершение работы.", 3);
                        return Result.Succeeded; //выбранное помещение оказалось не офисным
                    }
                }
            }
            #endregion

            bool unhandledError = false;

            #region Округлятор
            //Округлятор (только офисы)
            if (recalc) //если активна галочка Перерасчета - запускаем транзакцию
            {
                using (Transaction transaction = new Transaction(doc))
                {
                    try
                    {
                        transaction.Start("TNov - Округлятор");
                        Logger.Log("Открываем транзакцию 1 (округлятор)",1);
                        foreach (Room room in orooms) 
                        {
                            double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() * 0.3048 * 0.3048;
                            double areaR = Math.Round(area, 1);
                            string name = room.Name;
                            double k = 1;
                            foreach (string n in n3) { if (name.Contains(n)) { k = 0.5; } }
                            foreach (string n in n4) { if (name.Contains(n)) { k = 0.3; } }
                            double areaRK = Math.Round((areaR * k + 0.000001), 1);
                            room.get_Parameter(NRoomSqParamGuid)?.Set(areaR);
                            room.get_Parameter(NRoomSqKParamGuid)?.Set(areaRK);
                            Logger.Log("   Помещение " + room.Id + " : успешно",2);
                        }

                        transaction.Commit(); Logger.Log("Закрываем транзакцию 1", 1);
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

            #region Основной код
            //Офисография

            var oroomssortbynum = from oroom in orooms //сортированный список помещений по номеру офиса
                              orderby oroom.get_Parameter(NRoomOfficeNumber).AsValueString()
                                select oroom;

            var offices = from oroom in oroomssortbynum //список офисов
                         group oroom by oroom.get_Parameter(NRoomOfficeNumber).AsValueString();

            int officesCount = offices.Count();

            using (Transaction transaction2 = new Transaction(doc))
            {
                try
                {
                    transaction2.Start("TNov - Офисография");
                Logger.Log("Открываем транзакцию 2 (офисография)",1);

                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.officesProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.officesProgressBar.value.Text = PBCount.ToString()));
                this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.officesProgressBar.TNov_ProgressBar.Maximum = (double)officesCount));
                this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.officesProgressBar.maxvalue.Text = officesCount.ToString()));

                foreach (var office in offices) //проходим по каждому офису в списке офисов
                {
                    Logger.Log("Офис "+office.First().get_Parameter(NRoomOfficeNumber).AsValueString(),2);
                    
                    double offsqo = 0; //объявляем переменную для заполнения значения параметра N_Офис.Площадь.Общая
                    double offsqp = 0; //N_Офис.Площадь.Полезная
                    double offsqr = 0; //N_Офис.Площадь.Расчетная
                    foreach (var oroom in office) //проходим по каждой комнате в офисе
                    {
                        double sqNonConvert = oroom.get_Parameter(NRoomSqParamGuid).AsDouble();
                        double sq = oroom.get_Parameter(NRoomSqParamGuid).AsDouble() / 0.3048 / 0.3048; //объявляем переменную, получаем площадь каждого помещения в офисе
                        Logger.Log("   Помещение " + oroom.Id.ToString()+" имя: "+oroom.Name+" площадь:"+ sqNonConvert.ToString(), 2);
                        
                        offsqo += sq; //добавляем значение площади помещения к общей площади офиса

                        double sqp = sq; double sqr = sq; string name = oroom.Name;

                        foreach (string n in n1) { if (name.Contains(n)) { sqp = 0; sqr = 0; break; } }
                        offsqp += sqp; //полезная
 
                        foreach (string n in n2) { if (name.Contains(n)) { sqr = 0; break; } }
                        offsqr += sqr; //расчетная
                    }
                    //Общая площадь
                    foreach (var oroom in office) //проходим по каждой комнате в офисе
                    {
                        try
                        {
                            oroom.get_Parameter(NRoomOfficeSqO).Set(offsqo); //назначаем параметр каждому помещению в офисе
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + oroom.Id.ToString() + " Параметр N_Офис.Площадь.Общая ошибка: " + ex.Message,4);
                        }
                    }
                    //Полезная площадь
                    foreach (var oroom in office) 
                    {
                        try
                        {
                            oroom.get_Parameter(NRoomOfficeSqP).Set(offsqp); 
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + oroom.Id.ToString() + " Параметр N_Офис.Площадь.Полезная ошибка: " + ex.Message, 4);
                        }
                    }
                    //Расчетная площадь
                    foreach (var oroom in office)
                    {
                        try
                        {
                            oroom.get_Parameter(NRoomOfficeSqR).Set(offsqr);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + oroom.Id.ToString() + " Параметр N_Офис.Площадь.Расчетная ошибка: " + ex.Message, 4);
                        }
                    }
                    //Прогресс-бар: +1
                    PBCount++;
                    this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.officesProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.officesProgressBar.value.Text = "Офисы " + PBCount.ToString()));

                }

                transaction2.Commit();
                
                Logger.Log("Закрываем транзакцию 2",1);
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
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
        private void CloseProgressBarSafely()
        {
            if (officesProgressBar != null &&
                officesProgressBar.Dispatcher != null &&
                !officesProgressBar.Dispatcher.HasShutdownStarted)
            {
                officesProgressBar.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (officesProgressBar.IsLoaded)
                        officesProgressBar.Close();
                    // Завершаем цикл сообщений диспетчера, чтобы поток завершился
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }));
            }
        }
    }
}
