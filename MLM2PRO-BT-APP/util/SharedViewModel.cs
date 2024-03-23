using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using static MLM2PRO_BT_APP.HomeMenu;
using MaterialDesignColors;
using System.Text.RegularExpressions;

namespace MLM2PRO_BT_APP.util
{
    public sealed partial class SharedViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<ShotData> ShotDataCollection { get; private set; } = [];

        private static SharedViewModel? _instance;
        public static SharedViewModel Instance
        {
            get => _instance ??= new SharedViewModel();
        }

        private string? _gsProStatus;
        public string? GsProStatus
        {
            get => _gsProStatus;
            set => SetProperty(ref _gsProStatus, value, nameof(GsProStatus));
        }

        private string? _gsProClub;
        public string? GsProClub
        {
            get => _gsProClub;
            set => SetProperty(ref _gsProClub, value, nameof(GsProClub));
        }

        private string? _lmStatus;
        public string? LmStatus
        {
            get => _lmStatus;
            set => SetProperty(ref _lmStatus, value, nameof(LmStatus));
        }

        private string? _puttingStatus;
        public string? PuttingStatus
        {
            get => _puttingStatus;
            set => SetProperty(ref _puttingStatus, value, nameof(PuttingStatus));
        }

        private string? _lmBatteryLife;
        public string? LmBatteryLife
        {
            get => _lmBatteryLife;
            set => SetProperty(ref _lmBatteryLife, value + "%", nameof(LmBatteryLife));
        }

        private SolidColorBrush? _puttingStatusBackground;
        public SolidColorBrush? PuttingStatusBackground
        {
            get => _puttingStatusBackground;
            set => SetProperty(ref _puttingStatusBackground, value, nameof(PuttingStatusBackground));
        }

        private SolidColorBrush? _lmStatusBackground;
        public SolidColorBrush? LmStatusBackground
        {
            get => _lmStatusBackground;
            set => SetProperty(ref _lmStatusBackground, value, nameof(LmStatusBackground));
        }

        private SolidColorBrush? _lmBattLifeBackground;
        public SolidColorBrush? LmBattLifeBackground
        {
            get => _lmBattLifeBackground;
            set => SetProperty(ref _lmBattLifeBackground, value, nameof(LmBattLifeBackground));
        }

        private SolidColorBrush? _gsProStatusBackground;
        public SolidColorBrush? GsProStatusBackground
        {
            get => _gsProStatusBackground;
            set => SetProperty(ref _gsProStatusBackground, value, nameof(GsProStatusBackground));
        }

        private static SolidColorBrush GetStatusColor(string status)
        {
            // Normalize the status string to lower case for case-insensitive comparison
            var lowerCaseStatus = status.ToLower();
            Color cyan = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.Cyan];
            Color blueGrey = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.BlueGrey];
            Color red = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.Red];
            Color green = SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.Green];

            if (int.TryParse(status, out var batteryLevel))
            {
                if (batteryLevel < 15)
                {
                    return new SolidColorBrush(red);
                }
                else if (batteryLevel <50)
                {
                    return new SolidColorBrush(cyan);
                }
                else
                {
                    return new SolidColorBrush(green);
                }
            }
            
            if (lowerCaseStatus.Contains("failed"))
            {
                return new SolidColorBrush(red);
            }
            else if (lowerCaseStatus.Contains("disconnected"))
            {
                return new SolidColorBrush(cyan);
            }
            else if (lowerCaseStatus.Contains("connected") || lowerCaseStatus.Contains("success"))
            {
                return new SolidColorBrush(green);
            }
            else
            {
                return new SolidColorBrush(blueGrey);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void SetProperty<T>(ref T storage, T value, string? propertyName)
        {
            if (Equals(storage, value)) return;

            storage = value;
            // Dispatch the property change event to the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                OnPropertyChanged(propertyName);

                // Update background colors for specific properties
                if (propertyName == nameof(GsProStatus) && value is string gsProStatusValue)
                {
                    GsProStatusBackground = GetStatusColor(gsProStatusValue);
                }
                else if (propertyName == nameof(LmStatus) && value is string lmStatusValue)
                {
                    LmStatusBackground = GetStatusColor(lmStatusValue);
                }
                else if (propertyName == nameof(LmBatteryLife) && value is string lmBattLifeValue)
                {
                    string matches = string.Concat(MyRegex().Matches(lmBattLifeValue).Select(m => m.Value));
                    LmBattLifeBackground = GetStatusColor(matches);
                }
                else if (propertyName == nameof(PuttingStatus) && value is string puttingStatusValue)
                {
                    PuttingStatusBackground = GetStatusColor(puttingStatusValue);
                }
            });
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        [GeneratedRegex(@"\d+")]
        private static partial Regex MyRegex();
    }
}