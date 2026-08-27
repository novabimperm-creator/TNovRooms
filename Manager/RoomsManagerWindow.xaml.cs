using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TNovCommon;

namespace TNovRooms.Manager
{
    /// <summary>Вкладка, на которой открывается менеджер.</summary>
    public enum ManagerTab
    {
        Rounding,
        Aparts,
        Storages
    }

    /// <summary>Ручной нумератор, который нужно открыть после закрытия менеджера.</summary>
    public enum ManualNumberingKind
    {
        None,
        ApartAtLevel,
        RoomNumber
    }

    public partial class RoomsManagerWindow : Window
    {
        private readonly RoomsManagerContext _context;
        private readonly ManagerTab _startTab;

        private GateControl _gateControl;
        private RoundingControl _roundingControl;
        private ApartsControl _apartsControl;
        private OfficesControl _officesControl;
        private StoragesControl _storagesControl;

        public RoomsManagerWindow(RoomsManagerContext context) : this(context, ManagerTab.Rounding)
        {
        }

        public RoomsManagerWindow(RoomsManagerContext context, ManagerTab startTab)
        {
            InitializeComponent();
            _context = context;
            _startTab = startTab;
            UpdateBackupInfo();

            if (_context.StagesResolved)
            {
                UnlockFunctions();
            }
            else
            {
                SetNavEnabled(false);
                _gateControl = new GateControl(_context, this);
                FunctionContent.Content = _gateControl;
            }
        }

        public RoomsManagerContext Context => _context;

        /// <summary>Ручной нумератор, запрошенный пользователем перед закрытием окна.</summary>
        public ManualNumberingKind ManualNumberingRequest { get; private set; } = ManualNumberingKind.None;

        /// <summary>Вызывается GateControl, когда этапы 1-2 пройдены.</summary>
        public void OnStagesCompleted()
        {
            UnlockFunctions();
        }

        /// <summary>Вызывается GateControl, если пользователь отказался проходить этапы.</summary>
        public void CancelByUser()
        {
            DialogResult = false;
            Close();
        }

        public void UpdateBackupInfo()
        {
            RoomAreaBackup latest = _context.LatestBackup;
            TbBackupInfo.Text = latest == null
                ? "Бэкапы площадей: не найдены"
                : "Бэкапы площадей: " + _context.Backups.Count + "\nПоследний: " + latest.DisplayName;
        }

        /// <summary>
        /// Ручные нумераторы выбирают помещения в модели, поэтому модальное окно менеджера
        /// должно закрыться. Команда откроет нумератор и вернёт менеджер обратно.
        /// </summary>
        public void RequestManualNumbering(ManualNumberingKind kind)
        {
            ManualNumberingRequest = kind;
            DialogResult = true;
            Close();
        }

        private void UnlockFunctions()
        {
            SetNavEnabled(true);
            NavLockedHint.Visibility = Visibility.Collapsed;
            _gateControl = null;

            switch (_startTab)
            {
                case ManagerTab.Aparts: ShowAparts(); break;
                case ManagerTab.Storages: ShowStorages(); break;
                default: ShowRounding(); break;
            }
        }

        private void SetNavEnabled(bool enabled)
        {
            BtnRounding.IsEnabled = enabled;
            BtnAparts.IsEnabled = enabled;
            BtnOffices.IsEnabled = enabled;
            BtnStorages.IsEnabled = enabled;
            BtnCoefficients.IsEnabled = enabled;
        }

        private void ShowRounding()
        {
            if (_roundingControl == null) _roundingControl = new RoundingControl(_context, this);
            FunctionContent.Content = _roundingControl;
            HighlightButton(BtnRounding);
        }

        private void ShowAparts()
        {
            if (_apartsControl == null) _apartsControl = new ApartsControl(_context, this);
            FunctionContent.Content = _apartsControl;
            HighlightButton(BtnAparts);
        }

        private void ShowStorages()
        {
            if (_storagesControl == null) _storagesControl = new StoragesControl(_context, this);
            FunctionContent.Content = _storagesControl;
            HighlightButton(BtnStorages);
        }

        private void BtnRounding_Click(object sender, RoutedEventArgs e)
        {
            ShowRounding();
        }

        private void BtnAparts_Click(object sender, RoutedEventArgs e)
        {
            ShowAparts();
        }

        private void BtnOffices_Click(object sender, RoutedEventArgs e)
        {
            if (_officesControl == null) _officesControl = new OfficesControl(_context, this);
            FunctionContent.Content = _officesControl;
            HighlightButton(BtnOffices);
        }

        private void BtnStorages_Click(object sender, RoutedEventArgs e)
        {
            ShowStorages();
        }

        private void BtnCoefficients_Click(object sender, RoutedEventArgs e)
        {
            FunctionContent.Content = new PlaceholderControl(_context, this);
            HighlightButton(BtnCoefficients);
        }

        private void HighlightButton(Button activeButton)
        {
            var buttons = new[] { BtnRounding, BtnAparts, BtnOffices, BtnStorages, BtnCoefficients };
            var selectedBrush = (SolidColorBrush)FindResource("SelectedBrush");

            foreach (var btn in buttons)
            {
                btn.Background = Brushes.Transparent;
                btn.ClearValue(BorderBrushProperty);
                btn.ClearValue(BorderThicknessProperty);
            }
            activeButton.Background = selectedBrush;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = HelpLinks.GetHelpLink("Помещения");
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = _context.StagesResolved;
            Close();
        }
    }
}
