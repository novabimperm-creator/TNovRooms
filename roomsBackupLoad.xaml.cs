using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;
using TNovCommon;

namespace TNovRooms
{
    /// <summary>
    /// Логика взаимодействия для roomsBackupLoad.xaml
    /// </summary>
    public partial class roomsBackupLoad : Window
    {
        public string SelectedFilePath = "-";

        public roomsBackupLoad(string[] filesFromPath, string modelName)
        {
            InitializeComponent();
            LoadFileList(filesFromPath, modelName);
            this.SizeToContent = SizeToContent.Height;
        }

        private void LoadFileList(IEnumerable<string> filePaths, string modelName)
        {
            var fileItems = new List<FileItem>();
            foreach (var path in filePaths)
            {
                fileItems.Add(new FileItem(path, modelName));
            }
            comboBox.ItemsSource = fileItems;

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private void openButton_Click(object sender, RoutedEventArgs e)
        {
            if (comboBox.SelectedItem is FileItem selectedFile)
            {
                SelectedFilePath = selectedFile.FullPath;
                DialogResult = true;
            }
            else DialogResult = false;
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

    // Вспомогательный класс для отображения имени файла
    public class FileItem
    {
        public string FileName { get; }
        public string FullPath { get; }

        public FileItem(string fullPath, string modelName)
        {
            FullPath = fullPath;
            string strToReplace = "," + modelName + ",";
            FileName = Path.GetFileName(fullPath).Replace(strToReplace," ").Replace(".txt","");
        }
    }
}
