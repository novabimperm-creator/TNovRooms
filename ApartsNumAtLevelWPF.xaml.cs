using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovRooms
{
    /// <summary>
    /// Логика взаимодействия для ApartsNumAtLevelWPF.xaml
    /// </summary>
    public partial class ApartsNumAtLevelWPF : Window
    {
        public ApartsNumAtLevelWPF(ApartsNumAtLevelViewModel viewModel)
        {
            InitializeComponent();
            textBox1.Focus();
            DataContext = viewModel;

            this.SizeToContent = SizeToContent.Height;
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
            string commandText = HelpLinks.GetHelpLink("Квартирография"); 
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
    }
}
