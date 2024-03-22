using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Net;
using MLM2PRO_BT_APP.connections;
using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
namespace MLM2PRO_BT_APP;
public partial class App : Application
{
    public static SharedViewModel? SharedVm { get; private set; }

    private readonly BluetoothBaseInterface _manager;
    private HttpPuttingServer? PuttingConnection { get; }
    private readonly OpenConnectTcpClient _client;
    private readonly OpenConnectServer _openConnectServerInstance = new(IPAddress.Any, 951);
    private String currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
    private string? _lastMessage = "";
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        SharedVm = new SharedViewModel();
        LoadSettings();
        PuttingConnection = new HttpPuttingServer();
        _client = new OpenConnectTcpClient();

        if (SettingsManager.Instance.Settings.LaunchMonitor.UseBackupManager)
        {
            _manager = new BluetoothManagerBackup();
        }
        else
        {
            _manager = new BluetoothManager();
        }


    }

    public byte[] GetEncryptedKeyFromHex(byte[] input)
    {
        return _manager.ConvertAuthRequest(input);
    }

    private void CheckWebApiToken()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.Instance.Settings.WebApiSettings.WebApiSecret))
        {
            Logger.Log("Web api token is blank");
            if (SharedVm != null) SharedVm.LMStatus = "WEB API TOKEN NOT CONFIGURED";

            WebApiWindow webApiWindow = new WebApiWindow();
            webApiWindow.Topmost = true;
            webApiWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            webApiWindow.ShowDialog();
        }
    }

    public async Task StartGsPro()
    {
        String executablePath = Path.GetFullPath(SettingsManager.Instance.Settings.OpenConnect.GSProEXE ?? "C:\\GSProV1\\Core\\GSP\\GSPro.exe");
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executablePath));
        if (processes.Length > 0)
        {
            Logger.Log("The GSPro application is already running.");
            return;
        } else if (!File.Exists(executablePath))
        {
            Logger.Log("The GSPro application does not exist.");
            return;
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            WorkingDirectory = Path.GetDirectoryName(executablePath),
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
    public async Task AutoConnectGsPro()
    {
        try
        {
            bool gsProOpenApiLoaded = await WaitForWindow("APIv1 Connect", TimeSpan.FromSeconds(120));
            if (gsProOpenApiLoaded && !_client.IsConnected)
            {
                Logger.Log("GSPro OpenAPI window loaded.");
                _client.ConnectAsync();
            }
            else
            {
                Logger.Log("GSPro OpenAPI window did not load in time.");
                SharedVm.GSProStatus = "NOT CONNECTED";
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Exception in connecting: " + ex.Message);
        }
    }
    public async Task ConnectGsProButton()
    {
        if (!_client.IsConnected)
        {
            Logger.Log("Connecting to OpenConnect API.");
            _client.ConnectAsync();
        }
    }
    public async Task DisconnectGsPro()
    {
        try
        {
            // string? lmNotReadyJson = "{\"DeviceID\": \"GSPRO-MLM2PRO\",\"Units\": \"Yards\",\"ShotNumber\": 0,\"APIversion\": \"1\",\"ShotDataOptions\": {\"ContainsBallData\": false,\"ContainsClubData\": false,\"LaunchMonitorIsReady\": false}}";
            // await _client.SendDirectJsonAsync(lmNotReadyJson);
            // await Task.Delay(2000);
            _client.DisconnectAndStop();
            Logger.Log("Disconnected from server.");
            SharedVm.GSProStatus = "DISCONNECTED";
            
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
    public async Task SendShotData(OpenConnectApiMessage? messageToSend)
    {
        bool dataSent = messageToSend != null && await _client.SendDataAsync(messageToSend);
        try
        {
            String result = "Fail";
            Logger.Log(messageToSend?.ToString());
            string messageJson = JsonConvert.SerializeObject(messageToSend);
            if (messageToSend is { BallData.Speed: 0 })
            {
                result = "Fail";
                await InsertRow(messageToSend, result);
                SharedVm.GSProStatus = "CONNECTED, LM MISREAD";
                return;
            }

            if (dataSent)
            {
                result = "Success";
                Logger.Log("message successfully sent!");
                if (messageToSend != null) await InsertRow(messageToSend, result);
                SharedVm.GSProStatus = "CONNECTED, SHOT SENT!";
                return;
            }
            else
            {
                Logger.Log($"Error sending message: Going to attempt a connection with GSPro");
                await AutoConnectGsPro();
                var dataSent2 = messageToSend != null && await _client.SendDataAsync(messageToSend);
                if (dataSent2)
                {
                    result = "Success";
                    Logger.Log("Second attempt worked!");
                    if (messageToSend != null) await InsertRow(messageToSend, result);
                    SharedVm.GSProStatus = "CONNECTED, SHOT SENT!";
                    return;
                } else
                {
                    result = "Fail";
                    Logger.Log("Second attempt failed...");
                    if (messageToSend != null) await InsertRow(messageToSend, result);
                    SharedVm.GSProStatus = "DISCONNECTED, FAILED TO SEND SHOT";
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error sending message: {ex.Message}");
        }
    }
    private Task InsertRow(OpenConnectApiMessage inputData, string result)
    {
        HomeMenu.ShotData shotData = new HomeMenu.ShotData
        {
            ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
            Result = result,
            SmashFactor = MeasurementData.CalculateSmashFactor(inputData.BallData?.Speed ?? 0, inputData.ClubData?.Speed ?? 0),
            Club = DeviceManager.Instance.ClubSelection ?? "",
            BallSpeed = inputData.BallData?.Speed ?? 0,
            SpinAxis = inputData.BallData?.SpinAxis ?? 0,
            SpinRate = inputData.BallData?.TotalSpin ?? 0,
            VLA = inputData.BallData?.VLA ?? 0,
            HLA = inputData.BallData?.HLA ?? 0,
            ClubSpeed = inputData.ClubData?.Speed ?? 0,
            BackSpin = inputData.BallData?.BackSpin ?? 0,
            SideSpin = inputData.BallData?.SideSpin ?? 0
            //ClubPath = 0,
            //ImpactAngle = 0
        };
        Current.Dispatcher.Invoke(() =>
        {
            SharedViewModel.Instance.ShotDataCollection.Insert(0, shotData);
        });
        return Task.CompletedTask;
    }
    public async Task ConnectAndSetupBluetooth()
    {
        SharedVm.LMStatus = "LOOKING FOR DEVICE";
        _manager.RestartDeviceWatcher();
    }
    public async Task LmArmDevice()
    {
        await _manager.ArmDevice();
    }
    public async Task LmArmDeviceWithDelay()
    {
        await Task.Delay(1000);
        await LmArmDevice();
    }
    public async Task LmDisarmDevice()
    {
        await _manager.DisarmDevice();
    }
    public async Task LmDisarmDeviceWithDelay()
    {
        await Task.Delay(1000);
        await LmDisarmDevice();
    }
    public async Task LmDisconnect()
    {
        if (!_manager.isBluetoothDeviceValid()) return;
        _ = _manager.DisconnectAndCleanup();
    }
    public byte[]? GetBtKey()
    {
        return _manager.GetEncryptionKey();
    }
    public async Task BtManagerResub()
    {
        _ = _manager.UnSubAndReSub();
    }
    public async Task PuttingEnable()
    {
        var fullPath = Path.GetFullPath(SettingsManager.Instance.Settings.Putting.ExePath);
        if (File.Exists(fullPath))
        {
            Logger.Log("Putting executable exists.");
            var puttingStarted = PuttingConnection is { IsStarted: true };
            Logger.Log("Putting started: " + puttingStarted);
            if (puttingStarted == false)
            {
                Logger.Log("Starting putting server.");
                var isStarted = PuttingConnection?.Start();
                if (isStarted != true) return;
                if (SharedVm != null) SharedVm.PuttingStatus = "CONNECTED";
                if (PuttingConnection != null) PuttingConnection.PuttingEnabled = true;
            } 
            else
            {
                if (SharedVm != null) SharedVm.PuttingStatus = "CONNECTED";
                if (PuttingConnection != null)
                {
                    PuttingConnection.PuttingEnabled = true;
                    PuttingConnection.LaunchBallTracker = true;
                }
            }
            if (DeviceManager.Instance.ClubSelection == "PT")
            {
                await Task.Delay(1000);
                await StartPutting();
            }
        }
        else
        {
            Logger.Log("Putting executable missing.");
            if (SharedVm != null) SharedVm.PuttingStatus = "ball_tracking.exe missing";
        }
    }
    public async Task PuttingDisable()
    {
        if (PuttingConnection != null) PuttingConnection.PuttingEnabled = false;
        StopPutting();
    }
    public async Task StartPutting()
    {
        PuttingConnection?.StartPutting();
    }
    public async Task StopPutting()
    {
        PuttingConnection?.StopPutting();
    }
    private async void LoadSettings()
    {
        SettingsManager.Instance.LoadSettings();
    }
    private async Task StartOpenConnectServer()
    {
        _openConnectServerInstance.Start();
    }
    public async Task StopOpenConnectServer()
    {
        _openConnectServerInstance.Stop();
    }
    public async Task SendOpenConnectServerNewClientMessage()
    {
        if (!string.IsNullOrEmpty(_lastMessage))
        {
            await Task.Delay(1000);
            Logger.Log("OpenConnectServer: Sending message");
            Logger.Log(_lastMessage);
            Logger.Log("");
            _openConnectServerInstance.Multicast(_lastMessage);
        }
    }
    public async Task SendOpenConnectServerMessage(string? incomingMessage)
    {
        if (_openConnectServerInstance.IsStarted)
        {
            Logger.Log("OpenConnectServer: Sending message");
            Logger.Log(incomingMessage);
            Logger.Log("");
            _openConnectServerInstance.Multicast(incomingMessage);
        }
    }
    public async Task RelayOpenConnectServerMessage(string? outgoingMessage)
    {
        _lastMessage = outgoingMessage;
        Logger.Log("Relaying message to GSPro:");
        Logger.Log(outgoingMessage);
        Logger.Log("");
        await _client.SendDirectJsonAsync(outgoingMessage);
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
            StartGsPro();
        }

        if (SettingsManager.Instance.Settings.OpenConnect.EnableAPIRelay)
        {
            StartOpenConnectServer();
        }

        AutoConnectGsPro();

        Logger.Log("Bluetooth Backup Manager is " + (SettingsManager.Instance.Settings.LaunchMonitor.UseBackupManager ? "enabled" : "disabled"));


    }
    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Logger.Log($"AppCrash: " + e.Exception.ToString());
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Logger.Log($"AppCrash: " + exception?.ToString());
    }

    private void App_Exit(object sender, ExitEventArgs e)
    {
        _openConnectServerInstance.Stop();
    }
}