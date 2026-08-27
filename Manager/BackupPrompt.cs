using System;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>
    /// Предложение сохранить новый бэкап площадей после переокругления.
    /// Один диалог для Округлятора и Квартирографии.
    /// </summary>
    public static class BackupPrompt
    {
        /// <summary>
        /// Возвращает true, если бэкап сохранён: список бэкапов в контексте при этом уже перечитан.
        /// </summary>
        public static bool OfferSave(RoomsManagerContext context, RoomsManagerWindow window, string resultText)
        {
            var qViewModel = new QuestionWindowViewModel
            {
                headtxt = resultText + " Сохранить новые площади в новый бэкап?"
            };
            var qwpfview = new QuestionWindow280(qViewModel);
            qViewModel.CloseRequest += (s, args) => qwpfview.Close();
            bool? ok = qwpfview.ShowDialog();
            if (ok != true)
            {
                Logger.Log("Пользователь отказался сохранять бэкап", 1);
                return false;
            }

            var saveWindow = new roomsBackupSave();
            bool? saved = saveWindow.ShowDialog();
            if (saved != true)
            {
                Logger.Log("Сохранение бэкапа отменено", 1);
                return false;
            }

            try
            {
                RoomAreaBackupStore.Save(context.Config, context.DocName, saveWindow.backupName, context.Rooms);
            }
            catch (Exception ex)
            {
                Logger.Log("Ошибка при сохранении бэкапа: " + ex.Message, 4);
                new InfoWindow280("Не удалось сохранить бэкап: " + ex.Message).ShowDialog();
                return false;
            }

            context.ReloadBackups();
            window.UpdateBackupInfo();
            new InfoWindow280("Бэкап сохранён. Помещений в файле: " + context.Rooms.Count + ".").ShowDialog();
            return true;
        }
    }
}
