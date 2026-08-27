using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System.Collections.Generic;
using System.Globalization;

namespace TNovRooms.Manager
{
    public static class ManagerUtils
    {
        /// <summary>Коэффициент перевода футов в метры, как в исходном Округлятоpе.</summary>
        public const double FeetToMeters = 0.3048;

        /// <summary>
        /// N_Площадь.Округленная — числовой параметр со значением в м², а N_Кв.Площадь.* —
        /// параметры типа «Площадь», хранящиеся во внутренних единицах Revit (фут²).
        /// Порядок операций повторяет исходный код, чтобы значения совпадали побитово.
        /// </summary>
        public static double SqMetersToInternal(double squareMeters)
        {
            return squareMeters / FeetToMeters / FeetToMeters;
        }

        public static double InternalToSqMeters(double internalArea)
        {
            return internalArea * FeetToMeters * FeetToMeters;
        }

        /// <summary>
        /// Разбор списка имён помещений из настроек. Пустые фрагменты отбрасываются:
        /// name.Contains("") истинно для любого имени и сломало бы отбор.
        /// </summary>
        public static string[] SplitNames(string source)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(source)) return result.ToArray();
            foreach (string part in source.Split(','))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }
            return result.ToArray();
        }

        /// <summary>Имя помещения содержит любой из фрагментов списка.</summary>
        public static bool NameMatchesAny(Room room, string[] names)
        {
            string name = room.Name ?? "";
            foreach (string n in names) { if (name.Contains(n)) return true; }
            return false;
        }

        public static long IdValue(ElementId id)
        {
            if (id == null) return -1;
#if R2022
            return id.IntegerValue;
#else
            return id.Value;
#endif
        }

        /// <summary>
        /// Файлы бэкапов пишутся через double.ToString() в культуре пользователя,
        /// поэтому в них встречаются и "12,3", и "12.3".
        /// </summary>
        public static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string t = text.Trim();
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;
            return double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static string AreaToText(double value)
        {
            return value.ToString("0.0", CultureInfo.CurrentCulture);
        }

        public static string CoefficientToText(double value)
        {
            return value.ToString("0.0#", CultureInfo.CurrentCulture);
        }
    }
}
