using MLM2PRO_BT_APP.connections;
using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RightEdge.Core;
using RightEdge.Core.Helpers;
using RightEdge.Device;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Automation;
using Windows.System;

namespace MLM2PRO_BT_APP;

public enum PuttingSystem
{
    WEBCAM_PUTTING,
    RIGHTEDGE_PUTT_TRACKER
}

public partial class App
{
    public static SharedViewModel? SharedVm { get; private set; }
    private readonly ManualResetEvent _cleanupComplete = new ManualResetEvent(false);

    private readonly IBluetoothBaseInterface? _manager;
    private HttpPuttingServer? _puttingConnection;
    private ManagedPuttTrackerDevice? _rightEdgeDevice;
    private bool _silentRightEdgeHandednessChangeInProgress = false;
    private PuttingSystem _activePuttingSystem = PuttingSystem.WEBCAM_PUTTING;
    private OpenConnectTcpClient _client;
    private OpenConnectServer? _openConnectServerInstance;
    private string? _lastMessage = "";
    private BluetoothScanner? _bluetoothScanner;
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        SharedVm = new SharedViewModel();
        LoadSettings();
        _puttingConnection = new HttpPuttingServer();
        _client = new OpenConnectTcpClient();
        _client.PlayerInfoReceived += OpenConnectClient_PlayerDataReceived;
        SettingsManager.Instance.SettingsUpdated += OnSettingsUpdated;
        if (SettingsManager.Instance.Settings != null && SettingsManager.Instance.Settings.LaunchMonitor != null)
        {
            if (SettingsManager.Instance.Settings?.LaunchMonitor?.UseBackupManager ?? false)
            {
                _manager = new BluetoothManagerBackup();
            }
            else
            {
                _manager = new BluetoothManager();
            }
        }

        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            if (s != null)
            {
                Application_Exit(s, null);
                _cleanupComplete.WaitOne();
            }
        };
    }

    public byte[] GetEncryptedKeyFromHex(byte[]? input)
    {
        return _manager?.ConvertAuthRequest(input) ?? Array.Empty<byte>();
    }

    /*
    private static void CheckWebApiToken()
    {
        if (string.IsNullOrWhiteSpace(SettingsManager.Instance.Settings?.WebApiSettings?.WebApiSecret))
        {
            Logger.Log("Web api token is blank");
            if (SharedVm != null) SharedVm.LmStatus = "WEB API TOKEN NOT CONFIGURED";

            WebApiWindow webApiWindow = new()
            {
                Topmost = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            webApiWindow.ShowDialog();
        }
    }
    */

    public async Task StartGsPro()
    {
        String executablePath = Path.GetFullPath(SettingsManager.Instance.Settings?.OpenConnect?.GsProExe ?? "C:\\GSProV1\\Core\\GSP\\GSPro.exe");
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
            if (SettingsManager.Instance.Settings?.OpenConnect?.SkipGsProLauncher ?? false)
            {
                await ClickButtonWhenWindowLoads("GSPro Configuration", "Play!");
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error starting the GSPro process with arguments: {ex.Message}");
        }
    }
    private static async Task<bool> WaitForWindow(string windowTitle, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            AutomationElement? window = AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, windowTitle));
            if (window != null)
            {
                return true;
            }
            await Task.Delay(500);
        }
        return false;
    }
    private static async Task ClickButtonWhenWindowLoads(string windowTitle, string buttonName)
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

    private static bool IsTcpPortListening(int port)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var listeners = ipGlobalProperties.GetActiveTcpListeners();
            return listeners.Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> WaitForPortListener(int port, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (IsTcpPortListening(port))
            {
                return true;
            }

            await Task.Delay(500);
        }

        return false;
    }

    private async Task AutoConnectGsPro()
    {
        try
        {
            int port = SettingsManager.Instance?.Settings?.OpenConnect?.GsProPort ?? 921;
            bool portListening = await WaitForPortListener(port, TimeSpan.FromSeconds(120));

            if (portListening && !_client.IsConnected)
            {
                Logger.Log($"Detected listener on OpenAPI port {port}. Connecting...");
                _client.ConnectAsync();
            }
            else
            {
                Logger.Log($"No listener detected on OpenAPI port {port} within timeout.");
                if (SharedVm != null) SharedVm.GsProStatus = "NOT CONNECTED";
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Exception in connecting: " + ex.Message);
        }
    }
    private void OnSettingsUpdated(object? sender, EventArgs e)
    {
        string savedIp = SettingsManager.Instance?.Settings?.OpenConnect?.GsProIp ?? "127.0.0.1";
        int savedPort = SettingsManager.Instance?.Settings?.OpenConnect?.GsProPort ?? 921;

        if (_client.Address == savedIp && _client.Port == savedPort) return;

        bool wasConnected = _client.IsConnected;
        Logger.Log($"GSPro settings updated from {_client.Address}:{_client.Port} to {savedIp}:{savedPort}. Recreating client.");

        _client.DisconnectAndStop();
        _client = new OpenConnectTcpClient();

        if (!wasConnected) return;

        Logger.Log("Reconnecting to OpenConnect API using updated settings.");
        _client.ConnectAsync();
    }
    public void ConnectGsProButton()
    {
        if (!_client.IsConnected)
        {
            Logger.Log("Connecting to OpenConnect API.");
            _client.ConnectAsync();
        }
    }
    public void DisconnectGsPro()
    {
        try
        {
            // string? lmNotReadyJson = "{\"DeviceID\": \"GSPRO-MLM2PRO\",\"Units\": \"Yards\",\"ShotNumber\": 0,\"APIVersion\": \"1\",\"ShotDataOptions\": {\"ContainsBallData\": false,\"ContainsClubData\": false,\"LaunchMonitorIsReady\": false}}";
            // await _client.SendDirectJsonAsync(lmNotReadyJson);
            // await Task.Delay(2000);
            _client.DisconnectAndStop();
            Logger.Log("Disconnected from server.");
            if (SharedVm != null)SharedVm.GsProStatus = "DISCONNECTED";
            
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
            string result;
            if (messageToSend != null)
            {
                var payload = _client.BuildPayloadForSend(messageToSend);
                Logger.Log($"Sending shot payload: {payload}");
            }
            if (messageToSend is { BallData.Speed: 0 })
            {
                result = "Fail";
                await InsertRow(messageToSend, result);
                if (SharedVm != null) SharedVm.GsProStatus = "CONNECTED, LM MISREAD";
                return;
            }

            if (dataSent)
            {
                result = "Success";
                Logger.Log("message successfully sent!");
                if (messageToSend != null) await InsertRow(messageToSend, result);
                if (SharedVm != null) SharedVm.GsProStatus = "CONNECTED, SHOT SENT!";
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
                    if (SharedVm != null) SharedVm.GsProStatus = "CONNECTED, SHOT SENT!";
                } else
                {
                    result = "Fail";
                    Logger.Log("Second attempt failed...");
                    if (messageToSend != null) await InsertRow(messageToSend, result);
                    if (SharedVm != null) SharedVm.GsProStatus = "DISCONNECTED, FAILED TO SEND SHOT";
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Error sending message: {ex.Message}");
        }
    }
    private static Task InsertRow(OpenConnectApiMessage inputData, string result)
    {
        HomeMenu.ShotData shotData = new()
        {
            ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
            Result = result,
            SmashFactor = MeasurementData.CalculateSmashFactor(inputData.BallData?.Speed ?? 0, inputData.ClubData?.Speed ?? 0),
            Club = DeviceManager.Instance?.ClubSelection ?? "",
            BallSpeed = inputData.BallData?.Speed ?? 0,
            SpinAxis = inputData.BallData?.SpinAxis ?? 0,
            SpinRate = inputData.BallData?.TotalSpin ?? 0,
            Vla = inputData.BallData?.Vla ?? 0,
            Hla = inputData.BallData?.Hla ?? 0,
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
        if (SharedVm != null) SharedVm.LmStatus = "LOOKING FOR DEVICE";
        await (_manager?.RestartDeviceWatcher() ?? Task.CompletedTask);
    }
    public async Task LmArmDevice()
    {
        await (_manager?.ArmDevice() ?? Task.CompletedTask);
    }
    public async Task LmArmDeviceWithDelay()
    {
        await Task.Delay(1000);
        await LmArmDevice();
    }
    public async Task LmDisarmDevice()
    {
        await (_manager?.DisarmDevice() ?? Task.CompletedTask);

    }
    public async Task LmDisarmDeviceWithDelay()
    {
        await Task.Delay(1000);
        await LmDisarmDevice();
    }
    public void LmDisconnect()
    {
        if (!_manager?.IsBluetoothDeviceValid() ?? false) return;
        _ = _manager?.DisconnectAndCleanup();
    }
    public byte[]? GetBtKey()
    {
        return _manager?.GetEncryptionKey();
    }
    public void BtManagerReSub()
    {
        _ = _manager?.UnSubAndReSub();
    }
    public async Task PuttingEnable(PuttingSystem puttingsystem = PuttingSystem.WEBCAM_PUTTING)
    {
        _activePuttingSystem = puttingsystem;

        if (puttingsystem == PuttingSystem.RIGHTEDGE_PUTT_TRACKER)
        {
            try
            {
                if (_rightEdgeDevice != null)
                    _rightEdgeDevice.CloseConnections();

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    if (SharedViewModel.REDeviceSelectorControl != null)
                    {
                        SwingDirection initialHandedness = SwingDirection.RIGHT;
                        if (SharedViewModel.REHandednessSelectorControl != null)
                        {
                            initialHandedness = SharedViewModel.REHandednessSelectorControl.SelectedHandedness;
                            SharedViewModel.REHandednessSelectorControl.SelectionChanged += RightEdgeHandednessSelector_Changed;
                        }

                        _rightEdgeDevice = SharedViewModel.REDeviceSelectorControl.CreateSelectedDeviceObject();
                        _rightEdgeDevice.NewPuttReceived = newRightEdgePuttTrackerPuttDataReceived;
                        _rightEdgeDevice.ConnectionStatusChanged = rightEdgePuttTracker_ConnectionStatusChanged;

                        if (await _rightEdgeDevice.EstablishConnection(initialHandedness))
                        {
                            rightEdgePuttTracker_ConnectionStatusChanged(ManagedDeviceStatus.CONNECTED);
                            Logger.Log($"Right Edge Putt Tracker device connected. Selected device: {_rightEdgeDevice.Name}");
                        }
                        else
                        {
                            rightEdgePuttTracker_ConnectionStatusChanged(ManagedDeviceStatus.FAILED);
                            Logger.Log($"Failed to connect to Putt Tracker device. Selected device: {_rightEdgeDevice.Name}");

                            _rightEdgeDevice.ResetFailover();
                        }
                    }
                });       
            }
            catch (Exception ex)
            {
                Logger.Log($"ERROR refreshing Right Edge Putt Tracker device: {ex.Message}");
            }
        }
        else
        {
            var fullPath = Path.GetFullPath(SettingsManager.Instance.Settings?.Putting?.ExePath ?? "");
            if (File.Exists(fullPath) && _puttingConnection != null)
            {
                Logger.Log("Putting executable exists.");
                var puttingStarted = _puttingConnection is { IsStarted: true };
                _puttingConnection.manualStopPutting = false;
                Logger.Log("Putting started: " + puttingStarted);
                if (puttingStarted == false)
                {
                    Logger.Log("Starting putting server.");
                    var isStarted = _puttingConnection.Start();
                    if (isStarted != true) return;
                    if (SharedVm != null) SharedVm.PuttingStatus = "CONNECTED";
                    _puttingConnection.PuttingEnabled = true;
                }
                else
                {
                    if (SharedVm != null) SharedVm.PuttingStatus = "CONNECTED";
                    _puttingConnection.PuttingEnabled = true;
                    _puttingConnection.LaunchBallTracker = true;
                }
                if (DeviceManager.Instance?.ClubSelection == "PT" || !_puttingConnection.OnlyLaunchWhenPutting)
                {
                    await Task.Delay(1000);
                    StartPutting();
                }
            }
            else
            {
                Logger.Log("Putting executable missing.");
                if (SharedVm != null) SharedVm.PuttingStatus = "ball_tracking.exe missing";
            }
        }
            
    }
    public void PuttingDisable()
    {
        if (_puttingConnection != null)
        {
            if (App.SharedVm != null) App.SharedVm.PuttingStatus = "SHUTDOWN";
            _puttingConnection.manualStopPutting = true;
            _puttingConnection.StopPutting(true);
            _puttingConnection.Stop();
        }

        if (_rightEdgeDevice != null)
            _rightEdgeDevice.CloseConnections();
    }

    public void StartPutting()
    {
        if (_activePuttingSystem == PuttingSystem.RIGHTEDGE_PUTT_TRACKER)
        {
            if( _rightEdgeDevice != null )
                rightEdgePuttTracker_ConnectionStatusChanged(_rightEdgeDevice.ManagedStatus);
        }
        else
            _puttingConnection?.StartPutting();
    }

    public void StopPutting()
    {
        if (App.SharedVm != null) App.SharedVm.PuttingStatus = "DISCONNECTED";
        _puttingConnection?.StopPutting();
    }
    public void KillPutting()
    {
        _puttingConnection?.Dispose();
        _rightEdgeDevice?.CloseConnections();
    }

    public async Task PuttingToggleAutoClose()
    {
        if (SettingsManager.Instance?.Settings?.Putting != null)
        {
            SettingsManager.Instance.Settings.Putting.OnlyLaunchWhenPutting = !SettingsManager.Instance.Settings.Putting.OnlyLaunchWhenPutting;
            SettingsManager.Instance.SaveSettings();
        }
        if (_puttingConnection != null)
        {
            _puttingConnection.StopPutting(true);
            _puttingConnection.Dispose();
        }
        _puttingConnection = new HttpPuttingServer();
        await PuttingEnable();
    }

    // ===================================================
    //
    // Right Edge Putt Tracker Device Interface Event Handlers and Methods
    //
    // ===================================================
    private void rightEdgePuttTracker_ConnectionStatusChanged( ManagedDeviceStatus newStatus )
    {
        switch( newStatus )
        {
            case ManagedDeviceStatus.CONNECTING:
                if (SharedVm != null) SharedVm.PuttingStatus = "CONNECTING";
                break;

            case ManagedDeviceStatus.CONNECTED:
                if (SharedVm != null)
                {
                    SharedVm.PuttingStatus = "CONNECTED";

                    if (SharedVm.GsProClub != null)
                        SharedVm.PuttingStatus += SharedVm.GsProClub.Equals("PT", StringComparison.CurrentCultureIgnoreCase) ? "" : ", PUTTER NOT SELECTED";
                }

                break;

            case ManagedDeviceStatus.LOSING_CONNECTION:
                if (SharedVm != null) SharedVm.PuttingStatus = "RECONNECTING";
                break;

            case ManagedDeviceStatus.DISCONNECTED:
                if (SharedVm != null) SharedVm.PuttingStatus = "DISCONNECTED";
                break;

            case ManagedDeviceStatus.FAILED:
                if (SharedVm != null) SharedVm.PuttingStatus = "FAILED";
                break;
        }

        return;
    }

    private async void newRightEdgePuttTrackerPuttDataReceived(RightEdgePuttData new_putt_data)
    {
        Logger.Log("Received new putt from Right Edge Putt Tracker...");
        try
        {

            if (!_client.IsConnected)
            {
                Logger.Log("Cannot send putt from Right Edge Putt Tracker because not connected to GSPro OpenConnect.");
                return;
            }

            if(DeviceManager.Instance?.ClubSelection == "PT")
            {
                try
                {
                    OpenConnectApiMessage.Instance.ShotNumber++;
                    OpenConnectApiMessage messageToSend = new OpenConnectApiMessage()
                    {
                        ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
                        BallData = new BallData()
                        {
                            Speed = BasicHelpers.toMilesPerHour(new_putt_data.speed),
                            SpinAxis = 0,
                            TotalSpin = 0,
                            Hla = new_putt_data.degOffCenter,
                            Vla = 0,
                        },
                        ShotDataOptions = new ShotDataOptions()
                        {
                            ContainsBallData = true,
                            ContainsClubData = false,
                            LaunchMonitorIsReady = true,
                            IsHeartBeat = false
                        }
                    };

                    await (Application.Current as App)?.SendShotData(messageToSend);
                    if (App.SharedVm != null)
                        App.SharedVm.PuttingStatus = "SHOT SENT";
                }
                catch (Exception ex)
                {
                    Logger.Log($"ERROR sending putt received from Right Edge Putt Tracker to GSPro OpenConnect: {ex.Message}");
                }
            }
            else
            {
                rightEdgePuttTracker_ConnectionStatusChanged(ManagedDeviceStatus.CONNECTED);
                //if (App.SharedVm != null)
                //    App.SharedVm.PuttingStatus = "CONNECTED";

                Logger.Log("Not sending putt received from Right Edge Putt Tracker to GSPro because selected club in GS Pro is not putter");
            }
            
        }
        catch (Exception ex)
        {
            Logger.Log($"ERROR processing/sending putt received from Right Edge Putt Tracker: {ex.Message}");
        }
    }

    private void RightEdgeHandednessSelector_Changed(object sender, SwingDirection swing_direction)
    {
        if (_silentRightEdgeHandednessChangeInProgress)
        {
            _silentRightEdgeHandednessChangeInProgress = false;
            return;
        }

        setRightEdgeDeviceHandedness(swing_direction);

        return;
    }

    private async void setRightEdgeDeviceHandedness(SwingDirection swing_direction)
    {
        if ((_rightEdgeDevice != null) && (_rightEdgeDevice.ManagedStatus == ManagedDeviceStatus.CONNECTED))
        {
            if (!await _rightEdgeDevice.resetReadyPutt(swing_direction))
            {
                rightEdgePuttTracker_ConnectionStatusChanged(_rightEdgeDevice.ManagedStatus);
                Logger.Log("Failed attempt to set Right Edge device handedness to " + ((swing_direction == SwingDirection.LEFT) ? "LEFT" : "RIGHT") + ".");

                if( SharedViewModel.REHandednessSelectorControl != null )
                {
                    //_silentRightEdgeHandednessChangeInProgress = true;
                    SharedViewModel.REHandednessSelectorControl.SelectedHandedness = _rightEdgeDevice.PuttHandedness;
                }
                    
            }
            else
            {
                //if (SharedVm != null) SharedVm.PuttingStatus = "CONNECTED";
                rightEdgePuttTracker_ConnectionStatusChanged(ManagedDeviceStatus.CONNECTED);
                Logger.Log("Set Right Edge device handedness to " + ((swing_direction == SwingDirection.LEFT) ? "LEFT" : "RIGHT") + ".");

                if (SharedViewModel.REHandednessSelectorControl != null)
                {
                    //_silentRightEdgeHandednessChangeInProgress = true;
                    SharedViewModel.REHandednessSelectorControl.SelectedHandedness = swing_direction;
                }
            }
        }

        return;
    }

    private async void OpenConnectClient_PlayerDataReceived(object? sender, PlayerInfo ocPlayerObj)
    {
        
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            Logger.Log($"Keeping Right Edge device in sync with handedness received from GSPro OpenConnect...");
            if ((SharedViewModel.REHandednessSelectorControl != null) && (ocPlayerObj != null))
            {
                //
                // If we got Player info with the response, if it has handedness, make sure our handedness (device + display) is in sync...
                //
                Handed handednessValueReceived = ocPlayerObj.Handed.GetValueOrDefault(Handed.Rh);
                SwingDirection receivedHandedness = (handednessValueReceived == Handed.Lh) ? SwingDirection.LEFT : SwingDirection.RIGHT;
                if (receivedHandedness != SharedViewModel.REHandednessSelectorControl.SelectedHandedness)
                {
                    SharedViewModel.REHandednessSelectorControl.SelectedHandedness = receivedHandedness;
                    /*
                    if ((_rightEdgeDevice != null) && (_rightEdgeDevice.ManagedStatus == ManagedDeviceStatus.CONNECTED))
                        setRightEdgeDeviceHandedness(receivedHandedness);
                    else
                    {
                        _silentRightEdgeHandednessChangeInProgress = true;
                        SharedViewModel.REHandednessSelectorControl.SelectedHandedness = receivedHandedness;
                    }*/
                }
            }
            else
                Logger.Log($"Unexpected: Right Edge handedness control not found.");
        });
    }
    // ========================================================================
    //
    // Right Edge Putt Tracker Device Interface Event Handlers and Methods
    //
    // ========================================================================

    private static void LoadSettings()
    {
        SettingsManager.Instance.LoadSettings();
    }
    private void StartOpenConnectServer()
    {
        _openConnectServerInstance = new(IPAddress.Any, SettingsManager.Instance.Settings?.OpenConnect?.ApiRelayPort ?? 951);
        Logger.Log("OpenConnectServer: Starting server on port: " + SettingsManager.Instance.Settings?.OpenConnect?.ApiRelayPort);
        _openConnectServerInstance.Start();
    }
    public void StopOpenConnectServer()
    {
        _openConnectServerInstance?.Stop();
    }
    public async Task SendOpenConnectServerNewClientMessage()
    {
        if (!string.IsNullOrEmpty(_lastMessage))
        {
            await Task.Delay(1000);
            Logger.Log("OpenConnectServer: Sending message");
            Logger.Log(_lastMessage);
            Logger.Log("");
            _openConnectServerInstance?.Multicast(_lastMessage);
        }
    }
    public void SendOpenConnectServerMessage(string? incomingMessage)
    {
        if (!(_openConnectServerInstance?.IsStarted ?? false && !string.IsNullOrEmpty(incomingMessage))) return;
        Logger.Log("OpenConnectServer: Sending message");
        Logger.Log(incomingMessage);
        Logger.Log("");
        _openConnectServerInstance.Multicast(incomingMessage);
    }
    public async Task RelayOpenConnectServerMessage(string? outgoingMessage)
    {
        if (string.IsNullOrEmpty(outgoingMessage)) return;
        _lastMessage = outgoingMessage;
        Logger.Log("Relaying message to GSPro:");
        Logger.Log(outgoingMessage);
        Logger.Log("");
        var messageToRow = JsonConvert.DeserializeObject<OpenConnectApiMessage>(outgoingMessage);
        if (messageToRow != null) await InsertRow(messageToRow, "Relayed");
        await (_client.SendDirectJsonAsync(outgoingMessage) ?? Task.CompletedTask);
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow mainWindow = new();
        mainWindow.Loaded += MainWindow_Loaded;
        mainWindow.Show();
    }
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // CheckWebApiToken();

        if (SettingsManager.Instance.Settings?.Putting?.PuttingEnabled ?? false)
        {
            if (SettingsManager.Instance.Settings.Putting.AutoStartPutting)
            {
                PuttingSystem lastRunPuttingSystem = PuttingSystem.WEBCAM_PUTTING; // <-- Update to get this value from settings.
                await Task.Run(() => PuttingEnable(lastRunPuttingSystem));
            }
        }

        if (SettingsManager.Instance.Settings?.OpenConnect?.AutoStartGsPro ?? false)
        {
            
            await Task.Run(StartGsPro);
        }

        if (SettingsManager.Instance.Settings?.OpenConnect?.EnableApiRelay ?? false)
        {
            StartOpenConnectServer();
        }
        Logger.Log("Bluetooth Backup Manager is " + (SettingsManager.Instance.Settings?.LaunchMonitor?.UseBackupManager ?? false ? "enabled" : "disabled"));
        _bluetoothScanner = new BluetoothScanner(); // @TODO - ble scanner?
        await Task.Run(AutoConnectGsPro);
    }

    private static void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Logger.Log($"AppCrash: " + e.Exception);
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Logger.Log($"AppCrash: " + exception);
    }

    private async void Application_Exit(object sender, ExitEventArgs? e)
    {
        SettingsManager.Instance.SettingsUpdated -= OnSettingsUpdated;
        await PerformCleanupAsync();
        await Task.Delay(200);
        _cleanupComplete.Set();
    }

    public async Task PerformCleanupAsync()
    {
        await Task.Run(KillPutting);
    }
}