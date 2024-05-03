using System.Windows;
using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP
{
    public partial class WebApiWindow
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
                if (string.IsNullOrEmpty(WebAPITextBox.Text) || 
                    WebAPITextBox.Text.Contains("Secret") || 
                    WebAPITextBox.Text.Contains(' ') ||
                    WebAPITextBox.Text.Contains(':') ||
                    WebAPITextBox.Text.Length != 36)
                {
                    WebAPITextBox.Text = "Invalid Token Syntax";
                } 
                else
                {
                    if (SettingsManager.Instance.Settings?.WebApiSettings != null)
                    {
                        //SettingsManager.Instance.Settings.WebApiSettings.WebApiSecret = WebAPITextBox.Text;
                        SettingsManager.Instance.SaveSettings();
                        Logger.Log("API Token saved");
                        Task.Run(() =>
                        {
                            (Application.Current as App)?.ConnectAndSetupBluetooth();
                        });
                        Close();

                    }
                }
            }
        }
    }
}
