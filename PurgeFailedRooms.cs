using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB.Architecture;
using System;
using TNovCommon;
using Newtonsoft.Json;

namespace TNovRooms
{
    [Transaction(TransactionMode.Manual)]
    public class PurgeFailedRooms : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Лишние удалить"; DateTime dateTime = DateTime.Now; string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            if(ServerUtils.CheckConnection(TNovClassName, TNovVersion)==false) return Result.Failed;

            // создание log - файла
            Logger.Initialize(TNovClassName,dateTime,TNovVersion);
            

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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            Logger.Log( "Сбор элементов",1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            
            Logger.Log( "Ищем неразмещенные помещения",1);
            int ec = 0; //счетчик неразмещенных помещений
            List<Room> failedrooms = new List<Room>();

            foreach (Room room in rooms) //проверка наличия неразмещенных помещений
            {
                double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                if (area == 0) { failedrooms.Add(room); ec++; }
            }

            bool runIt = false;

            if (ec > 0) //если есть неразмещенные помещения - показать окно
            {
                var viewModel = new QuestionWindowViewModel();
                viewModel.headtxt = "В проекте присутствуют неразмещенные или избыточные помещения в количестве " + ec + " шт. Удалить их?";
                var wpfview = new QuestionWindow280(viewModel);
                viewModel.CloseRequest += (s, e) => wpfview.Close();
                bool? ok = wpfview.ShowDialog();
                if (ok != null && ok == true) { runIt = true; } 
                else { Logger.Log("Операция отменена пользователем. Завершение работы.", 3); return Result.Cancelled; }
            }
            else
            {
                new InfoWindow280("Все хорошо!\nНеразмещенных помещений в модели не оказалось.").ShowDialog();
                Logger.Log("Лишних помещений нет. Завершение работы.",5);
                return Result.Succeeded;
            }
            using (Transaction transaction = new Transaction(doc))
            {
                if (runIt)
                {
                    Logger.Log( "Открываем транзакцию",1);
                    transaction.Start("TNov - Удалить лишние помещения");
                    foreach (Room room in failedrooms)
                    {
                        ElementId roomId = room.Id;
                        Logger.Log( "Помещение "+roomId.ToString(),2);
                        doc.Delete(roomId);
                        Logger.Log( "   успешно.",2);
                    }
                    var info1 = new InfoWindow280("Успешно!\nНехорошие помещения удалены."); info1.ShowDialog();
                    transaction.Commit();
                    Logger.Log( "Закрываем транзакцию",1);
                }
            }
            Logger.Log( "Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
