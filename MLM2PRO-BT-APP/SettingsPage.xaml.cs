using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.UI.ApplicationSettings;

namespace MLM2PRO_BT_APP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsManager SettingsManager { get; } = SettingsManager.Instance;
        public AppSettings Settings => SettingsManager.Settings;
        public SettingsPage()
        {
            InitializeComponent();
            DataContext = SettingsManager.Instance;
        }

        private void Settings_ClearSettings_Button(object sender, RoutedEventArgs e)
        {
            SettingsManager.ClearSettings();
        }

        private void Settings_SaveSettings_Button(object sender, RoutedEventArgs e)
        {
            // You'll need to implement a method to update the settings object from the UI
            // and then save it using SettingsManager
            SettingsManager.Instance.SaveSettings();
        }
    }
}
