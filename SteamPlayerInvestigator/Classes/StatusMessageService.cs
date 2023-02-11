using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SteamPlayerInvestigator.Classes
{
    public class StatusMessageService : INotifyPropertyChanged
    {
        private string _statusMessage;
        public string StatusMessage
        {
            get { return _statusMessage; }
            set
            {
                _statusMessage = value;
                OnPropertyChanged("StatusMessage");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
