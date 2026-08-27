using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;

namespace TNovRooms
{
    public class RoomsBackupAnalyseViewModel
    {
        public string BackupName { get; }

        public ICollectionView Rows { get; }

        public RoomsBackupAnalyseViewModel(List<TNovRoom> rooms, string backupName)
        {
            BackupName = backupName;

            var view = CollectionViewSource.GetDefaultView(rooms);
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TNovRoom.RoomCategory)));
            Rows = view;
        }
    }
}
