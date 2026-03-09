using System.Windows;
using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP
{
    public partial class SettingsPage
    {
        private SettingsManager? SettingsManager { get; } = SettingsManager.Instance;
        public SettingsManager.AppSettings? Settings
        {
            get => SettingsManager?.Settings;
        }
        public SettingsPage()
        {
            InitializeComponent();
            DataContext = SettingsManager.Instance;
            SettingsManager.Instance.SettingsUpdated += (s, e) => RefreshDataContext();
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
            SettingsManager.Instance.ClearSettings();
        }
        private void Settings_SaveSettings_Button(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.SaveSettings();
        }
        private void Settings_GSPRO_Button(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.GSProSettingsUpdated();
        }
        private void Settings_InfiniteTees_Button(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.InfiniteTeesSettingsUpdated();
        }

        private void ToggleWebApiDetails_Click(object sender, RoutedEventArgs e)
        {
            if (WebApiDetailsPanel.Visibility == Visibility.Collapsed)
            {
                WebApiDetailsPanel.Visibility = Visibility.Visible;
                ToggleWebApiDetailsButton.Content = "Hide Details";
            }
            else
            {
                WebApiDetailsPanel.Visibility = Visibility.Collapsed;
                ToggleWebApiDetailsButton.Content = "Show Details";
            }
        }
    }
}
