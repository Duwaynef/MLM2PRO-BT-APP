using MLM2PRO_BT_APP.WebApiClient;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MLM2PRO_BT_APP;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class HomeMenu : Page
{
    public TextBox GSProStatusTextBox;
    //public ObservableCollection<ShotData> ShotDataCollection { get; set; } = new ObservableCollection<ShotData>();
    public HomeMenu()
    {
        InitializeComponent();
        this.DataContext = App.SharedVM;
        // ShotDataDataGrid.ItemsSource = App.SharedVM.ShotDataCollection;
        ShotDataDataGrid.ItemsSource = SharedViewModel.Instance.ShotDataCollection;
    }

    private void GSPro_Connect_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.ConnectGSPro();
    }

    private void GSPro_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.DisconnectGSPro();
    }

    private void GSPro_Send_TestShot_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.SendTestShotData();
    }

    private async Task Write01(object sender, RoutedEventArgs e)
    {
        //await manager.WriteConfig(DeviceManager.Instance.GetParametersFromSettings());
    }

    private async Task Write02(object sender, RoutedEventArgs e)
    {
        //byte[] data = byteConversionUtils.HexStringToByteArray("01180001000000"); //01180001000000 == arm device???
        //await manager.WriteCommand(data);
    }

    private async Task Write03(object sender, RoutedEventArgs e)
    {
        //byte[] data = byteConversionUtils.HexStringToByteArray("01180000000000"); //01180000000000 == disarm device???
        //await manager.WriteCommand(data);
    }

    private async Task Write04(object sender, RoutedEventArgs e)
    {
        // resub?

        //await manager.SetupDeviceAsync(manager.bluetoothDevice);
    }

    private async Task Write05(object sender, RoutedEventArgs e)
    {
        /*
        byte[] byteArray = byteConversionUtils.StringToByteArray("010000000001488384561d45c4f21a079de55c24df9c7167f10d1964f2179bc8170dfbe2453a");
        byte[] byteKeyOutput = await manager.ConvertAuthRequest(byteArray);
        Logger.Log("Key: " + byteConversionUtils.ByteArrayToHexString(byteKeyOutput));

        // second decrypt attempt
        byte[] byteKeyOutput2 = byteConversionUtils.StringToByteArray("488384561D45C4F21A079DE55C24DF9C7167F10D1964F2179BC8170DFBE2453A");
        byte[] byteArray2 = byteConversionUtils.StringToByteArray("1b001070acd281e3ffff000000000900010100040082032b000000012e2700230004001b1100d5d19ddaf553e26e3d2ad9b478beff33e4baf6b7477854bb4e8354f92c8a3328");
        byte[] outputByteArr = btEncryption.DecryptKnownKey(byteArray2, byteKeyOutput2);
        Logger.Log("Decrypted Bytes: " + byteConversionUtils.ByteArrayToHexString(outputByteArr));
        */
    }

    private void DecryptTextBox(object sender, RoutedEventArgs e)
    {
        /*
        String textBoxInput = "";//ConsoleInputTextBox.Text;
        String keyTextBoxInput = "";// KeyTextBox.Text;
        byte[] byteArray = byteConversionUtils.StringToByteArray(textBoxInput);
        byte[] byteArray2 = byteConversionUtils.StringToByteArray(keyTextBoxInput);

        byte[] outputByteArr = btEncryption.DecryptKnownKey(byteArray, byteArray2);
        Logger.Log("Decrypted Bytes: " + byteConversionUtils.ByteArrayToHexString(outputByteArr));
        */
    }

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
        //public double BackSpin { get; set; }
        //public double SideSpin { get; set; }
        //public double ClubPath { get; set; }
        //public double ImpactAngle { get; set; }
    }
    public void AddTestShotDataRows()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            SharedViewModel.Instance.ShotDataCollection.Insert(0, new ShotData { ShotNumber = OpenConnectApiMessage.Instance.ShotNumber, Result = "Test", Club = "Test", BallSpeed = 100, SpinAxis = 20, SpinRate = 5000, VLA = 40, HLA = 10, ClubSpeed = 50 });
        });
    }

    private async void LM_WebApiTest_Click(object sender, RoutedEventArgs e)
    {
        App.SharedVM.LMStatus = "TESTING WEBAPI";
        
        WebApiClient.WebApiClient webApiClient = new WebApiClient.WebApiClient();
        Logger.Log("WebApiTest_Click: UserToken: " + SettingsManager.Instance.Settings.WebApiSettings.WebApiToken);
        Logger.Log("WebApiTest_Click: UserId: " + SettingsManager.Instance.Settings.WebApiSettings.WebApiUserId);
        WebApiClient.WebApiClient.ApiResponse response = await webApiClient.SendRequestAsync(SettingsManager.Instance.Settings.WebApiSettings.WebApiUserId);

        if (response != null && response.Success)
        {
            SettingsManager.Instance.Settings.WebApiSettings.WebApiDeviceId = response.User.Id;
            SettingsManager.Instance.Settings.WebApiSettings.WebApiToken = response.User.Token;
            SettingsManager.Instance.Settings.WebApiSettings.WebApiExpireDate = response.User.ExpireDate;
            SettingsManager.Instance.SaveSettings();
            Logger.Log($"User ID: {response?.User?.Id}, Token: {response?.User?.Token}, Expire Date: {response?.User?.ExpireDate}");
            App.SharedVM.LMStatus = "WEBAPI SUCCESS";
            
        }
        else
        {
            App.SharedVM.LMStatus = "IWEBAPI FAILED";
            Logger.Log("Failed to get a valid response.");
        }
    }

    private void LaunchMonitor_Connect_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.ConnectAndSetupBluetooth();
    }

    private void LM_ARMButton_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.LMArmDevice();
    }

    private void LM_DISARMButton_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.LMDisarmDevice();
    }

    private void LaunchMonitor_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.LMDisconnect();
    }

    private void LM_Resub_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.BTManagerResub();
    }

    private void Putting_Connect_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.PuttingEnable();
    }

    private void Putting_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        (App.Current as App)?.PuttingDisable();
    }
}
