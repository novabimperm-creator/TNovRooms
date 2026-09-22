using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovRooms
{
    /// <summary>
    /// Логика взаимодействия для roomsBackupSave.xaml
    /// </summary>
    public partial class roomsBackupSave : Window
    {
        public string backupName = "-";

        public roomsBackupSave()
        {
            InitializeComponent();
            this.SizeToContent = SizeToContent.Height;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            backupName = textBox1.Text;
            DialogResult = true;
            this.Close();
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            HelpLinks.ShowHelp("Помещения Резервные копии");
        }
    }
}