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

            Guid NRoomSqParamGuid = new Guid("4f890165-ec27-4a22-811a-07e010101ec5"); //N_Площадь.Округленная
            Guid NRoomSqKParamGuid = new Guid("e6b18cda-4550-4531-afae-96a9035f7fca"); //N_Площадь.ОкруглСКоэффициентом
            Guid NRoomIsApartParamGuid = new Guid("155f8c55-e05f-4737-883e-1338eb722735"); //N_Квартира
            Guid NRoomApartNumberParamGuid = new Guid("2f2edd07-cd47-4e30-b091-c1ceb5e6ff63"); //N_Кв.Номер
            Guid NRoomApartNumAtLevelParamGuid = new Guid("7cdb6adb-756e-4e5b-b4d0-5ccaf3cee047"); //N_Кв.НомерНаЭтаже
            Guid NRoomApartLivingParamGuid = new Guid("4ec5dcb5-eb89-414f-8296-666e8ca6abcc");
                if(OldTemplateProject(commandData)) 
                    NRoomApartLivingParamGuid = new Guid("0ffffc62-53c8-4c8f-b435-ddb1777af6fb");//N_Кв.Комната.Жилая
            Guid NRoomApartSqOParamGuid = new Guid("878f4b53-8dfa-4bdf-8f30-ddbf764d6bf4"); //N_Кв.Площадь.Общая
            Guid NRoomApartSqOKParamGuid = new Guid("b7b357cd-9449-4bd0-aa6d-1af9c29ba5d3"); //N_Кв.Площадь.ОбщаяСКоэффициентом
            Guid NRoomApartSqParamGuid = new Guid("05960e6f-00c1-47c9-ba37-0e9c9198ed8e"); //N_Кв.Площадь
            Guid NRoomApartSqBParamGuid = new Guid("3f1b5a3f-496d-4d87-980d-81891c833f71"); //N_Кв.Площадь.Балконы
            Guid NRoomApartSqBKParamGuid = new Guid("6adce072-d2ad-400a-9bab-8052d7eb09d0"); //N_Кв.Площадь.БалконыСКоэффициентом
            Guid NRoomApartSqLivParamGuid = new Guid("a3cf3a19-5377-4bc0-9f85-c26e206fb64a"); //N_Кв.Площадь.Жилая
            Guid NRoomApartNumberOfRoomsParamGuid = new Guid("188e3cb5-3003-4d13-89fb-a531173f212d"); //N_Кв.Комнаты.Количество
            string NRoomApartRoomToSpec = "Поквартир.Сетка"; //Поквартир.Сетка
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
                if(room.get_Parameter(NRoomIsApartParamGuid)?.AsInteger()==1) arooms.Add(room);
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

            string names1 = viewModel.k05; string names2 = viewModel.k03; bool recalc = viewModel.recalc;

            //получаем имена помещений, удаляем возможные пробелы в начале и конце имен
            string[] n1 = names1.Split(','); for (int i = 0; i < n1.Length; i++) { n1[i] = n1[i].Trim(); }
            string[] n2 = names2.Split(','); for (int i = 0; i < n2.Length; i++) { n2[i] = n2[i].Trim(); }

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
                    if(room.get_Parameter(NRoomIsApartParamGuid)?.AsInteger()==1)
                    {
                        string apartNum = room.get_Parameter(NRoomApartNumberParamGuid).AsValueString();
                        foreach (var aroom in arooms)
                        {
                            string apartNum1 = aroom.get_Parameter(NRoomApartNumberParamGuid).AsValueString();
                            if (apartNum1 == apartNum) newARooms.Add(aroom);
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
                        double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() * 0.3048 * 0.3048;
                        double areaR = Math.Round(area, 1);
                        string name = room.Name;
                        double k = 1;
                        foreach (string n in n1) { if (name.Contains(n)) { k = 0.5; } }
                        foreach (string n in n2) { if (name.Contains(n)) { k = 0.3; } }
                        double areaRK = Math.Round((areaR * k + 0.000001), 1);
                        room.get_Parameter(NRoomSqParamGuid)?.Set(areaR);
                        room.get_Parameter(NRoomSqKParamGuid)?.Set(areaRK);
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
                string apart = aroom.get_Parameter(NRoomApartNumberParamGuid).AsValueString();
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
            //Квартирография
            var aroomssortbynum = from aroom in arooms //сортированный список помещений по номеру квартиры
                              orderby aroom.get_Parameter(NRoomApartNumberParamGuid).AsValueString()
                                select aroom;

            var aparts = from aroom in aroomssortbynum //список квартир
                         group aroom by aroom.get_Parameter(NRoomApartNumberParamGuid).AsValueString();

            int apartsCount = aparts.Count();

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

                foreach (var apart in aparts) //проходим по каждой квартире в списке квартир
                {
                    Logger.Log("Квартира " + apart.First().get_Parameter(NRoomApartNumberParamGuid).AsValueString(), 2);

                    double apsqo = 0; //объявляем переменную для заполнения значения параметра N_Кв.Площадь.Общая
                    double apsqok = 0; //N_Кв.Площадь.ОбщаяСКоэффициентом
                    double apsq = 0; //N_Кв.Площадь
                    double apsqb = 0; //N_Кв.Площадь.Балконы
                    double apsqbk = 0; //N_Кв.Площадь.БалконыСКоэффициентом
                    double apsqliv = 0; //N_Кв.Площадь.Жилая
                    int aprn = 0; //N_Кв.Комнаты.Количество
                    int specCount = 0; //Поквартир.Сетка

                    foreach (var aroom in apart) //проходим по каждой комнате в квартире
                    {
                        double sqNonConvert = aroom.get_Parameter(NRoomSqParamGuid).AsDouble();
                        double sq = aroom.get_Parameter(NRoomSqParamGuid).AsDouble() / 0.3048 / 0.3048; //объявляем переменную, получаем площадь каждого помещения в квартире
                        Logger.Log("   Помещение " + aroom.Id.ToString() + " имя: " + aroom.Name + " площадь:" + sqNonConvert.ToString(), 2);

                        string name = aroom.Name; 
                        double sqsq = sq; double sqb = 0; double sqbk = 0; double sqliv = 0;

                        apsqo = apsqo + sq; //добавляем значение площади помещения к общей площади квартиры

                        double sqk = aroom.get_Parameter(NRoomSqKParamGuid).AsDouble() / 0.3048 / 0.3048; //получаем площадь с коэфф каждого помещения в квартире
                        apsqok = apsqok + sqk; //общая с коэфф

                        foreach (string n in n1) { if (name.Contains(n)) { sqsq = 0; break; } }
                        foreach (string n in n2) { if (name.Contains(n)) { sqsq = 0; break; } }
                        apsq = apsq + sqsq; //кв.площадь

                        foreach (string n in n1) { if (name.Contains(n)) { sqb = sq; break; } }
                        foreach (string n in n2) { if (name.Contains(n)) { sqb = sq; break; } }
                        apsqb = apsqb + sqb; //балконы

                        foreach (string n in n1) { if (name.Contains(n)) { sqbk = sqk; break; } }
                        foreach (string n in n2) { if (name.Contains(n)) { sqbk = sqk; break; } }
                        apsqbk = apsqbk + sqbk; //балк с коэфф

                        int livingroom = aroom.get_Parameter(NRoomApartLivingParamGuid).AsInteger();
                        if (livingroom == 1) 
                        { 
                            sqliv = sq;
                            aprn++; //кол-во комнат
                        }
                        apsqliv = apsqliv + sqliv; //жилая
                    }

                    //N_Кв.Площадь.Общая
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.get_Parameter(NRoomApartSqOParamGuid).Set(apsqo);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр N_Кв.Площадь.Общая ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь.ОбщаяСКоэффициентом
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.get_Parameter(NRoomApartSqOKParamGuid).Set(apsqok);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр N_Кв.Площадь.ОбщаяСКоэффициентом ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.get_Parameter(NRoomApartSqParamGuid).Set(apsq);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр N_Кв.Площадь ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь.Балконы
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.get_Parameter(NRoomApartSqBParamGuid).Set(apsqb);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр N_Кв.Площадь.Балконы ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь.БалконыСКоэффициентом
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.get_Parameter(NRoomApartSqBKParamGuid).Set(apsqbk);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр N_Кв.Площадь.БалконыСКоэффициентом ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь.Жилая
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.get_Parameter(NRoomApartSqLivParamGuid).Set(apsqliv);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр N_Кв.Площадь.Жилая ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Комнаты.Количество
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.get_Parameter(NRoomApartNumberOfRoomsParamGuid).Set(aprn.ToString());
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр N_Кв.Комнаты.Количество ошибка: " + ex.Message, 4);
                        }
                    }

                    //Поквартир.Сетка
                    foreach (var aroom in apart)
                    {
                        if (specCount == 0) aroom.LookupParameter(NRoomApartRoomToSpec).Set(1); //назначаем Поквартир.Сетка только первому помещению в кв
                        else aroom.LookupParameter(NRoomApartRoomToSpec).Set(0);
                        specCount++;
                    }

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
