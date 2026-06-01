using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.Architecture;
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Threading;
using TNovCommon;
using Newtonsoft.Json;

namespace TNovRooms
{
    
    [Transaction(TransactionMode.Manual)]
    public class ApartsNum : IExternalCommand
    {
        private TNovProgressBar apartsnumProgressBar;
        private void ThreadStartingPoint()
        {
            this.apartsnumProgressBar = new TNovProgressBar();
            this.apartsnumProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Сквозные номера квартир";
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
            Guid NLevelNumberParamGuid = new Guid("4d2aa1b8-727c-43a1-8b1e-8c22dd484e11"); //N_Эт.Номер
            Guid NRoomIsApartParamGuid = new Guid("155f8c55-e05f-4737-883e-1338eb722735"); //N_Квартира
            Guid NRoomApartNumberParamGuid = new Guid("2f2edd07-cd47-4e30-b091-c1ceb5e6ff63"); //N_Кв.Номер
            Guid NRoomApartNumAtLevelParamGuid = new Guid("7cdb6adb-756e-4e5b-b4d0-5ccaf3cee047"); //N_Кв.НомерНаЭтаже
            #endregion

            #region Сбор элементов
            Logger.Log( "Сбор элементов",1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список

            List<Room> roomsA = new List<Room>(); //список помещений квартир

            foreach (Room room in rooms) //заполнение списка помещений квартир
            {
                if (room.get_Parameter(NRoomIsApartParamGuid)?.AsInteger() == 1) roomsA.Add(room); 
            }
            
            var roomsAF = from room in roomsA //сортировка квартир по Эт.Номеру и номеру на этаже
                                      orderby (double)(room.get_Parameter(NLevelNumberParamGuid)?.AsDouble())*1000+ (int)(room.get_Parameter(NRoomApartNumAtLevelParamGuid)?.AsInteger())
                                      select room;

            var floors = from room in roomsAF //группирование по Эт.Номеру и номеру на этаже
                         group room by ((double)(room.get_Parameter(NLevelNumberParamGuid)?.AsDouble()) * 0.3048 * 0.3048 * 1000 + (int)(room.get_Parameter(NRoomApartNumAtLevelParamGuid)?.AsInteger())).ToString();
            
            List<Room>roomsToSet = new List<Room>(); //итоговый список помещений
            List<int>values = new List<int>(); //итоговый список значений параметра
            #endregion

            int i1 = 1;

            #region Диалог
            Logger.Log( "Диалоговое окно - ввод первого номера",1);
            var viewModel = new ApartsNumViewModel();
            var wpfview = new ApartsNumWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }

            string first = viewModel.first;

            Logger.Log( "Проверка корректности номера",1);

            int.TryParse(first, out i1); //первый номер квартиры

            #endregion

            #region Итоговые списки
            Logger.Log( "Заполняем итоговые списки",1);

            foreach (var f in floors)
            {
                Logger.Log( "Этаж " +f.First().get_Parameter(NLevelNumberParamGuid)?.AsDouble().ToString(),2);
                foreach (Room room in f)
                {
                    roomsToSet.Add(room); //заполняем итоговый список помещений
                    values.Add(i1); //заполняем итоговый список значений параметра
                }
                i1++;
            }
            #endregion

            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsnumProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsnumProgressBar.value.Text = "Квартира " + PBCount.ToString()));
                this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsnumProgressBar.TNov_ProgressBar.Maximum = (double)roomsToSet.Count()));
                this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsnumProgressBar.maxvalue.Text = roomsToSet.Count().ToString()));

                int i = 0;
                transaction.Start("TNov - Сквозные номера квартир");
                Logger.Log( "Открываем транзакцию",1);

                foreach (Room room in roomsToSet)
                {
                    try
                    {
                        room.get_Parameter(NRoomApartNumberParamGuid)?.Set(values[i].ToString());
                        i++;
                        PBCount++;
                        Logger.Log("   Помещение " + room.Id + " успешно", 2);
                    }
                    catch (Exception ex) { Logger.Log("   Помещение " + room.Id + " ошибка: " + ex.Message, 4); }
                    this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsnumProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsnumProgressBar.value.Text = "Квартира "+PBCount.ToString()));
                    
                }

                //var info1 = new InfoWindow280("Успешно!\nСквозные номера квартир заполнены."); info1.ShowDialog();
                
                transaction.Commit();
                this.apartsnumProgressBar.Dispatcher.Invoke((System.Action)(() => this.apartsnumProgressBar.Close()));
                Logger.Log( "Закрываем транзакцию",1);
            }
            #endregion

            Logger.Log( "Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
