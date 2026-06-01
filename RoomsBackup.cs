using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using TNovCommon;
using System.Windows;
using System.Windows.Threading;
using System.Threading;

namespace TNovRooms
{
    public class TNovRoom
    {
        public string RoomCategory;
        public int RoomGroupNumber;
        public string RoomId;
        public string RoomName;
        public string RoomModelS;
        public string RoomModelSK;
        public string RoomBackupS;
        public string RoomBackupSK;
    }
    [Transaction(TransactionMode.Manual)]
    public class RoomsBackup : IExternalCommand
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
            string DBCommandName = "Помещения Резервные копии";
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
            Guid NRoomIsApartParamGuid = new Guid("155f8c55-e05f-4737-883e-1338eb722735"); //N_Квартира
            Guid NRoomApartNumberParamGuid = new Guid("2f2edd07-cd47-4e30-b091-c1ceb5e6ff63"); //N_Кв.Номер
            Guid NRoomOfficeNumber = new Guid("e73bb005-9ad8-489c-bc1f-fd8c3b521ec3"); //N_Офис.Номер
            Guid NLevelNumberParamGuid = new Guid("4d2aa1b8-727c-43a1-8b1e-8c22dd484e11"); //N_Эт.Номер
            BuiltInParameter roomNumber = BuiltInParameter.ROOM_NUMBER; //Номер
            #endregion
            #region Сбор элементов
            Logger.Log("Сбор элементов", 1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            Logger.Log("Ищем неразмещенные помещения", 1);
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
            // Диалоговое окно
            roomsBackupStart wpfview = new roomsBackupStart();
            bool? ok = wpfview.ShowDialog();
            if (wpfview.scenario>0) { } 
            else { Logger.Log("Выполнение отменено пользователем. Завершение работы.", 3); return Result.Cancelled; }
            #endregion

            string date = DateTime.Now.ToString(); date = date.Replace(":", "-");

            bool unhandledError = false;

            switch (wpfview.scenario)
            {
                case 1:
                    #region Сохранение

                    Logger.Log("Выбран сценарий Сохранить бэкап", 1);

                    roomsBackupSave wpfview1 = new roomsBackupSave();
                    bool? ok1 = wpfview1.ShowDialog();
                    if (ok1 != null && ok1 == true) { } 
                    else { Logger.Log("Выполнение отменено пользователем. Завершение работы.", 3); return Result.Cancelled; }

                    string backupName = "Без имени";
                    if (wpfview1.backupName != null && wpfview1.backupName.Length > 0 ) backupName = wpfview1.backupName;
                    Logger.Log("Имя бэкапа: "+wpfview1.backupName, 1);

                    Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.IsBackground = true;
                    thread.Start();
                    Thread.Sleep(100);

                    int PBCount = 0;
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.value.Text = PBCount.ToString()));
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Maximum = (double)rooms.Count));
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.maxvalue.Text = rooms.Count.ToString()));


                    try { 
                        foreach (var room in rooms)
                        {
                            Logger.Log("Помещение " +room.Id.IntegerValue.ToString(),2);
                            //тип помещения
                            string roomType = "Прочие";

                            int isApart = room.get_Parameter(NRoomIsApartParamGuid).AsInteger();
                            if (room.get_Parameter(NRoomIsApartParamGuid)?.AsInteger() == 1) roomType = "Квартиры";

                            bool office = false;
                            Parameter offnumParam = room.get_Parameter(NRoomOfficeNumber);
                            if (offnumParam != null && offnumParam.HasValue)
                            {
                                string offNumValue = offnumParam.AsString();
                                bool isOffice = Double.TryParse(offNumValue, out double num);
                                if (isOffice || offnumParam.AsString().Length > 0) { office = true; roomType = "Офисы"; }
                            }

                            if (room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString().Contains("Кладов")&& isApart!= 1&&office==false) 
                                roomType = "Кладовые";

                            Logger.Log("   тип: " + roomType, 2);

                            //площади
                            string roomSqStr = "0";
                            string roomSqRStr = "0";
                            roomSqStr = room.get_Parameter(NRoomSqParamGuid).AsDouble().ToString();
                            roomSqRStr = room.get_Parameter(NRoomSqKParamGuid).AsDouble().ToString();
                            Logger.Log("   площадь: " + roomSqStr, 2);
                            Logger.Log("   площадь округл: " + roomSqRStr, 2);

                            //запись в бэкап
                            string filePath = config.ServerPath + "roomsBackup/"+ date + "," + docName + "," + backupName + ".txt";
                            File.AppendAllText(filePath, roomType + "|" + room.Id.IntegerValue.ToString() + "|" + roomSqStr + "|" + roomSqRStr+"\n");
                            Logger.Log("   записано в бэкап", 2);

                            //Прогресс-бар: +1
                            PBCount++;
                            this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.value.Text = PBCount.ToString()));

                        }
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
                    #endregion

                    break;
                case 2:
                    #region Загрузка. Открытие файла
                    Logger.Log("Выбран сценарий Загрузить бэкап", 1);
                    string txtPath = config.ServerPath + "roomsBackup";
                    string searchString = docName + ",";
                    List<string> filesFromPath = Directory.GetFiles(txtPath).ToList();
                    if(filesFromPath==null||filesFromPath.Count == 0)
                    {
                        Logger.Log("Папка бэкапов пуста. Завершение работы.", 3);
                        return Result.Cancelled;
                    }
                    List<string> files = new List<string>();
                    foreach (string file in filesFromPath)
                    {
                        if(file.Contains(searchString)) files.Add(file);
                    }
                    string[] filesArray = files.ToArray();

                    if (filesArray.Length < 1)
                    {
                        new InfoWindow280("Для данной модели отсутствуют файлы резервных копий площадей помещений. " +
                        "Чтобы создать копию, воспользуйтесь плагином TNov Помещения Резервные копии - Сохранить").ShowDialog();
                        Logger.Log("Бэкапы отсутствуют. Завершение работы.", 3);
                        return Result.Cancelled;
                    }
                    #endregion
                    #region Загрузка. Диалог
                    //окно выбора бэкапа
                    Logger.Log("Запуск окна выбора бэкапа", 1);
                    roomsBackupLoad wpfview2 = new roomsBackupLoad(filesArray, docName);
                    bool? ok2 = wpfview2.ShowDialog();
                    if (ok2 != null && ok2 == true) { }
                    else { Logger.Log("Выполнение отменено пользователем. Завершение работы.", 3); return Result.Cancelled; }
                    #endregion
                    #region Загрузка. Заполнение списков
                    List<TNovRoom> tNovRooms = new List<TNovRoom>();
                    string[] lines = File.ReadAllLines(wpfview2.SelectedFilePath);
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        string[] parts = lines[i].Split('|');
                        int id = 0;
                        int.TryParse(parts[1], out id);
                        if (id == 0) continue;
                        ElementId eId = new ElementId(id);
                        Element elem = doc.GetElement(eId);
                        string roomCategory = parts[0];
                        string modelRoomId = parts[1];
                        string modelRoomS = "";
                        string modelRoomSK = "";
                        string modelRoomName = "";
                        int modelRoomGroupNumber = 0;
                        if (elem != null)
                        {
                            modelRoomS = elem.get_Parameter(NRoomSqParamGuid).AsDouble().ToString();
                            modelRoomSK = elem.get_Parameter(NRoomSqKParamGuid).AsDouble().ToString();
                            modelRoomName = elem.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                            if (roomCategory == "Квартиры")
                            {
                                string gNum = elem.get_Parameter(NRoomApartNumberParamGuid).AsString();
                                if (gNum != null && gNum.Length > 0) int.TryParse (gNum, out modelRoomGroupNumber);
                            }
                            if (roomCategory == "Офисы")
                            {
                                string gNum = elem.get_Parameter(NRoomOfficeNumber).AsString();
                                if (gNum != null && gNum.Length > 0) int.TryParse(gNum, out modelRoomGroupNumber);
                            }
                            if (roomCategory == "Кладовые")
                            {
                                string gNum = elem.get_Parameter(roomNumber).AsString();
                                if(gNum!=null&&gNum.Length>0) int.TryParse(gNum, out modelRoomGroupNumber);
                            }
                            if (roomCategory == "Прочие")
                            {
                                double gNum = elem.get_Parameter(NLevelNumberParamGuid).AsDouble();
                                modelRoomGroupNumber=(int)gNum;
                            }
                        }
                        else
                        {
                            modelRoomId = "не найдено";
                        }
                        TNovRoom tNovRoom = new TNovRoom()
                        {
                            RoomCategory = roomCategory,
                            RoomId = modelRoomId,
                            RoomName = modelRoomName,
                            RoomGroupNumber = modelRoomGroupNumber,
                            RoomModelS = modelRoomS,
                            RoomModelSK = modelRoomSK,
                            RoomBackupS = parts[2],
                            RoomBackupSK = parts[3],
                        };
                        if(tNovRoom.RoomModelS == tNovRoom.RoomBackupS && tNovRoom.RoomModelSK == tNovRoom.RoomBackupSK) { }
                        else tNovRooms.Add(tNovRoom);
                    }
                    #endregion
                    if (tNovRooms==null||tNovRooms.Count==0)
                    {
                        new InfoWindow280("Все площади из резервной копии соответствуют текущим в модели").ShowDialog();
                        Logger.Log("Площади совпадают. Завершение работы.", 3);
                        return Result.Cancelled;
                    }

                    //сортировка
                    tNovRooms = tNovRooms.OrderBy(x => x.RoomCategory).ThenBy(x=>x.RoomGroupNumber).ThenBy(x => x.RoomId).ToList();
                    #region Загрузка. Окно анализа
                    //окно анализа
                    Logger.Log("Запуск окна анализа бэкапа", 1);
                    string[] fileNameParts = Path.GetFileName(wpfview2.SelectedFilePath).Split(',');
                    string backupNameToWindow = fileNameParts[0] + " " + fileNameParts[2].Replace(".txt","");
                    roomsBackupAnalyse wpfview3 = new roomsBackupAnalyse(tNovRooms, backupNameToWindow);
                    bool? ok3 = wpfview3.ShowDialog();
                    if (ok3 != null && ok3 == true) { }
                    else { Logger.Log("Выполнение отменено пользователем. Завершение работы.", 3); return Result.Cancelled; }

                    Logger.Log("Сценарий: "+wpfview3.scenario,1);
                    #endregion
                    #region Загрузка. Восстановление
                    //восстановление
                    if (wpfview3.scenario != "0") tNovRooms = tNovRooms.Where(t=>t.RoomId==wpfview3.scenario).ToList();

                    using (Transaction transaction = new Transaction(doc))
                    {
                        try
                        {
                            transaction.Start("TNov - Резервное копирование (площади)");
                            Logger.Log("Открываем транзакцию", 1);
                            foreach (var tNovRoom in tNovRooms)
                            {
                                foreach (var room in rooms)
                                {
                                    //добавить квартирографию и офисографию
                                    if (room.Id.IntegerValue.ToString() == tNovRoom.RoomId)
                                    {
                                        Logger.Log("Помещение " + room.Id.IntegerValue.ToString(), 2);
                                        double sq = 0; Double.TryParse(tNovRoom.RoomBackupS, out sq);
                                        double sqk = 0; Double.TryParse(tNovRoom.RoomBackupSK, out sqk);
                                        if (sq > 0)
                                        {
                                            try
                                            {
                                                room.get_Parameter(NRoomSqParamGuid).Set(sq);
                                                room.get_Parameter(NRoomSqKParamGuid).Set(sqk);
                                                Logger.Log("   параметры назначены", 2);
                                            }
                                            catch (Exception ex)
                                            {
                                                Logger.Log("Помещение " + room.Id.IntegerValue.ToString() + " ошибка: " + ex.Message, 4);
                                            }
                                        }
                                        break;
                                    }
                                }
                            

                            }
                            transaction.Commit(); Logger.Log("Закрываем транзакцию", 1);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("Ошибка: " + ex.Message, 4);
                            new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                            unhandledError = true;
                        }
                    #endregion
                    }

                    break;
            }
            if (unhandledError)
            {
                Logger.Log("Завершение работы с ошибками.", 4);
                return Result.Succeeded;
            }
            Logger.Log("Завершение работы.", 5);
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
