using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using MLM2PRO_BT_APP.api;
using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
using NetCoreServer;

namespace MLM2PRO_BT_APP.connections
{

    class HttpPuttingSession(HttpPuttingServer server) : HttpSession(server)
    {
        private HttpPuttingServer PuttingServer { get; } = server;

        protected override async void OnReceivedRequest(HttpRequest request)
        {
            try
            {  
                if (request.Method is "POST" or "PUT")
                {
                    string value = request.Body;
                    Logger.Log("Received Putting request: " + value);
                    var message = JsonSerializer.Deserialize<PuttingDataMessage>(value);
                    Logger.Log("Putting request converted");
                    Logger.Log(request.Body);

                    if (message != null)
                    {
                        if (App.SharedVm != null) App.SharedVm.PuttingStatus = "INCOMING MESSAGE";
                        if (PuttingServer.PuttingEnabled && DeviceManager.Instance?.ClubSelection == "PT")
                        {
                            var messageToSend = BallDataFromPuttingBallData(message.BallData);
                            if (messageToSend != null)
                            {
                                (Application.Current as App)?.SendShotData(messageToSend);
                                if (App.SharedVm != null) App.SharedVm.PuttingStatus = "SHOT SENT";
                            }
                        }
                        else
                        {
                            if (App.SharedVm != null) App.SharedVm.PuttingStatus = "CONNECTED";
                            Logger.Log("Not sending Putt because selected club is not putter");
                        }
                        await Task.Delay(2000);
                        if (App.SharedVm != null) App.SharedVm.PuttingStatus = "CONNECTED, READY";
                    }

                }
                SendResponseAsync(Response.MakeOkResponse());

            }
            catch(Exception ex)
            {
                Logger.Log("Putting incoming message exception \n" + ex);
                SendResponseAsync(Response.MakeErrorResponse());
            }

        }

        protected override void OnReceivedRequestError(HttpRequest request, string error)
        {
            Logger.Log($"Request error: {error}");
        }

        protected override void OnError(SocketError error)
        {
            Logger.Log($"HTTP session caught an error: {error}");
        }

        private static OpenConnectApiMessage? BallDataFromPuttingBallData(api.BallData? puttBallData)
        {
            if (puttBallData == null) return null;
            OpenConnectApiMessage.Instance.ShotNumber++;
            return new OpenConnectApiMessage()
            {
                ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
                BallData = new BallData()
                {
                    Speed = puttBallData.BallSpeed,
                    SpinAxis = 0,
                    TotalSpin = puttBallData.TotalSpin,
                    Hla = puttBallData.LaunchDirection,
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
        }
    }

    class HttpPuttingServer : HttpServer
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);
        [DllImport("User32.dll")]
        private static extern bool BringWindowToTop(nint handle);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]

        private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
        private static readonly nint HwndTopmost = new nint(-1);
        private static readonly nint HwndNotTopmost = new nint(-2);
        private const uint SwpNosize = 0x0001;
        private const uint SwpNomove = 0x0002;
        private const int SW_MINIMIZE = 6;
        private const int SW_RESTORE = 9;
        private const uint TopmostFlags = SwpNomove | SwpNosize;
        public bool PuttingEnabled;
        private bool _mDisposing;
        public bool manualStopPutting;

        private Process? PuttingProcess { get; set; }
        public bool OnlyLaunchWhenPutting { get; }
        private bool AutoHidePuttingWhenAutoLaunchDisabled { get; }
        private bool KeepPuttingCamOnTop { get; }
        public bool LaunchBallTracker { get; set; }
        private int WebcamIndex { get; }
        private string BallColor { get; }
        private int CamPreviewWidth { get; }
        private string ExecutablePath { get; }
        private string ExecutableName { get; }
        private string AdditionalExeArgs { get; }
        private bool HideExeLogs { get; }
        protected override TcpSession CreateSession() => new HttpPuttingSession(this);
        protected override void OnError(SocketError error) => Logger.Log($"HTTP session caught an error: {error}");

        public HttpPuttingServer()
          : base(IPAddress.Any, SettingsManager.Instance?.Settings?.Putting?.PuttingPort ?? 8888)
        {
            Logger.Log($"Starting putting receiver on port {Port}");

            OnlyLaunchWhenPutting = SettingsManager.Instance?.Settings?.Putting?.OnlyLaunchWhenPutting ?? true;
            AutoHidePuttingWhenAutoLaunchDisabled = SettingsManager.Instance?.Settings?.Putting?.AutoHidePuttingWhenAutoLaunchDisabled ?? true;
            KeepPuttingCamOnTop = SettingsManager.Instance?.Settings?.Putting?.KeepPuttingCamOnTop ?? true;
            LaunchBallTracker = SettingsManager.Instance?.Settings?.Putting?.PuttingEnabled ?? true;
            WebcamIndex = SettingsManager.Instance?.Settings?.Putting?.WebcamIndex ?? 0;
            BallColor = SettingsManager.Instance?.Settings?.Putting?.BallColor ?? "white";
            CamPreviewWidth = SettingsManager.Instance?.Settings?.Putting?.CamPreviewWidth ?? 450;
            ExecutablePath = SettingsManager.Instance?.Settings?.Putting?.ExePath ?? "./ball_tracking/ball_tracking.exe";
            ExecutableName = Path.GetFileName(ExecutablePath);
            AdditionalExeArgs = SettingsManager.Instance?.Settings?.Putting?.AdditionalExeArgs ?? "";
            HideExeLogs = SettingsManager.Instance?.Settings?.Putting?.HideExeLogs ?? true;

            if (LaunchBallTracker && CheckBallTrackingExists() && !OnlyLaunchWhenPutting)
            {
                LaunchProcess();
                if (KeepPuttingCamOnTop)
                {
                    FocusProcess();
                }
            }
        }

        public void StartPutting()
        {
            PuttingEnabled = true;
            if (LaunchBallTracker && PuttingProcess == null)
            {
                LaunchProcess();
            }
            if (KeepPuttingCamOnTop || AutoHidePuttingWhenAutoLaunchDisabled)
                FocusProcess();
        }
        public void StopPutting(bool force = false)
        {
            PuttingEnabled = false;
            if (LaunchBallTracker && OnlyLaunchWhenPutting || force)
            {
                KillProcess();
            } 
            else if (AutoHidePuttingWhenAutoLaunchDisabled)
            {
                UnFocusProcess();
            }
        }


        private bool CheckBallTrackingExists()
        {
            if (!File.Exists(ExecutablePath))
            {
                Logger.Log($"{ExecutablePath} file not found.");
                Logger.Log("Download latest release of ball_tracking program from https://github.com/alleexx/cam-putting-py/releases and unzip to same folder as this program");
                return false;
            }
            return true;
        }

        private void LaunchProcess()
        {
            if (!CheckBallTrackingExists()) return;
            var startInfo = new ProcessStartInfo(ExecutablePath)
            {
                Arguments = $"-w {WebcamIndex} -c {BallColor} -r {CamPreviewWidth}",
                WorkingDirectory = Path.GetDirectoryName(ExecutablePath),
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = SettingsManager.Instance?.Settings?.Putting?.HideConsoleWindow ?? false,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                EnvironmentVariables =
                {
                    ["PYTHONUNBUFFERED"] = "TRUE"
                },
                Environment =
                {
                    ["PYTHONUNBUFFERED"] = "TRUE"
                }
            };

            if (!string.IsNullOrWhiteSpace(AdditionalExeArgs))
            {
                startInfo.Arguments = $"{startInfo.Arguments} {AdditionalExeArgs}";
            }

            Logger.Log($"Starting putting camera: '{startInfo.FileName} {startInfo.Arguments}' ");
            PuttingProcess = Process.Start(startInfo);

            if (PuttingProcess == null)
            {
                Logger.Log("Error opening putting process");
                return;
            }
            Logger.Log(PuttingProcess.BasePriority.ToString());

            PuttingProcess.EnableRaisingEvents = true;
            PuttingProcess.OutputDataReceived += OnBallTrackerLogs;
            PuttingProcess.ErrorDataReceived += OnBallTrackerErrors;
            PuttingProcess.BeginOutputReadLine();
            PuttingProcess.BeginErrorReadLine();

            PuttingProcess.Exited += OnPuttingProcessClosed;

            int attempts = 0;
            while ((int)PuttingProcess.MainWindowHandle == 0)
            {
                if (attempts % 5 == 0)
                {
                    Logger.Log("Waiting for main window to launch...");
                }
                Thread.Sleep(1000);
                attempts += 1;
            }
            Logger.Log("Main Window launched");
        }

        private void OnBallTrackerLogs(object _, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Data) && !HideExeLogs)
                Logger.Log($"[{ExecutableName}] {args.Data}");
        }

        private void OnBallTrackerErrors(object _, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Data) && !HideExeLogs)
                Logger.Log($"[{ExecutableName}] {args.Data}");
        }
        private void OnPuttingProcessClosed(object? _, EventArgs? args)
        {
            PuttingProcess = null;
            if ((PuttingEnabled || !OnlyLaunchWhenPutting) && !_mDisposing && !manualStopPutting)
            {
                Logger.Log($"{ExecutableName} closed unexpectedly. Reopening...");
                StartPutting();
            }
            else
            {
                Logger.Log($"{ExecutableName} closed");
            }
        }

        private void FocusProcess()
        {
            if (PuttingProcess != null)
            {
                IntPtr gsProHandle = FindWindow(null, "GSPro");
                nint handle = PuttingProcess.MainWindowHandle;
                if (handle != nint.Zero)
                {
                    SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, TopmostFlags);
                    BringWindowToTop(handle);
                    SetForegroundWindow(handle);
                    ShowWindow(handle, SW_RESTORE);
                }

                if (gsProHandle != IntPtr.Zero)
                {
                    SetForegroundWindow(gsProHandle);
                    ShowWindow(gsProHandle, SW_RESTORE);
                }

                if (App.SharedVm != null) App.SharedVm.PuttingStatus = "CONNECTED, READY";
            }
        }

        private void UnFocusProcess()
        {
            if (PuttingProcess != null)
            {
                IntPtr handle = PuttingProcess.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    ShowWindow(handle, SW_MINIMIZE);
                    if (App.SharedVm != null)
                        App.SharedVm.PuttingStatus = "CONNECTED, WINDOW UNFOCUSED";
                }
            }
        }

        private void KillProcess()
        {
            PuttingProcess?.Kill();
            PuttingProcess = null;
        }


        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    _mDisposing = true;
                    StopPutting(force: true);
                    base.Dispose(disposing);
                }
            }
        }
    }
}
