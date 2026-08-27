using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>
    /// GUID общих параметров помещений и чтение значений. GUID совпадают с теми,
    /// что используют RoomsRound / RoomsBackup / Aparts / Offices.
    /// </summary>
    public static class RoomParams
    {
        public static readonly Guid RoundedArea = new Guid("4f890165-ec27-4a22-811a-07e010101ec5");
        public static readonly Guid RoundedAreaK = new Guid("e6b18cda-4550-4531-afae-96a9035f7fca");
        public static readonly Guid IsApartment = new Guid("155f8c55-e05f-4737-883e-1338eb722735");
        public static readonly Guid ApartmentNumber = new Guid("2f2edd07-cd47-4e30-b091-c1ceb5e6ff63");
        public static readonly Guid OfficeNumber = new Guid("e73bb005-9ad8-489c-bc1f-fd8c3b521ec3");
        public static readonly Guid LevelNumber = new Guid("4d2aa1b8-727c-43a1-8b1e-8c22dd484e11");
        public static readonly Guid FloorArea = new Guid("4aa338cf-d518-4954-8e85-f8a99c94da9f");

        public const string RoundedAreaTitle = "N_Площадь.Округленная";
        public const string RoundedAreaKTitle = "N_Площадь.ОкруглСКоэффициентом";
        /// <summary>Параметр типа «Площадь» — AsDouble/Set работают во внутренних единицах Revit (фут²).</summary>
        public const string FloorAreaTitle = "N_Площадь этажа";
        public const string DepartmentTitle = "Назначение";
        public const string StorageRoomName = "Кладовая";

        /// <summary>Имя помещения без номера (Room.Name возвращает "Имя Номер").</summary>
        public static string GetRoomName(Room room)
        {
            Parameter p = room.get_Parameter(BuiltInParameter.ROOM_NAME);
            string value = p?.AsString();
            return string.IsNullOrWhiteSpace(value) ? "(без имени)" : value.Trim();
        }

        public static string GetRoomNumber(Room room)
        {
            Parameter p = room.get_Parameter(BuiltInParameter.ROOM_NUMBER);
            return p?.AsString() ?? "";
        }

        public static string GetDepartment(Room room)
        {
            Parameter p = room.get_Parameter(BuiltInParameter.ROOM_DEPARTMENT);
            if (p == null || !p.HasValue) return "";
            string value = p.AsString();
            return value == null ? "" : value.Trim();
        }

        public static bool HasDepartment(Room room)
        {
            return GetDepartment(room).Length > 0;
        }

        /// <summary>Площадь в квадратных метрах (0 у неразмещённых помещений).</summary>
        public static double GetAreaM2(Room room)
        {
            Parameter p = room.get_Parameter(BuiltInParameter.ROOM_AREA);
            if (p == null) return 0;
            return p.AsDouble() * ManagerUtils.FeetToMeters * ManagerUtils.FeetToMeters;
        }

        public static bool IsUnplaced(Room room)
        {
            Parameter p = room.get_Parameter(BuiltInParameter.ROOM_AREA);
            return p == null || p.AsDouble() == 0;
        }

        /// <summary>
        /// Кладовая как самостоятельная единица: имя ровно «Кладовая», и помещение
        /// не входит ни в квартиру, ни в офис. Условие исключений совпадает с тем,
        /// по которому бэкапы относят помещение к категории «Кладовые».
        /// </summary>
        public static bool IsStorageRoom(Room room)
        {
            if (GetRoomName(room) != StorageRoomName) return false;

            Parameter apartParam = room.get_Parameter(IsApartment);
            if (apartParam != null && apartParam.AsInteger() == 1) return false;

            return !OfficeParams.IsOfficeRoom(room);
        }

        /// <summary>
        /// Сырое значение N_Эт.Номер. В шаблоне это параметр типа «Площадь», поэтому
        /// AsDouble возвращает внутренние единицы Revit, а не сам номер этажа.
        /// Годится как ключ сортировки и группировки: преобразование монотонное.
        /// </summary>
        public static double GetLevelNumber(Room room)
        {
            Parameter p = room.get_Parameter(LevelNumber);
            return p == null ? 0 : p.AsDouble();
        }

        /// <summary>Номер этажа для показа пользователю.</summary>
        public static double GetLevelDisplayNumber(Room room)
        {
            return Math.Round(ManagerUtils.InternalToSqMeters(GetLevelNumber(room)), 3);
        }

        public static bool HasRoundingParams(Room room)
        {
            return Param.ParamExistByGuid(RoundedArea, room) && Param.ParamExistByGuid(RoundedAreaK, room);
        }

        public static double GetRoundedArea(Room room)
        {
            Parameter p = room.get_Parameter(RoundedArea);
            return p == null ? 0 : p.AsDouble();
        }

        public static double GetRoundedAreaK(Room room)
        {
            Parameter p = room.get_Parameter(RoundedAreaK);
            return p == null ? 0 : p.AsDouble();
        }

        /// <summary>
        /// Категория помещения для файла бэкапа. Логика повторяет RoomsBackup,
        /// чтобы новые бэкапы читались старым плагином «Резервные копии».
        /// </summary>
        public static string GetBackupCategory(Room room)
        {
            string roomType = "Прочие";

            Parameter apartParam = room.get_Parameter(IsApartment);
            int isApart = apartParam == null ? 0 : apartParam.AsInteger();
            if (isApart == 1) roomType = "Квартиры";

            bool office = false;
            Parameter offnumParam = room.get_Parameter(OfficeNumber);
            if (offnumParam != null && offnumParam.HasValue)
            {
                string offNumValue = offnumParam.AsString();
                if (!string.IsNullOrEmpty(offNumValue)) { office = true; roomType = "Офисы"; }
            }

            string name = room.get_Parameter(BuiltInParameter.ROOM_NAME)?.AsString() ?? "";
            if (name.Contains("Кладов") && isApart != 1 && !office) roomType = "Кладовые";

            return roomType;
        }
    }
}
