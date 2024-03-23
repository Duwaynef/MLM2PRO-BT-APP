using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using static MLM2PRO_BT_APP.HomeMenu;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;
using MaterialDesignColors.Recommended;
using System.Text.RegularExpressions;

namespace MLM2PRO_BT_APP.util
{
    public class SharedViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ShotData> ShotDataCollection { get; private set; } = new ObservableCollection<ShotData>();

        private static SharedViewModel? _instance;
        public static SharedViewModel? Instance => _instance ??= new SharedViewModel();

        public SharedViewModel()
        {

        }

        private string? _gsProStatus;
        public string? GSProStatus
        {
            get => _gsProStatus;
            set => SetProperty(ref _gsProStatus, value, nameof(GSProStatus));
        }

        private string? _gsProClub;
        public string? GSProClub
        {
            get => _gsProClub;
            set => SetProperty(ref _gsProClub, value, nameof(GSProClub));
        }

        private string? _lmStatus;
        public string? LMStatus
        {
            get => _lmStatus;
            set => SetProperty(ref _lmStatus, value, nameof(LMStatus));
        }

        private string? _puttingStatus;
        public string? PuttingStatus
        {
            get => _puttingStatus;
            set => SetProperty(ref _puttingStatus, value, nameof(PuttingStatus));
        }

        private string? _LmBatteryLife;
        public string? LmBatteryLife
        {
            get => _LmBatteryLife;
            set => SetProperty(ref _LmBatteryLife, value + "%", nameof(LmBatteryLife));
        }

        private SolidColorBrush? _PuttingStatusBackground;
        public SolidColorBrush? PuttingStatusBackground
        {
            get => _PuttingStatusBackground;
            set => SetProperty(ref _PuttingStatusBackground, value, nameof(PuttingStatusBackground));
        }

        private SolidColorBrush? _lmStatusBackground;
        public SolidColorBrush? LMStatusBackground
        {
            get => _lmStatusBackground;
            set => SetProperty(ref _lmStatusBackground, value, nameof(LMStatusBackground));
        }

        private SolidColorBrush? _lmBattLifeBackground;
        public SolidColorBrush? LMBattLifeBackground
        {
            get => _lmBattLifeBackground;
            set => SetProperty(ref _lmBattLifeBackground, value, nameof(LMBattLifeBackground));
        }

        private SolidColorBrush? _gsProStatusBackground;
        public SolidColorBrush? GSProStatusBackground
        {
            get => _gsProStatusBackground;
            set => SetProperty(ref _gsProStatusBackground, value, nameof(GSProStatusBackground));
        }

        private static SolidColorBrush? GetStatusColor(string status)
        {
            // Normalize the status string to lower case for case-insensitive comparison
            var lowerCaseStatus = status.ToLower();
            Color Cyan = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.Cyan];
            Color BlueGrey = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.BlueGrey];
            Color Red = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.Red];
            Color Green = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.Green];

            if (int.TryParse(status, out var batteryLevel))
            {
                if (batteryLevel < 15)
                {
                    return new SolidColorBrush(Red);
                }
                else if (batteryLevel <50)
                {
                    return new SolidColorBrush(Cyan);
                }
                else
                {
                    return new SolidColorBrush(Green);
                }
            }
            
            if (lowerCaseStatus.Contains("failed"))
            {
                return new SolidColorBrush(Red);
            }
            else if (lowerCaseStatus.Contains("disconnected"))
            {
                return new SolidColorBrush(Cyan);
            }
            else if (lowerCaseStatus.Contains("connected") || lowerCaseStatus.Contains("success"))
            {
                return new SolidColorBrush(Green);
            }
            else
            {
                return new SolidColorBrush(BlueGrey);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private bool SetProperty<T>(ref T storage, T value, string? propertyName)
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
                else if (propertyName == nameof(LmBatteryLife) && value is string lmBattLifeValue)
                {
                    var matches = string.Concat(Regex.Matches(lmBattLifeValue, @"\d+").Cast<Match>().Select(m => m.Value));
                    LMBattLifeBackground = GetStatusColor(matches);
                }
                else if (propertyName == nameof(PuttingStatus) && value is string puttingStatusValue)
                {
                    PuttingStatusBackground = GetStatusColor(puttingStatusValue);
                }
            });
            return true;
        }


        public event PropertyChangedEventHandler? PropertyChanged;
    }
}