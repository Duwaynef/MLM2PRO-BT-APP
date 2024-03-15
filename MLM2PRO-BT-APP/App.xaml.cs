using Newtonsoft.Json;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using static MLM2PRO_BT_APP.HomeMenu;

namespace MLM2PRO_BT_APP;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static SharedViewModel SharedVM { get; set; }
    // private SettingsManager settingsManager;
    private BluetoothManager manager;
    private OpenConnectTCPClient client;
    ByteConversionUtils byteConversionUtils = new ByteConversionUtils();
    public string jsonContent = "";
    public App()
    {
        InitializeComponent();
        // Initialize the shared ViewModel
        SharedVM = new SharedViewModel();

        Startup += App_Startup;

        // Initialize the BluetoothManager
        manager = new BluetoothManager();
    }

    private void App_Startup(object sender, StartupEventArgs e)
    {
        // Load settings, connect to devices, or any startup logic here
        // Before navigating to your first page, ensure settings are loaded
        SettingsManager.Instance.LoadSettings();

        // Initialize the OpenConnectTCPClient
        client = new OpenConnectTCPClient(SettingsManager.Instance.Settings.OpenConnect.GSProIp, SettingsManager.Instance.Settings.OpenConnect.GSProPort);
        ConnectGSPro();
    }
    public void ConnectGSPro()
    {
        try
        {
            client.ConnectAsync();
        }
        catch (Exception ex)
        {
            Logger.Log("Exception in connecting: " + ex.Message);
        }
    }


    public async Task DisconnectGSPro()
    {
        try
        {
            string lmNotReadyJson = "{\"DeviceID\": \"GSPRO-MLM2PRO\",\"Units\": \"Yards\",\"ShotNumber\": 0,\"APIversion\": \"1\",\"ShotDataOptions\": {\"ContainsBallData\": false,\"ContainsClubData\": false,\"LaunchMonitorIsReady\": false}}";
            await client.SendDirectJsonAsync(lmNotReadyJson);
            await Task.Delay(1000);
            client.Disconnect();
            Logger.Log("Disconnected from server.");
            App.SharedVM.GSProStatus = "DISCONNECTED"; // or "NOT CONNECTED" or any other desired value
            
        }
        catch (Exception ex)
        {
            Logger.Log($"Error disconnecting from server: {ex.Message}");
        }
    }
    public async Task SendTestShotData()
    {
        try
        {
            OpenConnectApiMessage.Instance.ShotCounter++;
            OpenConnectApiMessage messageSent = OpenConnectApiMessage.TestShot(OpenConnectApiMessage.Instance.ShotCounter);
            await SendShotData(messageSent);
        }
        catch (Exception ex)
        {
            Logger.Log($"Error sending message: {ex.Message}");
        }
    }
    public async Task SendShotData(OpenConnectApiMessage messageToSend)
    {
        try
        {
            HomeMenu.ShotData shotData;
            Logger.Log(messageToSend?.ToString());
            string messageJson = JsonConvert.SerializeObject(messageToSend);
            bool dataSent = await client.SendDataAsync(messageToSend);

            if (dataSent)
            {
                shotData = new HomeMenu.ShotData { Result = "Success", BallSpeed = messageToSend.BallData.Speed, SpinAxis = messageToSend.BallData.SpinAxis, SpinRate = messageToSend.BallData.TotalSpin, LaunchDirection = messageToSend.BallData.VLA, LaunchAngle = messageToSend.BallData.HLA, ClubSpeed = messageToSend.ClubData.Speed, BackSpin = 0, SideSpin = 0, ClubPath = 0, ImpactAngle = 0 };
            }
            else
            {
                Logger.Log($"Error sending message: Going to attempt a connection with GSPro");
                ConnectGSPro();
                bool dataSent2 = await client.SendDataAsync(messageToSend);
                if (dataSent2)
                {
                    shotData = new HomeMenu.ShotData { Result = "Success", BallSpeed = messageToSend.BallData.Speed, SpinAxis = messageToSend.BallData.SpinAxis, SpinRate = messageToSend.BallData.TotalSpin, LaunchDirection = messageToSend.BallData.VLA, LaunchAngle = messageToSend.BallData.HLA, ClubSpeed = messageToSend.ClubData.Speed, BackSpin = 0, SideSpin = 0, ClubPath = 0, ImpactAngle = 0 };
                } else
                {
                    shotData = new HomeMenu.ShotData { Result = "Fail", BallSpeed = messageToSend.BallData.Speed, SpinAxis = messageToSend.BallData.SpinAxis, SpinRate = messageToSend.BallData.TotalSpin, LaunchDirection = messageToSend.BallData.VLA, LaunchAngle = messageToSend.BallData.HLA, ClubSpeed = messageToSend.ClubData.Speed, BackSpin = 0, SideSpin = 0, ClubPath = 0, ImpactAngle = 0 };
                }                
            }
            SharedViewModel.Instance.ShotDataCollection.Insert(0, shotData);
        }
        catch (Exception ex)
        {
            Logger.Log($"Error sending message: {ex.Message}");
        }
    }
    public async Task ConnectAndSetupBluetooth()
    {
        _ = manager.ConnectAndSetup(SettingsManager.Instance.Settings.LaunchMonitor.BluetoothDeviceName);
    }
    public async Task LMArmDevice()
    {
        byte[] data = byteConversionUtils.HexStringToByteArray("01180001000000"); //01180001000000 == arm device???
        _ = manager.WriteCommand(data);
    }
    public async Task LMDisarmDevice()
    {
        byte[] data = byteConversionUtils.HexStringToByteArray("01180000000000"); //01180000000000 == disarm device???
        _ = manager.WriteCommand(data);
    }
    public async Task LMDisconnect()
    {
        _ = manager.DisconnectAndCleanup();
    }
    public class TextBoxStreamWriter : TextWriter
    {
        private TextBox output;

        public TextBoxStreamWriter(TextBox output)
        {
            this.output = output;
        }

        public override void Write(string value)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                output.Text += value;
            });
        }

        // Optionally override WriteLine(string) for convenience
        public override void WriteLine(string value)
        {
            Write(value + Environment.NewLine);
        }

        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
    }
    public byte[] GetBTKey()
    {
        return manager.getEncryptionKey();
    }
    public async Task BTManagerResub()
    {
        _ = manager.UnSubAndReSub();
    }
}

