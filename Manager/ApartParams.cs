using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;

namespace TNovRooms.Manager
{
    /// <summary>
    /// GUID общих параметров квартирографии и чтение их значений.
    /// GUID совпадают с теми, что использует команда Aparts.
    /// </summary>
    public static class ApartParams
    {
        public static readonly Guid NumberAtLevel = new Guid("7cdb6adb-756e-4e5b-b4d0-5ccaf3cee047");
        public static readonly Guid LivingRoom = new Guid("4ec5dcb5-eb89-414f-8296-666e8ca6abcc");
        public static readonly Guid LivingRoomOldTemplate = new Guid("0ffffc62-53c8-4c8f-b435-ddb1777af6fb");
        public static readonly Guid AreaTotal = new Guid("878f4b53-8dfa-4bdf-8f30-ddbf764d6bf4");
        public static readonly Guid AreaTotalK = new Guid("b7b357cd-9449-4bd0-aa6d-1af9c29ba5d3");
        public static readonly Guid Area = new Guid("05960e6f-00c1-47c9-ba37-0e9c9198ed8e");
        public static readonly Guid AreaBalcony = new Guid("3f1b5a3f-496d-4d87-980d-81891c833f71");
        public static readonly Guid AreaBalconyK = new Guid("6adce072-d2ad-400a-9bab-8052d7eb09d0");
        public static readonly Guid AreaLiving = new Guid("a3cf3a19-5377-4bc0-9f85-c26e206fb64a");
        public static readonly Guid RoomsCount = new Guid("188e3cb5-3003-4d13-89fb-a531173f212d");

        public const string SpecGridTitle = "Поквартир.Сетка";
        public const string NumberTitle = "N_Кв.Номер";
        public const string NumberAtLevelTitle = "N_Кв.НомерНаЭтаже";

        /// <summary>
        /// В шаблонах до 2022.3 у параметра «N_Кв.Комната.Жилая» другой GUID.
        /// </summary>
        public static Guid GetLivingRoomGuid(ExternalCommandData commandData)
        {
            return Aparts.OldTemplateProject(commandData) ? LivingRoomOldTemplate : LivingRoom;
        }

        public static bool IsApartmentRoom(Room room)
        {
            Parameter p = room.get_Parameter(RoomParams.IsApartment);
            return p != null && p.AsInteger() == 1;
        }

        /// <summary>
        /// Сквозной номер квартиры. Исходный код читает его через AsValueString,
        /// AsString оставлен как резервный путь для нестандартных типов параметра.
        /// </summary>
        public static string GetNumber(Room room)
        {
            Parameter p = room.get_Parameter(RoomParams.ApartmentNumber);
            if (p == null) return "";
            string value = p.AsValueString();
            if (string.IsNullOrEmpty(value)) value = p.AsString();
            return value == null ? "" : value.Trim();
        }

        public static int GetNumberAtLevel(Room room)
        {
            Parameter p = room.get_Parameter(NumberAtLevel);
            return p == null ? 0 : p.AsInteger();
        }

        public static bool IsLivingRoom(Room room, Guid livingGuid)
        {
            Parameter p = room.get_Parameter(livingGuid);
            return p != null && p.AsInteger() == 1;
        }

        /// <summary>Значение параметра площади квартиры в м² (в модели хранится в фут²).</summary>
        public static double GetAreaM2(Room room, Guid guid)
        {
            Parameter p = room.get_Parameter(guid);
            return p == null ? 0 : ManagerUtils.InternalToSqMeters(p.AsDouble());
        }

        /// <summary>N_Кв.Комнаты.Количество — текстовый параметр.</summary>
        public static int GetRoomsCount(Room room)
        {
            Parameter p = room.get_Parameter(RoomsCount);
            if (p == null) return 0;
            int value;
            return int.TryParse(p.AsString(), out value) ? value : 0;
        }

        /// <summary>
        /// Проверка через get_Parameter, а не через перебор ParametersMap:
        /// метод вызывается для каждого помещения модели.
        /// </summary>
        public static bool HasApartParams(Room room)
        {
            return room.get_Parameter(AreaTotal) != null
                && room.get_Parameter(AreaTotalK) != null
                && room.get_Parameter(Area) != null
                && room.get_Parameter(AreaBalcony) != null
                && room.get_Parameter(AreaBalconyK) != null
                && room.get_Parameter(AreaLiving) != null
                && room.get_Parameter(RoomsCount) != null;
        }

        public static bool HasSpecGrid(Room room)
        {
            return room.LookupParameter(SpecGridTitle) != null;
        }
    }
}
