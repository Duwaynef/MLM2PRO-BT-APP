using MLM2PRO_BT_APP.Putting;
using Newtonsoft.Json;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using static MLM2PRO_BT_APP.HomeMenu;
using System.Xml.Linq;
using System.Windows.Automation;
using System.Net;

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
    OpenConnectServer OpenConnectServerInstance = new OpenConnectServer(IPAddress.Any, 951);
    public string lastMessage = "";    
    public string jsonContent = "";
    public App()
    {
        SharedVM = new SharedViewModel();
        LoadSettings();
        manager = new BluetoothManager();
        PuttingConnection = new HttpPuttingServer();
        client = new OpenConnectTCPClient(SettingsManager.Instance.Settings.OpenConnect.GSProIp, SettingsManager.Instance.Settings.OpenConnect.GSProPort);
    }
    private void CheckWebApiToken()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.Instance.Settings.WebApiSettings.WebApiSecret))
        {
            Logger.Log("Web api token is blank");
            App.SharedVM.LMStatus = "WEB API TOKEN NOT CONFIGURED";

            WebApiWindow WebApiWindow = new WebApiWindow();
            WebApiWindow.Topmost = true;
            WebApiWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WebApiWindow.ShowDialog();
        }
    }
    public async Task StartGSPro()
    {
        String ExecutablePath = Path.GetFullPath(SettingsManager.Instance.Settings.OpenConnect.GSProEXE ?? "C:\\GSProV1\\Core\\GSP\\GSPro.exe");
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExecutablePath));
        if (processes.Length > 0)
        {
            Logger.Log("The GSPro application is already running.");
            return;
        } else if (!File.Exists(ExecutablePath))
        {
            Logger.Log("The GSPro application does not exist.");
            return;
        }

        var startInfo = new ProcessStartInfo(ExecutablePath)
        {
            WorkingDirectory = Path.GetDirectoryName(ExecutablePath),
            UseShellExecute = true
        };

        try
        {
            Process.Start(startInfo);
            Logger.Log("GSPro Started");

            if(SettingsManager.Instance.Settings.OpenConnect.SkipGSProLauncher)
            {
                await ClickButtonWhenWindowLoads("GSPro Configuration", "Play!");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error starting the GSPro process with arguments: {ex.Message}");
        }
    }
    public async Task<bool> WaitForWindow(string windowTitle, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        AutomationElement window = null;
        while (sw.Elapsed < timeout)
        {
            window = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, windowTitle));
            if (window != null)
            {
                return true;
            }
            await Task.Delay(500);
        }
        return false;
    }
    public async Task ClickButtonWhenWindowLoads(string windowTitle, string buttonName)
    {
        Logger.Log("Application started, waiting for window...");
        bool windowLoaded = await WaitForWindow(windowTitle, TimeSpan.FromSeconds(120));
        if (windowLoaded)
        {
            var window = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, windowTitle));
            var button = window?.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, buttonName));
            var invokePattern = button?.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            invokePattern?.Invoke();
            Logger.Log($"{buttonName} button clicked in {windowTitle}");
        }
        else
        {
            Logger.Log("Window did not appear in time.");
        }
    }
    public async Task ConnectGSPro()
    {
        try
        {
            bool GSProOpenAPILoaded = await WaitForWindow("APIv1 Connect", TimeSpan.FromSeconds(120));
            if (GSProOpenAPILoaded)
            {
                Logger.Log("GSPro OpenAPI window loaded.");
                client.ConnectAsync();
            }
            else
            {
                Logger.Log("GSPro OpenAPI window did not load in time.");
                App.SharedVM.GSProStatus = "NOT CONNECTED";
            }
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
            ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
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
        Application.Current.Dispatcher.Invoke(() =>
        {
            SharedViewModel.Instance.ShotDataCollection.Insert(0, shotData);
        });
    }
    public async Task ConnectAndSetupBluetooth()
    {
        App.SharedVM.LMStatus = "LOOKING FOR DEVICE";
        manager.RestartDeviceWatcher();
    }
    public async Task LMArmDevice()
    {
        await manager.ArmDevice();
    }
    public async Task LMArmDeviceWithDelay()
    {
        await Task.Delay(1000);
        await LMArmDevice();
    }
    public async Task LMDisarmDevice()
    {
        await manager.DisarmDevice();
    }
    public async Task LMDisarmDeviceWithDelay()
    {
        await Task.Delay(1000);
        await LMDisarmDevice();
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
            Logger.Log("Putting executable exists.");
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
            Logger.Log("Putting executable missing.");
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
    public void LoadSettings()
    {
        SettingsManager.Instance.LoadSettings();
    }
    public async Task StartOpenConnectServer()
    {
        OpenConnectServerInstance.Start();
    }
    public async Task StopOpenConnectServer()
    {
        OpenConnectServerInstance.Stop();
    }
    public async Task SendOpenConnectServerNewClientMessage()
    {
        if (!string.IsNullOrEmpty(lastMessage))
        {
            await Task.Delay(1000);
            Logger.Log("Sending message to OpenConnectServerClients:");
            Logger.Log(lastMessage);
            Logger.Log("");
            OpenConnectServerInstance.Multicast(lastMessage);
        }
    }
    public async Task SendOpenConnectServerMessage(String incomingMessage)
    {
        if (OpenConnectServerInstance.IsStarted)
        {
            Logger.Log("Sending message to OpenConnectServerClients");
            Logger.Log(incomingMessage);
            Logger.Log("");
            OpenConnectServerInstance.Multicast(incomingMessage);
        }
    }
    public async Task RelayOpenConnectServerMessage(String outgoingMessage)
    {
        lastMessage = outgoingMessage;
        Logger.Log("Relaying message to GSPro:");
        Logger.Log(outgoingMessage);
        Logger.Log("");
        await client.SendDirectJsonAsync(outgoingMessage);
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow mainWindow = new MainWindow();
        mainWindow.Loaded += MainWindow_Loaded;
        mainWindow.Show();
    }
    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CheckWebApiToken();
        if (SettingsManager.Instance.Settings.Putting.PuttingEnabled)
        {
            if (SettingsManager.Instance.Settings.Putting.AutoStartPutting)
            {
                PuttingEnable();
            }
        }

        if (SettingsManager.Instance.Settings.OpenConnect.AutoStartGSPro)
        {
            StartGSPro();
        }

        if (SettingsManager.Instance.Settings.OpenConnect.EnableAPIRelay)
        {
            StartOpenConnectServer();
        }

        ConnectGSPro();
    }
    private void App_Exit(object sender, ExitEventArgs e)
    {
        OpenConnectServerInstance.Stop();
    }
}