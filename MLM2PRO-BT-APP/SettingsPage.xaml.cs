using System.Windows;
using System.Windows.Controls;
using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class SettingsPage : Page
    {
        private SettingsManager? SettingsManager { get; } = SettingsManager.Instance;
        public SettingsManager.AppSettings? Settings => SettingsManager?.Settings;
        public SettingsPage()
        {
            InitializeComponent();
            DataContext = SettingsManager.Instance;
            if (SettingsManager.Instance != null) SettingsManager.Instance.SettingsUpdated += (s, e) => RefreshDataContext();
        }
        private void RefreshDataContext()
        {
            Dispatcher.Invoke(() =>
            {
                DataContext = null;
                DataContext = SettingsManager.Instance;
            });
        }
        private void Settings_ClearSettings_Button(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance?.ClearSettings();
        }
        private void Settings_SaveSettings_Button(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance?.SaveSettings();
        }
    }
}
