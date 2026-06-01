using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TNovCommon;

namespace TNovRooms
{
    [Transaction(TransactionMode.Manual)]
    public class TNovRoomUpdater : IUpdater
    {
        static AddInId _appId;
        static UpdaterId _updaterId;

        public TNovRoomUpdater(AddInId id)
        {
            _appId = id;

            _updaterId = new UpdaterId(_appId, new Guid("898f186f-c08c-4bd9-a81b-01cc3fa96f0e"));
        }

        public void Execute(UpdaterData data)
        {
            Guid param1Guid = new Guid("51cf9b84-e3e6-4c52-b723-340669e3c500");//N_Секция
            Guid param2Guid = new Guid("4d2aa1b8-727c-43a1-8b1e-8c22dd484e11");//N_Эт.Номер
            Guid paramAGuid = new Guid("155f8c55-e05f-4737-883e-1338eb722735");//N_Квартира
            Guid param3AGuid = new Guid("7cdb6adb-756e-4e5b-b4d0-5ccaf3cee047");//N_Кв.НомерНаЭтаже
            Guid paramOGuid = new Guid("e73bb005-9ad8-489c-bc1f-fd8c3b521ec3");//N_Офис.Номер
            Guid paramTGuid = new Guid("5b03cee1-38d2-4e17-9f7d-a88fd3b1913b");//Т_Номер прод пом
            Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
            Guid TPolozhParamGuid = new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"); //Т_Положение
            Guid TNaznParamGuid = new Guid("2a73f7b8-05e7-410a-b22a-66498e315df4"); //Т_Назначение

            Document doc = data.GetDocument();
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;

            string prefix = "0";
            string docName = doc.Title.ToString();
            List<string> list = new List<string>() { "02-50ЛЕТ" , "16-НЧ", "ПОСЕЛК", "27-АВИА", "59-КОСМ1", "69-КРАС.3", "76-СУЗДАЛ.23", "76.23-18.03",
            "59-ППРК","27-ВРН-03","27-ЛАЗО-01","27-ХГ4"};
            foreach (string item in list) { if (docName.Contains(item)) { prefix = ""; break; } }

            List<ElementId> idsA = data.GetAddedElementIds().ToList();
            List<ElementId> idsB = data.GetModifiedElementIds().ToList();

            List<ElementId> ids = new List<ElementId>();

            foreach (var id in idsA) ids.Add(id);
            foreach (var id in idsB) ids.Add(id);

            if (docName.Contains("-АР") || docName.Contains("_АР") || docName.Contains("-АР-") || docName.Contains("_ПОФ") || docName.Contains("-ПОФ-"))
            {
                foreach (ElementId id in ids)
                {
                    try 
                    {
                        Element elem = doc.GetElement(id);
                        if (elem == null) continue;

                        int scenario = 0; // 1 - кладовые, 2 - квартиры, 3 - офисы

                        Parameter roomNameParam = elem.get_Parameter(BuiltInParameter.ROOM_NAME);
                        if (roomNameParam != null && roomNameParam.AsString()?.Contains("Кладов") == true) scenario = 1;
                        else
                        {
                            if (Param.ParamExistByGuid(paramAGuid, elem))
                            {
                                if (elem.get_Parameter(paramAGuid).AsInteger() == 1) scenario = 2;
                            }
                            if (Param.ParamExistByGuid(paramOGuid, elem))
                            {
                                if (elem.get_Parameter(paramOGuid).AsString() != null && elem.get_Parameter(paramOGuid).AsString().Length > 0)
                                {
                                    double num = 0;
                                    bool isOffice = Double.TryParse(elem.get_Parameter(paramOGuid).AsString(), out num);
                                    if (isOffice && num > 0) scenario = 3;
                                }
                            }
                        }
                        if (scenario > 0)
                        {
                            string value = ""; string part1 = "";
                            if (Param.ParamExistByGuid(param1Guid, elem))
                            {
                                part1 = elem.get_Parameter(param1Guid).AsString();
                                if (part1 != null && part1.Length > 0) value += part1;
                            }
                            if (Param.ParamExistByGuid(param2Guid, elem))
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
                                    if (Param.ParamExistByGuid(param3AGuid, elem))
                                        part3 = elem.get_Parameter(param3AGuid).AsValueString();
                                    break;
                                case 3:
                                    if (Param.ParamExistByGuid(paramOGuid, elem))
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
                            string roomNaznValue = "";
                            if (roomNazn.HasValue) roomNaznValue = roomNazn.AsString();
                            if (scenario == 3 && roomNaznValue.Length > 0)
                            {
                                comments = roomNaznValue + " (№" + value + ")";
                            }
                            if (comments.Length > 0)
                                elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS).Set(comments);//назначаем также параметр Комментарии для спецификации офисов
                            
                            if(Param.ParamExistByGuid(paramTGuid, elem)) elem.get_Parameter(paramTGuid).Set(value);


                            if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, elem) && elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1)
                            { }
                            else 
                            { 
                                string value1 = GetTNazn(roomNazn.AsString(), elem.get_Parameter(BuiltInParameter.ROOM_NAME).AsString());
                                if (Param.ParamExistByGuid(TPolozhParamGuid, elem))
                                {
                                    elem.get_Parameter(TPolozhParamGuid).Set(value1);
                                }
                                if (Param.ParamExistByGuid(TNaznParamGuid, elem))
                                {
                                    elem.get_Parameter(TNaznParamGuid).Set(value1);
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        
                    }
                    


                }
            }
                

            


        }
        string GetTNazn(string Nazn, string Name)
        {
            string TNazn = "";
            if (Nazn.Contains("Жилое")) TNazn = Nazn;
            else if (Nazn.Contains("Технич"))
            {
                if (Name.Contains("Лестн") || Name.Contains("лестн")) TNazn = "Лестница";
                else TNazn = "Техническое";
            }
            else if (Nazn.Contains("Лестн")) TNazn = "Лестница";
            else if (Nazn.Contains("Кладов")) TNazn = "Кладовые";
            else if (Nazn.Contains("Встроен")) TNazn = "МОП";
            else if (Nazn.Contains("Парк")) TNazn = "МОП";
            else if (Nazn.Contains("МОП"))
            {
                if (Name.Contains("Лестн") || Name.Contains("лестн")) TNazn = "Лестница";
                else if (Name.Contains("Кладов")) TNazn = "Кладовые";
                else if (Name.Contains("Электр")) TNazn = "Техническое";
                else if (Name.Contains("связи")) TNazn = "Техническое";
                else if (Name.Contains("Технич")) TNazn = "Техническое";
                else if (Name.Contains("Техпом")) TNazn = "Техническое";
                else if (Name.Contains("кондиц")) TNazn = "Техническое";
                else if (Name.Contains("ИТП")) TNazn = "Техническое";
                else if (Name.Contains("Котельная")) TNazn = "Техническое";
                else if (Name.Contains("Пульт")) TNazn = "Техническое";
                else if (Name.Contains("Венткамера")) TNazn = "Техническое";
                else TNazn = "МОП";
            }
            else TNazn = "Коммерция";
            return TNazn;
        }
        public string GetAdditionalInformation()
        {
            return "TNov, bim@pm-nova.ru";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.FloorsRoofsStructuralWalls;
        }

        public UpdaterId GetUpdaterId()
        {
            return _updaterId;
        }

        public string GetUpdaterName()
        {
            return "TNovRoomUpdater";
        }
    }
}
