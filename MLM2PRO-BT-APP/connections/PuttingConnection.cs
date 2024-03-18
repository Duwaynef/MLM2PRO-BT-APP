using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetCoreServer;


namespace MLM2PRO_BT_APP.Putting
{

    class HttpPuttingSession : HttpSession
    {
        public HttpPuttingServer PuttingServer { get; private set; }

        public HttpPuttingSession(HttpPuttingServer server) : base(server)
        {
            PuttingServer = server;
            var options = new JsonSerializerOptions
            {
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };
        }

        protected override void OnReceivedRequest(HttpRequest request)
        {
            try
            {  
                if (request.Method == "POST" || request.Method == "PUT")
                {
                    string key = request.Url;
                    string value = request.Body;
                    PuttingDataMessage? message = JsonSerializer.Deserialize<PuttingDataMessage>(value);
                    Logger.Log(request.Body);

                    if (message != null)
                    {
                        App.SharedVM.PuttingStatus = "INCOMING MESSAGE";
                        if (PuttingServer.PuttingEnabled && DeviceManager.Instance.ClubSelection == "PT")
                        {
                            OpenConnectApiMessage? messageToSend = BallDataFromPuttingBallData(message?.BallData);
                            if (messageToSend != null)
                            {
                                (App.Current as App)?.SendShotData(messageToSend);
                                App.SharedVM.PuttingStatus = "SHOT SENT";
                            }
                            else
                            {
                                
                            }
                        }
                        else
                        {
                            App.SharedVM.PuttingStatus = "CONNECTED";
                            Logger.Log("Not sending Putt because selected club is not putter");
                        }
                    }

                }
                SendResponseAsync(Response.MakeOkResponse());

            }
            catch
            {
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

        public static OpenConnectApiMessage? BallDataFromPuttingBallData(Putting.BallData? puttBallData)
        {
            if (puttBallData == null) return null;
            OpenConnectApiMessage.Instance.ShotNumber++;
            return new OpenConnectApiMessage()
            {
                ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
                BallData = new MLM2PRO_BT_APP.BallData()
                {
                    Speed = puttBallData.BallSpeed,
                    SpinAxis = 0,
                    TotalSpin = puttBallData.TotalSpin,
                    HLA = puttBallData.LaunchDirection,
                    VLA = 0,
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
        [DllImport("User32.dll")]
        private static extern bool SetForegroundWindow(nint hWnd);
        [DllImport("User32.dll")]
        private static extern bool BringWindowToTop(nint handle);
        [DllImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private static readonly nint HWND_TOPMOST = new nint(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;
        public bool PuttingEnabled = false;
        private bool mDisposing;

        public Process? PuttingProcess { get; private set; }
        public bool OnlyLaunchWhenPutting { get; }
        public bool KeepPuttingCamOnTop { get; }
        public bool LaunchBallTracker { get; }
        public int WebcamIndex { get; }
        public string BallColor { get; }
        public int CamPreviewWidth { get; }
        public string ExecutablePath { get; }
        public string ExecutableName { get; }
        public string AdditionalExeArgs { get; }
        public bool HideExeLogs { get; }
        protected override TcpSession CreateSession() => new HttpPuttingSession(this);
        protected override void OnError(SocketError error) => Logger.Log($"HTTP session caught an error: {error}");

        public HttpPuttingServer()
          : base(IPAddress.Any, SettingsManager.Instance.Settings.Putting.PuttingPort)
        {
            Logger.Log($"Starting putting receiver on port {Port}");

            OnlyLaunchWhenPutting = SettingsManager.Instance.Settings.Putting.OnlyLaunchWhenPutting;
            KeepPuttingCamOnTop = SettingsManager.Instance.Settings.Putting.KeepPuttingCamOnTop;
            LaunchBallTracker = SettingsManager.Instance.Settings.Putting.LaunchBallTracker;
            WebcamIndex = SettingsManager.Instance.Settings.Putting.WebcamIndex;
            BallColor = SettingsManager.Instance.Settings.Putting.BallColor;
            CamPreviewWidth = SettingsManager.Instance.Settings.Putting.CamPreviewWidth;
            ExecutablePath = SettingsManager.Instance.Settings.Putting.ExePath;
            ExecutableName = Path.GetFileName(ExecutablePath);
            AdditionalExeArgs = SettingsManager.Instance.Settings.Putting.AdditionalExeArgs;
            HideExeLogs = SettingsManager.Instance.Settings.Putting.HideExeLogs;

            if (LaunchBallTracker && CheckBallTrackingExists() && !OnlyLaunchWhenPutting)
            {
                LaunchProcess();
                if (KeepPuttingCamOnTop)
                    FocusProcess();
            }
        }

        public void StartPutting()
        {
            PuttingEnabled = true;
            if (LaunchBallTracker && PuttingProcess == null)
            {
                LaunchProcess();
                App.SharedVM.PuttingStatus = "CONNECTED";
            }
            if (KeepPuttingCamOnTop)
                FocusProcess();
        }
        public void StopPutting(bool force = false)
        {
            PuttingEnabled = false;
            if (LaunchBallTracker && OnlyLaunchWhenPutting || force)
                KillProcess();
                App.SharedVM.PuttingStatus = "DISCONNECTED";
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

        public void LaunchProcess()
        {
            if (CheckBallTrackingExists())
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(ExecutablePath);
                startInfo.Arguments = $"-w {WebcamIndex} -c {BallColor} -r {CamPreviewWidth}";
                startInfo.WorkingDirectory = Path.GetDirectoryName(ExecutablePath);
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
                startInfo.CreateNoWindow = SettingsManager.Instance?.Settings?.Putting?.HideConsoleWindow ?? false;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardInput = true;
                startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "TRUE";
                startInfo.Environment["PYTHONUNBUFFERED"] = "TRUE";

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
            if ((PuttingEnabled || !OnlyLaunchWhenPutting) && !mDisposing)
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
                nint handle = PuttingProcess.MainWindowHandle;
                if (handle != nint.Zero)
                {
                    SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
                    BringWindowToTop(handle);
                    SetForegroundWindow(handle);
                }
            }
        }

        public void KillProcess()
        {
            Logger.Log("Shutting down putting camera");

            PuttingProcess?.Kill();
            PuttingProcess = null;
        }


        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    mDisposing = true;
                    StopPutting(force: true);
                    base.Dispose(disposing);
                }
            }
        }
    }
}
