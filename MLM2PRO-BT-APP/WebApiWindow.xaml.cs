using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP
{
    /// <summary>
    /// Interaction logic for WebApiWindow.xaml
    /// </summary>
    public partial class WebApiWindow : Window
    {
        public WebApiWindow()
        {
            InitializeComponent();
        }

        private void WebAPISaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (WebAPITextBox.Text != "")
            {
                Logger.Log("Save API Token called");
                // Save the token to the settings
                if (String.IsNullOrEmpty(WebAPITextBox.Text) || 
                    WebAPITextBox.Text.Contains("Secret") || 
                    WebAPITextBox.Text.Contains(" ") ||
                    WebAPITextBox.Text.Contains(":") ||
                    WebAPITextBox.Text.Length != 36)
                {
                    WebAPITextBox.Text = "Invalid Token Syntax";
                } 
                else
                {
                    SettingsManager.Instance.Settings.WebApiSettings.WebApiSecret = WebAPITextBox.Text;
                    SettingsManager.Instance.SaveSettings();
                    Logger.Log("API Token saved");
                    Task.Run(() =>
                    {
                        (Application.Current as App)?.ConnectGsProButton();
                    });
                    
                    Close();
                }
            }
        }
    }
}
