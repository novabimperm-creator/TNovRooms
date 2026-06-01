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
    public class RoomsNumViewModel : INotifyPropertyChanged
    {
        private string _startvalue = "1";
        public string startvalue { get => _startvalue; set { _startvalue = value; OnPropertyChanged(); } }

        public RelayCommand NumerateCommand { get; set; }

        public RoomsNumViewModel()
        {
            NumerateCommand = new RelayCommand(param => { Numerate(); }, CanNumerate);
        }
        public void Numerate()
        {
            //параметры
            BuiltInParameter roomNumber = BuiltInParameter.ROOM_NUMBER; //Номер

            RaiseHideRequest();
            int i = 1;
            int.TryParse(startvalue, out i);
            using (TransactionGroup group = new TransactionGroup(RevitAPI.Document, "TNov - Ручной нумератор помещений"))
            {
                ISelectionFilter _filter = new RoomSelectionFilter();
                group.Start();

                while (true)
                {
                    try
                    {
                        using (Transaction t = new Transaction(RevitAPI.Document, "TNov - Ручной нумератор помещений"))
                        {
                            t.Start();
                            TransactionHandler.SetWarningResolver(t);
                            Reference reference = RevitAPI.UiDocument.Selection.PickObject(ObjectType.Element, _filter, $"Выберите элемент {i}");
                            Autodesk.Revit.DB.Parameter parameter = RevitAPI.Document.GetElement(reference).get_Parameter(roomNumber);
                            if (parameter != null)
                            {
                                parameter.Set(i.ToString());
                                i++;
                                t.Commit();
                            }
                            else
                            {
                                new InfoWindow280($"Ошибка!\nУ элемента {reference.ElementId} нет параметра {parameter.Definition.Name}.").ShowDialog();
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
