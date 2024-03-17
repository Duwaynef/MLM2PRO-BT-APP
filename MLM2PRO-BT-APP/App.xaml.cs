using MLM2PRO_BT_APP.Putting;
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
    internal HttpPuttingServer? PuttingConnection { get; }
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

        if (SettingsManager.Instance.Settings.Putting.PuttingEnabled)
        {
            // Initialize the PuttingServer
            PuttingConnection = new HttpPuttingServer();
            if (SettingsManager.Instance.Settings.Putting.AutoStartPutting)
            {
                PuttingEnable();
            }
        }

        if (SettingsManager.Instance.Settings.WebApiSettings.WebApiSecret == "")
        {
            Logger.Log("Web api token is blank");
            App.SharedVM.LMStatus = "WEB API TOKEN NOT CONFIGURED";
        }
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
            if (messageToSend.BallData.Speed == 0)
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
    public async Task insertRow(OpenConnectApiMessage inputData, string result)
    {
        HomeMenu.ShotData shotData = new HomeMenu.ShotData
        {
            ShotCounter = OpenConnectApiMessage.Instance.ShotCounter,
            Result = result,
            Club = DeviceManager.Instance.ClubSelection ?? "",
            BallSpeed = inputData.BallData?.Speed ?? 0,
            SpinAxis = inputData.BallData?.SpinAxis ?? 0,
            SpinRate = inputData.BallData?.TotalSpin ?? 0,
            VLA = inputData.BallData?.VLA ?? 0,
            HLA = inputData.BallData?.HLA ?? 0,
            ClubSpeed = inputData.ClubData?.Speed ?? 0,
            //BackSpin = 0,
            //SideSpin = 0,
            //ClubPath = 0,
            //ImpactAngle = 0
        };
        SharedViewModel.Instance.ShotDataCollection.Insert(0, shotData);
    }

    public async Task ConnectAndSetupBluetooth()
    {
        manager.RestartDeviceWatcher();
    }
    public async Task LMArmDevice()
    {
        await manager.ArmDevice();
    }
    public async Task LMDisarmDevice()
    {
        await manager.DisarmDevice();
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

    public async Task PuttingEnable()
    {
        string fullPath = Path.GetFullPath(SettingsManager.Instance.Settings.Putting.ExePath);
        if (File.Exists(fullPath))
        {
            Console.WriteLine("Putting executable exists.");
            bool puttingStarted = PuttingConnection.IsStarted;
            if (puttingStarted == false)
            {
                bool? isStarted = PuttingConnection?.Start();
                if (isStarted == true)
                {
                    App.SharedVM.PuttingStatus = "CONNECTED";
                    PuttingConnection.PuttingEnabled = true;
                }
            } else
            {
                App.SharedVM.PuttingStatus = "CONNECTED";
                PuttingConnection.PuttingEnabled = true;
            }           
        }
        else
        {
            Console.WriteLine("Putting executable missing.");
            App.SharedVM.PuttingStatus = "ball_tracking.exe missing";
        }
        
    }

    public async Task PuttingDisable()
    {
        PuttingConnection.PuttingEnabled = false;
    }

    public async Task StartPutting()
    {
        PuttingConnection?.StartPutting();
    }
    public async Task StopPutting()
    {
        PuttingConnection?.StopPutting();
    }
}

