using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TNovCommon;

namespace TNovRooms
{
    public class ApartsNumAtLevelViewModel : INotifyPropertyChanged
    {
        
        private string _startvalue = "1";
        public string startvalue { get => _startvalue; set { _startvalue = value; OnPropertyChanged(); } }
        private bool _recalcnums = true;
        public bool recalcnums { get => _recalcnums; set { _recalcnums = value; OnPropertyChanged(); } }
        public RelayCommand NumerateCommand { get; set; }

        public ApartsNumAtLevelViewModel()
        {
            NumerateCommand = new RelayCommand(param => { Numerate(); }, CanNumerate);
        }
        public void Numerate()
        {
            Guid NRoomApartNumAtLevelParamGuid = new Guid("7cdb6adb-756e-4e5b-b4d0-5ccaf3cee047"); //N_Кв.НомерНаЭтаже

            RaiseHideRequest();
            int i = 1;
            int.TryParse(startvalue, out i);

            using (TransactionGroup group = new TransactionGroup(RevitAPI.Document, "TNov - Ручной нумератор квартир"))
            {
                ISelectionFilter _filter = new RoomSelectionFilter();
                group.Start();

                while (true)
                {
                    try
                    {
                        using (Transaction t = new Transaction(RevitAPI.Document, "TNov - Ручной нумератор квартир"))
                        {
                            t.Start();
                            Reference reference = RevitAPI.UiDocument.Selection.PickObject(ObjectType.Element, _filter, $"Выберите элемент {i}");
                            Autodesk.Revit.DB.Parameter parameter = RevitAPI.Document.GetElement(reference).get_Parameter(NRoomApartNumAtLevelParamGuid);
                            if (parameter != null)
                            {
                                parameter.Set(i);
                                t.Commit();
                            }
                            else
                            {
                                var info1 = new InfoWindow280($"Ошибка!\nУ элемента {reference.ElementId} нет параметра {parameter.Definition.Name}."); info1.ShowDialog();
                                t.Commit();
                                group.Assimilate();
                                break;
                            }
                        }
                    }
                    catch
                    {
                        group.Assimilate();
                        break;
                    }
                }
            }
            i++;
            startvalue = i.ToString();
            RaiseShowRequest();
        }

        private bool CanNumerate(object param)
        {
            return int.TryParse(startvalue, out _);
        }

        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler HideRequest;
        private void RaiseHideRequest()
        {
            HideRequest?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler ShowRequest;
        private void RaiseShowRequest()
        {
            ShowRequest?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
