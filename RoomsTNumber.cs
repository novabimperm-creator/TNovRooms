using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Markup;
using TNovCommon;

namespace TNovRooms
{
    [Transaction(TransactionMode.Manual)]
    public class RoomsTNumber : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            #region Исходные
            DateTime dateTime = DateTime.Now;
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string DBCommandName = "Номера продаваемых помещений";
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

            #region Сбор элементов
            Logger.Log( "Сбор элементов",1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            #endregion
            #region Параметры
            Guid param1Guid = new Guid("51cf9b84-e3e6-4c52-b723-340669e3c500");//N_Секция
            Guid param2Guid = new Guid("4d2aa1b8-727c-43a1-8b1e-8c22dd484e11");//N_Эт.Номер
            Guid paramAGuid = new Guid("155f8c55-e05f-4737-883e-1338eb722735");//N_Квартира
            Guid param3AGuid = new Guid("7cdb6adb-756e-4e5b-b4d0-5ccaf3cee047");//N_Кв.НомерНаЭтаже
            Guid paramOGuid = new Guid("e73bb005-9ad8-489c-bc1f-fd8c3b521ec3");//N_Офис.Номер
            Guid paramTGuid = new Guid("5b03cee1-38d2-4e17-9f7d-a88fd3b1913b");//Т_Номер прод пом
            #endregion
            string prefix = "0";
            List<string> list = new List<string>() { "02-50ЛЕТ" , "16-НЧ", "ПОСЕЛК", "27-АВИА", "59-КОСМ1", "69-КРАС.3", "76-СУЗДАЛ.23", "76.23-18.03",
            "59-ППРК","27-ВРН-03","27-ЛАЗО-01","27-ХГ4"};
            foreach (string item in list) { if (docName.Contains(item)) { prefix = ""; break; } }
            bool unhandledError = false;
            #region Основной код
            using (Transaction transaction = new Transaction(doc))
            {
                try { 
                Logger.Log( "Открываем транзакцию",1);
                transaction.Start("TNov - номера помещений по ТЗ");
                foreach (Room elem in rooms)
                {
                    int scenario = 0; // 1 - кладовые, 2 - квартиры, 3 - офисы
                    if (elem.get_Parameter(BuiltInParameter.ROOM_NAME).AsString().Contains("Кладов")) scenario = 1;
                    if (elem.get_Parameter(paramAGuid) != null)
                    {
                        if (elem.get_Parameter(paramAGuid).AsInteger() == 1) scenario = 2;
                    }
                    if (elem.get_Parameter(paramOGuid) != null)
                    {
                        if (elem.get_Parameter(paramOGuid).AsString() != null && elem.get_Parameter(paramOGuid).AsString().Length > 0)
                        {
                            double num = 0;
                            bool isOffice = Double.TryParse(elem.get_Parameter(paramOGuid).AsString(), out num);
                            if (isOffice && num > 0) scenario = 3;
                        }
                    }
                    if (scenario > 0)
                    {
                        string value = ""; string part1 = "";
                        if (elem.get_Parameter(param1Guid) != null)
                        { part1 = elem.get_Parameter(param1Guid).AsString(); if (part1 != null && part1.Length > 0) value += part1; }
                        if (elem.get_Parameter(param2Guid) != null)
                        {
                            double part2 = Math.Round(elem.get_Parameter(param2Guid).AsDouble() / 10.76391);
                            if (Math.Abs(part2) > 0)
                            {
                                string pt2 = part2.ToString();

                                if (Math.Abs(part2) < 10)
                                {
                                    if (pt2.StartsWith("-")) { pt2 = pt2.Replace("-", "-" + prefix); }
                                    else pt2 = prefix + pt2;
                                }
                                value += "-" + pt2;
                            }
                        }
                        string part3 = ""; double part3double = 0;
                        switch (scenario)
                        {
                            case 1:
                                part3 = elem.get_Parameter(BuiltInParameter.ROOM_NUMBER).AsString();
                                break;
                            case 2:
                                part3 = elem.get_Parameter(param3AGuid).AsValueString();
                                break;
                            case 3:
                                part3 = elem.get_Parameter(paramOGuid).AsString();
                                break;
                        }

                        if (part3 != null && part3.Length > 0)
                        {
                            double.TryParse(part3, out part3double);
                            if (part3double > 0 && part3double < 10) part3 = prefix + part3;
                            value += "-" + part3;
                        }

                        string comments = "";
                        Parameter roomNazn = elem.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT);
                        if (scenario == 3 && roomNazn != null)
                        {
                            string nazn = "Офис";
                            if (roomNazn.AsString() != null && roomNazn.AsString().Length > 0)
                            {
                                nazn = roomNazn.AsString();
                            }
                            comments = nazn + " (№" + value + ")";
                        }
                        try
                        {
                            if (comments.Length > 0)
                                elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(comments);//назначаем также параметр Комментарии для спецификации офисов
                            elem.get_Parameter(paramTGuid).Set(value); //назначение целевого параметра
                            Logger.Log("Помещение " + elem.Id.IntegerValue.ToString() + ": параметры заполнены",2);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("Помещение " + elem.Id.IntegerValue.ToString() + " ошибка: "+ex.Message,4);
                        }
                    }
                }
                new InfoWindow280("Успешно!\nПараметр Т_Номер продаваемого помещения заполнен.").ShowDialog();
                transaction.Commit();
                Logger.Log( "Закрываем транзакцию",1);
                }
                catch (Exception ex)
                {
                    Logger.Log("Ошибка: " + ex.Message, 4);
                    new InfoWindow280("Ошибка: " + ex.Message).ShowDialog();
                    unhandledError = true;
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
    }
}
