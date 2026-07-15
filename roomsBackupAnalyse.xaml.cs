using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TNovCommon;

namespace TNovRooms
{
    /// <summary>
    /// Логика взаимодействия для roomsBackupAnalyse.xaml
    /// </summary>
    public partial class roomsBackupAnalyse : Window
    {
        public string scenario = "0";

        public roomsBackupAnalyse(List<TNovRoom> tNovRooms, string backupName)
        {
            InitializeComponent();
            DataContext = new RoomsBackupAnalyseViewModel(tNovRooms, backupName);
            SizeToContent = SizeToContent.Height;
        }

        private void replace_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                scenario = button.Tag?.ToString() ?? "0";
                DialogResult = true;
                Close();
            }
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            scenario = "0";
            DialogResult = true;
            Close();
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
