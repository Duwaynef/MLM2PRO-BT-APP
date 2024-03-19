using System.Windows;
using System.Windows.Controls;
using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        private SettingsManager? SettingsManager { get; } = SettingsManager.Instance;
        public SettingsManager.AppSettings? Settings => SettingsManager?.Settings;
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
                DataContext = null; // Clear existing DataContext
                DataContext = SettingsManager.Instance; // Re-assign DataContext to refresh bindings
            });
        }


        private void Settings_ClearSettings_Button(object sender, RoutedEventArgs e)
        {
            SettingsManager.Instance.ClearSettings();
        }

        private void SettingsManager_SettingsUpdated(object sender, EventArgs e)
        {
        }

        private void Settings_SaveSettings_Button(object sender, RoutedEventArgs e)
        {
            // You'll need to implement a method to update the settings object from the UI
            // and then save it using SettingsManager
            SettingsManager.Instance.SaveSettings();
        }
    }
}
