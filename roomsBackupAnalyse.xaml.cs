using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections;
using System.Windows.Navigation;
using System.Security.Cryptography.X509Certificates;

using Autodesk.Revit.UI;
using Newtonsoft.Json;
using TNovCommon;
using System.Globalization;

namespace TNovRooms
{
    /// <summary>
    /// Логика взаимодействия для roomsBackupAnalyse.xaml
    /// </summary>
    public partial class roomsBackupAnalyse : Window
    {
        public string scenario = "0";
        public roomsBackupAnalyse(List<TNovRoom>tNovRooms,string backupName)
        {
            InitializeComponent();

            headBlock.Text += backupName;

            this.SizeToContent = SizeToContent.Height;

            StackPanel sp02 = new StackPanel(); sp02.Orientation = Orientation.Horizontal;
            var catTitle = new TextBlock { Text = "Категория", Margin = new Thickness(5, 5, 5, 5), Width = 80, }; sp02.Children.Add(catTitle);
            var gNumTitle = new TextBlock { Text = "№кв/офиса", Margin = new Thickness(5, 5, 5, 5), Width = 80, }; sp02.Children.Add(gNumTitle);
            var idTitle = new TextBlock { Text = "ID", Margin = new Thickness(5, 5, 5, 5), Width = 80, }; sp02.Children.Add(idTitle);
            var nameTitle = new TextBlock { Text = "Имя", Margin = new Thickness(5, 5, 5, 5), Width = 150, }; sp02.Children.Add(nameTitle);
            var sMTitle = new TextBlock { Text = "S", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(sMTitle);
            var sKMTitle = new TextBlock { Text = "Sк", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(sKMTitle);
            var sBTitle = new TextBlock { Text = "S(б)", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(sBTitle);
            var sKBTitle = new TextBlock { Text = "Sк(б)", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(sKBTitle);
            var buttonTitle = new TextBlock { Text = "Действие", Margin = new Thickness(5, 5, 5, 5), Width = 150, }; sp02.Children.Add(buttonTitle);
            sp0.Children.Add(sp02);

            int apartsCount = 0; int officesCount = 0; int otherCount = 0; int storeroomCount = 0;

            
            foreach (var tNovRoom in tNovRooms)
            {
                //заголовки для группировки по типу
                if (tNovRoom.RoomCategory == "Квартиры" && apartsCount == 0)
                {
                    StackPanel spHead = new StackPanel(); spHead.Orientation = Orientation.Horizontal;
                    var spHeadNameBlock = new TextBlock { Text = "Квартиры", TextWrapping = TextWrapping.Wrap, Width = 150, Margin = new Thickness(5, 5, 5, 5) };
                    spHead.Children.Add(spHeadNameBlock); sp0.Children.Add(spHead);
                    apartsCount++;
                }
                if (tNovRoom.RoomCategory == "Офисы" && officesCount == 0)
                {
                    StackPanel spHead = new StackPanel(); spHead.Orientation = Orientation.Horizontal;
                    var spHeadNameBlock = new TextBlock { Text = "Офисы", TextWrapping = TextWrapping.Wrap, Width = 150, Margin = new Thickness(5, 5, 5, 5) };
                    spHead.Children.Add(spHeadNameBlock); sp0.Children.Add(spHead);
                    officesCount++;
                }
                if (tNovRoom.RoomCategory == "Кладовые" && storeroomCount == 0)
                {
                    StackPanel spHead = new StackPanel(); spHead.Orientation = Orientation.Horizontal;
                    var spHeadNameBlock = new TextBlock { Text = "Кладовые", TextWrapping = TextWrapping.Wrap, Width = 150, Margin = new Thickness(5, 5, 5, 5) };
                    spHead.Children.Add(spHeadNameBlock); sp0.Children.Add(spHead);
                    storeroomCount++;
                }
                if (tNovRoom.RoomCategory == "Прочие" && otherCount == 0)
                {
                    StackPanel spHead = new StackPanel(); spHead.Orientation = Orientation.Horizontal;
                    var spHeadNameBlock = new TextBlock { Text = "Прочие", TextWrapping = TextWrapping.Wrap, Width = 150, Margin = new Thickness(5, 5, 5, 5) };
                    spHead.Children.Add(spHeadNameBlock); sp0.Children.Add(spHead);
                    otherCount++;
                }

                //строка для tNovRoom

                StackPanel sp = new StackPanel(); sp.Orientation = Orientation.Horizontal; sp.Background = new SolidColorBrush(Colors.MintCream);
                var catBlock = new TextBlock { Text = tNovRoom.RoomCategory, TextWrapping = TextWrapping.Wrap, Width = 80, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(catBlock);
                var gNumBlock = new TextBlock { Text = tNovRoom.RoomGroupNumber.ToString(), TextWrapping = TextWrapping.Wrap, Width = 80, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(gNumBlock);
                var idBlock = new TextBlock { Text = tNovRoom.RoomId, TextWrapping = TextWrapping.Wrap, Width = 80, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(idBlock);
                var nameBlock = new TextBlock { Text = tNovRoom.RoomName, TextWrapping = TextWrapping.Wrap, Width = 150, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(nameBlock);
                var smBlock = new TextBlock { Text = tNovRoom.RoomModelS, TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(smBlock);
                var sKMBlock = new TextBlock { Text = tNovRoom.RoomModelSK, TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(sKMBlock);
                var sBBlock = new TextBlock { Text = tNovRoom.RoomBackupS, TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(sBBlock);
                var sKBBlock = new TextBlock { Text = tNovRoom.RoomBackupSK, TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(sKBBlock);
                
                var btn = new Button
                { Content = "Восстановить", Width = 150, Height = 25, Margin = new Thickness(5, 5, 5, 5), VerticalAlignment = VerticalAlignment.Center, Tag = tNovRoom.RoomId, };
                sp.Children.Add(btn); btn.Click += new RoutedEventHandler(replace_Click);
                
                sp0.Children.Add(sp);
            }
        }

        private void replace_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            scenario = button.Tag.ToString();

            DialogResult = true;
            this.Close(); // закрытие окна


        }
        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close(); // закрытие окна
        }
        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            
            this.Close(); // закрытие окна
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = @"https://portal.talan.group/knowledge/proektirovanie/pomeshcheniyarezervnoekopirovanieivosstanovlenie/";
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }
    }
    
}
