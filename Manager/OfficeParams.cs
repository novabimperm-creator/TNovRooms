using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;

namespace TNovRooms.Manager
{
    /// <summary>
    /// GUID общих параметров офисографии и чтение их значений.
    /// GUID совпадают с теми, что использует команда Offices.
    /// </summary>
    public static class OfficeParams
    {
        public static readonly Guid AreaTotal = new Guid("835dbef4-b314-4a24-9c12-814abcf6b66f");
        public static readonly Guid AreaUseful = new Guid("8afe9673-011e-49d5-a8a4-57fc14cc3b1d");
        public static readonly Guid AreaCalculated = new Guid("72d42023-d485-49e3-8b7d-2ddda6791f28");

        public const string NumberTitle = "N_Офис.Номер";

        /// <summary>
        /// Помещение относится к офису, если у него заполнен N_Офис.Номер.
        /// Условие повторяет команду Offices.
        /// </summary>
        public static bool IsOfficeRoom(Room room)
        {
            Parameter p = room.get_Parameter(RoomParams.OfficeNumber);
            if (p == null || !p.HasValue) return false;
            string value = p.AsString();
            return value != null && value.Length > 0;
        }

        public static string GetNumber(Room room)
        {
            Parameter p = room.get_Parameter(RoomParams.OfficeNumber);
            if (p == null) return "";
            string value = p.AsValueString();
            if (string.IsNullOrEmpty(value)) value = p.AsString();
            return value == null ? "" : value.Trim();
        }

        /// <summary>Значение параметра площади офиса в м² (в модели хранится в фут²).</summary>
        public static double GetAreaM2(Room room, Guid guid)
        {
            Parameter p = room.get_Parameter(guid);
            return p == null ? 0 : ManagerUtils.InternalToSqMeters(p.AsDouble());
        }

        public static bool HasOfficeParams(Room room)
        {
            return room.get_Parameter(AreaTotal) != null
                && room.get_Parameter(AreaUseful) != null
                && room.get_Parameter(AreaCalculated) != null;
        }
    }
}
