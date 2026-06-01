using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TNovCommon;

namespace TNovRooms
{
    public class RoomsViewModel : INotifyPropertyChanged
    {
        public int selection { get; set; }

        private ICommand _scenario1;
        public ICommand scenario1
        {
            get
            {
                if (_scenario1 == null)
                {
                    _scenario1 = new RelayCommand(param => { selection = 1; }, CanExecute);
                }
                return _scenario1;
            }
        }
        private ICommand _scenario2;
        public ICommand scenario2
        {
            get
            {
                if (_scenario2 == null)
                {
                    _scenario2 = new RelayCommand(param => { selection = 2; }, CanExecute);
                }
                return _scenario2;
            }
        }
        private bool _recalc = true;
        public bool recalc { get => _recalc; set { _recalc = value; OnPropertyChanged(); } }

        private string _k03 = "Балкон,Французский балкон,Терраса";
        public string k03
        {
            get => _k03;
            set
            {
                _k03 = value;
                OnPropertyChanged();
            }
        }
        private string _k05 = "Лоджия";
        public string k05
        {
            get => _k05;
            set
            {
                _k05 = value;
                OnPropertyChanged();
            }
        }

        private string _names1 = "Лестница,лестница,Лестничная клетка,лестничная клетка";
        public string names1
        {
            get => _names1;
            set
            {
                _names1 = value;
                OnPropertyChanged();
            }
        }
        private string _names2 = "Коридор,Тамбур,Холл,Электрощитовая,Венткамера,Терраса";
        public string names2
        {
            get => _names2;
            set
            {
                _names2 = value;
                OnPropertyChanged();
            }
        }

        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
        private bool CanExecute(object param)
        {
            return true;
        }
    }
}
