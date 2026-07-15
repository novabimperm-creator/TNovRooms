using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovRooms
{
    /// <summary>
    /// Логика взаимодействия для roomsBackupStart.xaml
    /// </summary>
    public partial class roomsBackupStart : Window
    {
        public int scenario = 0;

        public roomsBackupStart()
        {
            InitializeComponent();
            this.SizeToContent = SizeToContent.Height;
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            scenario = 1;
            DialogResult = true;
            this.Close();
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            scenario = 2;
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
            string commandText = HelpLinks.GetHelpLink("Помещения Резервные копии");
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
    }
}