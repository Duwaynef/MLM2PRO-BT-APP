using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using static MLM2PRO_BT_APP.HomeMenu;

namespace MLM2PRO_BT_APP
{
    public class SharedViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ShotData> ShotDataCollection { get; private set; } = new ObservableCollection<ShotData>();

        private static SharedViewModel _instance;
        public static SharedViewModel Instance => _instance ??= new SharedViewModel();

        private string _gsProStatus;
        public string GSProStatus
        {
            get => _gsProStatus;
            set => SetProperty(ref _gsProStatus, value, nameof(GSProStatus));
        }

        private string _lmStatus;
        public string LMStatus
        {
            get => _lmStatus;
            set => SetProperty(ref _lmStatus, value, nameof(LMStatus));
        }

        private string _lmBattLife;
        public string LMBattLife
        {
            get => _lmBattLife;
            set => SetProperty(ref _lmBattLife, value, nameof(LMBattLife));
        }

        private SolidColorBrush _lmStatusBackground;
        public SolidColorBrush LMStatusBackground
        {
            get => _lmStatusBackground;
            set => SetProperty(ref _lmStatusBackground, value, nameof(LMStatusBackground));
        }

        private SolidColorBrush _lmBattLifeBackground;
        public SolidColorBrush LMBattLifeBackground
        {
            get => _lmBattLifeBackground;
            set => SetProperty(ref _lmBattLifeBackground, value, nameof(LMBattLifeBackground));
        }

        private SolidColorBrush _gsProStatusBackground;
        public SolidColorBrush GSProStatusBackground
        {
            get => _gsProStatusBackground;
            set => SetProperty(ref _gsProStatusBackground, value, nameof(GSProStatusBackground));
        }

        private SolidColorBrush GetStatusColor(string status)
        {
            // Normalize the status string to lower case for case-insensitive comparison
            string lowerCaseStatus = status.ToLower();
            int batteryLevel;
            if (int.TryParse(status, out batteryLevel))
            {
            }

            if (lowerCaseStatus.Contains("disconnected") || batteryLevel < 50)
            {
                return new SolidColorBrush(Colors.DarkCyan);
            }
            else if (lowerCaseStatus.Contains("failed") || batteryLevel < 15)
            {
                return new SolidColorBrush(Colors.DarkRed);
            }
            else if (lowerCaseStatus.Contains("connected") || lowerCaseStatus.Contains("success") || batteryLevel > 50)
            {
                return new SolidColorBrush(Colors.Green);
            }
            else
            {
                return new SolidColorBrush(Colors.Transparent);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected bool SetProperty<T>(ref T storage, T value, string propertyName)
        {
            if (Equals(storage, value)) return false;

            storage = value;
            // Dispatch the property change event to the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(propertyName);

                // Update background colors for specific properties
                if (propertyName == nameof(GSProStatus) && value is string gsProStatusValue)
                {
                    GSProStatusBackground = GetStatusColor(gsProStatusValue);
                }
                else if (propertyName == nameof(LMStatus) && value is string lmStatusValue)
                {
                    LMStatusBackground = GetStatusColor(lmStatusValue);
                }
                else if (propertyName == nameof(LMBattLife) && value is string lmBattLifeValue)
                {
                    LMBattLifeBackground = GetStatusColor(lmBattLifeValue);
                }
            });
            return true;
        }


        public event PropertyChangedEventHandler PropertyChanged;
    }
}