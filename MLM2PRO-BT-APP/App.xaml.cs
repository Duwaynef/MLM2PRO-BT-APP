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
            OpenConnectApiMessage messageSent = OpenConnectApiMessage.Instance.TestShot();
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
            String result = "Fail";
            Logger.Log(messageToSend?.ToString());
            string messageJson = JsonConvert.SerializeObject(messageToSend);
            if (messageToSend.ClubData.Speed == 0 || messageToSend.BallData.Speed == 0)
            {
                result = "Fail";
                await insertRow(messageToSend, result);
                App.SharedVM.GSProStatus = "CONNECTED, LM MISREAD";
                return;
            }

            bool dataSent = await client.SendDataAsync(messageToSend);
            if (dataSent)
            {
                result = "Success";
                Logger.Log("message sucessfully sent!");
                await insertRow(messageToSend, result);
                App.SharedVM.GSProStatus = "CONNECTED, SHOT SENT!";
                return;
            }
            else
            {
                Logger.Log($"Error sending message: Going to attempt a connection with GSPro");
                ConnectGSPro();
                bool dataSent2 = await client.SendDataAsync(messageToSend);
                if (dataSent2)
                {
                    result = "Success";
                    Logger.Log("Second attempt worked!");
                    await insertRow(messageToSend, result);
                    App.SharedVM.GSProStatus = "CONNECTED, SHOT SENT!";
                    return;
                } else
                {
                    result = "Fail";
                    Logger.Log("Second attempt failed...");
                    await insertRow(messageToSend, result);
                    App.SharedVM.GSProStatus = "DISCONNECTED, FAILED TO SEND SHOT";
                    return;
                }                
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error sending message: {ex.Message}");
        }
    }
    public async Task insertRow(OpenConnectApiMessage inputData,String result)
    {
        HomeMenu.ShotData shotData;
        shotData = new HomeMenu.ShotData
        {
            ShotCounter = OpenConnectApiMessage.Instance.ShotCounter,
            Result = result,
            BallSpeed = inputData.BallData.Speed,
            SpinAxis = inputData.BallData.SpinAxis,
            SpinRate = inputData.BallData.TotalSpin,
            VLA = inputData.BallData.VLA,
            HLA = inputData.BallData.HLA,
            ClubSpeed = inputData.ClubData.Speed,
            BackSpin = 0,
            SideSpin = 0,
            ClubPath = 0,
            ImpactAngle = 0
        };
        SharedViewModel.Instance.ShotDataCollection.Insert(0, shotData);
    }
    public async Task ConnectAndSetupBluetooth()
    {
        manager.RestartDeviceWatcher();
    }
    public async Task LMArmDevice()
    {
        byte[] data = byteConversionUtils.HexStringToByteArray("010D0001000000"); //01180001000000 also found 010D0001000000 == arm device???
        _ = manager.WriteCommand(data);
    }
    public async Task LMDisarmDevice()
    {
        byte[] data = byteConversionUtils.HexStringToByteArray("01180000000000"); //01180000000000 == disarm device???
        _ = manager.WriteCommand(data);
    }
    public async Task LMDisconnect()
    {
        byte[] data = new byte[] { 0, 0, 0, 0, 0, 0, 0 }; // Tell the Launch Monitor to disconnect
        await manager.WriteCommand(data);
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

