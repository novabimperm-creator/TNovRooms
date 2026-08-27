using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>
    /// Чтение списка и запись файлов бэкапов площадей. Формат файла и имени полностью
    /// совместим с плагином «Помещения Резервные копии».
    /// </summary>
    public static class RoomAreaBackupStore
    {
        private const string FolderName = "roomsBackup";

        public static string GetFolder(TNovConfig config)
        {
            return config.ServerPath + FolderName;
        }

        public static List<RoomAreaBackup> LoadList(TNovConfig config, string docName)
        {
            var result = new List<RoomAreaBackup>();
            string folder = GetFolder(config);

            string[] files;
            try
            {
                if (!Directory.Exists(folder)) return result;
                files = Directory.GetFiles(folder, "*.txt");
            }
            catch (Exception ex)
            {
                Logger.Log("Не удалось прочитать папку бэкапов: " + ex.Message, 4);
                return result;
            }

            string searchString = docName + ",";
            foreach (string file in files)
            {
                if (!Path.GetFileName(file).Contains(searchString)) continue;
                result.Add(new RoomAreaBackup(file, docName));
            }

            return result.OrderByDescending(b => b.Timestamp).ThenByDescending(b => b.FileName).ToList();
        }

        /// <summary>
        /// Записывает бэкап по всем помещениям модели одной операцией записи.
        /// Числа сохраняются через double.ToString() в текущей культуре — так же,
        /// как это делает старый плагин, иначе его строковое сравнение перестанет работать.
        /// </summary>
        public static string Save(TNovConfig config, string docName, string backupName, IList<Room> rooms)
        {
            string folder = GetFolder(config);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string date = DateTime.Now.ToString("dd.MM.yyyy HH-mm-ss", CultureInfo.InvariantCulture);
            string safeName = SanitizeName(backupName);
            string filePath = Path.Combine(folder, date + "," + docName + "," + safeName + ".txt");

            var sb = new StringBuilder();
            foreach (Room room in rooms)
            {
                long idint = ManagerUtils.IdValue(room.Id);
                string category = RoomParams.GetBackupCategory(room);
                string area = RoomParams.GetRoundedArea(room).ToString();
                string areaK = RoomParams.GetRoundedAreaK(room).ToString();
                sb.Append(category).Append('|')
                  .Append(idint.ToString(CultureInfo.InvariantCulture)).Append('|')
                  .Append(area).Append('|')
                  .Append(areaK).Append('\n');
            }

            File.WriteAllText(filePath, sb.ToString());
            Logger.Log("Бэкап сохранён: " + filePath + ", помещений: " + rooms.Count, 1);
            return filePath;
        }

        private static string SanitizeName(string backupName)
        {
            string name = string.IsNullOrWhiteSpace(backupName) ? "Без имени" : backupName.Trim();
            name = name.Replace(",", " ");
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, ' ');
            }
            name = name.Trim();
            return name.Length == 0 ? "Без имени" : name;
        }
    }
}
