using System.Windows;
using System.Windows.Controls;
using MLM2PRO_BT_APP.connections;
using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP;

public partial class HomeMenu : Page
{
    public HomeMenu()
    {
        InitializeComponent();
        this.DataContext = App.SharedVm;
        // ShotDataDataGrid.ItemsSource = App.SharedVM.ShotDataCollection;
        ShotDataDataGrid.ItemsSource = SharedViewModel.Instance.ShotDataCollection;
    }

    private async void GSPro_Connect_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.ConnectGsProButton();
    }
    private async void GSPro_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.DisconnectGsPro();
    }
    private async void GSPro_Send_TestShot_Click(object sender, RoutedEventArgs e)
    {
        (Application.Current as App)?.SendTestShotData();
    } 
    
    /* interesting code to hold on to
    private async Task Write05(object sender, RoutedEventArgs e)
    {
        byte[] byteArray = byteConversionUtils.StringToByteArray("010000000001488384561d45c4f21a079de55c24df9c7167f10d1964f2179bc8170dfbe2453a");
        byte[] byteKeyOutput = await manager.ConvertAuthRequest(byteArray);
        Logger.Log("Key: " + byteConversionUtils.ByteArrayToHexString(byteKeyOutput));

        // second decrypt attempt
        byte[] byteKeyOutput2 = byteConversionUtils.StringToByteArray("488384561D45C4F21A079DE55C24DF9C7167F10D1964F2179BC8170DFBE2453A");
        byte[] byteArray2 = byteConversionUtils.StringToByteArray("1b001070acd281e3ffff000000000900010100040082032b000000012e2700230004001b1100d5d19ddaf553e26e3d2ad9b478beff33e4baf6b7477854bb4e8354f92c8a3328");
        byte[] outputByteArr = btEncryption.DecryptKnownKey(byteArray2, byteKeyOutput2);
        Logger.Log("Decrypted Bytes: " + byteConversionUtils.ByteArrayToHexString(outputByteArr));
    }
    private void DecryptTextBox(object sender, RoutedEventArgs e)
    {
        String textBoxInput = "";//ConsoleInputTextBox.Text;
        String keyTextBoxInput = "";// KeyTextBox.Text;
        byte[] byteArray = byteConversionUtils.StringToByteArray(textBoxInput);
        byte[] byteArray2 = byteConversionUtils.StringToByteArray(keyTextBoxInput);

        byte[] outputByteArr = btEncryption.DecryptKnownKey(byteArray, byteArray2);
        Logger.Log("Decrypted Bytes: " + byteConversionUtils.ByteArrayToHexString(outputByteArr));
    }
    */
    
    public class ShotData
    {
        public int ShotNumber { get; set; }
        public string Result { get; set; } = "";
        public string Club { get; set; } = "";
        public double ClubSpeed { get; set; }
        public double BallSpeed { get; set; } 
        public double SpinAxis { get; set; }
        public double SpinRate { get; set; }
        public double HLA { get; set; }
        public double VLA { get; set; }
        public double BackSpin { get; set; }
        public double SideSpin { get; set; }

        //public double ClubPath { get; set; }
        //public double ImpactAngle { get; set; }
    }
    private async void LM_WebApiTest_Click(object sender, RoutedEventArgs e)
    {
        App.SharedVm.LMStatus = "TESTING WEBAPI";
        
        WebApiClient webApiClient = new WebApiClient();
        Logger.Log("WebApiTest_Click: UserToken: " + SettingsManager.Instance.Settings.WebApiSettings.WebApiToken);
        Logger.Log("WebApiTest_Click: UserId: " + SettingsManager.Instance.Settings.WebApiSettings.WebApiUserId);
        WebApiClient.ApiResponse response = await webApiClient.SendRequestAsync(SettingsManager.Instance.Settings.WebApiSettings.WebApiUserId);

        if (response.Success)
        {
            SettingsManager.Instance.Settings.WebApiSettings.WebApiDeviceId = response.User.Id;
            SettingsManager.Instance.Settings.WebApiSettings.WebApiToken = response.User.Token;
            SettingsManager.Instance.Settings.WebApiSettings.WebApiExpireDate = response.User.ExpireDate;
            SettingsManager.Instance.SaveSettings();
            Logger.Log($"User ID: {response.User.Id}, Token: {response.User.Token}, Expire Date: {response.User.ExpireDate}");
            App.SharedVm.LMStatus = "WEBAPI SUCCESS";
            
        }
        else
        {
            App.SharedVm.LMStatus = "WEBAPI FAILED";
            Logger.Log("Failed to get a valid response.");
        }
    }
    private async void LaunchMonitor_Connect_Click(object sender, RoutedEventArgs e)
    {
        await (Application.Current as App)?.ConnectAndSetupBluetooth();
    }
    private async void LM_ARMButton_Click(object sender, RoutedEventArgs e)
    {
        await (Application.Current as App)?.LmArmDevice();
    }
    private async void LM_DISARMButton_Click(object sender, RoutedEventArgs e)
    {
        await (Application.Current as App)?.LmDisarmDevice();
    }
    private async void LaunchMonitor_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await (Application.Current as App)?.LmDisconnect();
    }
    private async void LM_Resub_Click(object sender, RoutedEventArgs e)
    {
        await (Application.Current as App)?.BtManagerResub();
    }
    private async void Putting_Connect_Click(object sender, RoutedEventArgs e)
    {
        await (Application.Current as App)?.PuttingEnable();
    }
    private async void Putting_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await (Application.Current as App)?.PuttingDisable();
    }
}
