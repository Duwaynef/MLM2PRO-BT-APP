using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using System.Configuration;
using Windows.Media.Protection;
using System.Net.WebSockets;
using Newtonsoft.Json;
using System.Runtime.InteropServices.WindowsRuntime;
/*
namespace MLM2PRO_BT_APP
{
    class HttpPuttingSession
    {
        public ConnectionManager ConnectionManager;
        public HttpPuttingServer PuttingServer { get; private set; }
        private StreamWebSocket webSocket;

        public HttpPuttingSession(HttpPuttingServer server, ConnectionManager connectionManager)
        {
            ConnectionManager = connectionManager;
            PuttingServer = server;
            webSocket = new StreamWebSocket();
        }

        public async void ConnectAsync()
        {
            Uri serverUri = new Uri("");
            await webSocket.ConnectAsync(serverUri);
            Listen();
        }

        private async void Listen()
        {
            try
            {
                while (webSocket.Information.State == WebSocketState.Open)
                {
                    DataReader reader = new DataReader(webSocket.InputStream);
                    await reader.LoadAsync(1024);
                    string message = reader.ReadString(reader.UnconsumedBufferLength);
                    if (!string.IsNullOrEmpty(message))
                    {
                        HttpRequestMessage request = new HttpRequestMessage();
                        request.Content = new HttpStringContent(message);
                        OnReceivedRequest(request);
                    }
                }
            }
            catch (Exception ex)
            {
                PuttingLogger.LogPuttError($"Error processing request: {ex.Message}");
            }
        }

        private async void OnReceivedRequest(HttpRequestMessage request)
        {
            try
            {
                string message = await request.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(message))
                {
                    if ((request.Method.Method == "POST") || (request.Method.Method == "PUT"))
                    {
                        string key = request.RequestUri.ToString();
                        string value = message;
                        PuttingDataMessage dataMessage = JsonSerializer.Deserialize<PuttingDataMessage>(value);
                        PuttingLogger.LogPuttIncoming(value);

                        if (dataMessage != null)
                        {
                            if (PuttingServer.PuttingEnabled)
                                ConnectionManager.SendShot(BallDataFromPuttingBallData(dataMessage.ballData), null);
                            else
                                PuttingLogger.LogPuttInfo("Not sending Putt because selected club is not putter");
                        }
                    }
                }

                SendResponseAsync(Response.MakeOkResponse());
            }
            catch (Exception ex)
            {
                PuttingLogger.LogPuttError($"Error processing request: {ex.Message}");
                SendResponseAsync(Response.MakeErrorResponse());
            }
        }

        private void SendResponseAsync(object response)
        {
            // Implement sending response asynchronously
        }

        private BallData? BallDataFromPuttingBallData(PuttingBallData? puttBallData)
        {
            if (puttBallData == null) return null;
            return new PuttingBallData()
            {
                Speed = puttBallData.BallSpeed,
                //SpinAxis = -1 * (puttBallData.SpinAxis < 90 ? r10BallData.SpinAxis : r10BallData.SpinAxis - 360),
                TotalSpin = puttBallData.TotalSpin,
                HLA = puttBallData.LaunchDirection,
                VLA = 0,
            };
        }
    }

    class HttpPuttingServer
    {
        private readonly StreamSocketListener _listener;
        private readonly ConnectionManager _connectionManager;

        public bool PuttingEnabled { get; private set; }
        private bool _disposing;
        private bool _disposed = false;

        public Process PuttingProcess { get; private set; }
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

        public HttpPuttingServer(ConnectionManager connectionManager)
        {
            _listener = new StreamSocketListener();
            _connectionManager = connectionManager;

            _listener.ConnectionReceived += OnConnectionReceived;
            _listener.Control.KeepAlive = true;

            PuttingEnabled = false;

            OnlyLaunchWhenPutting = SettingsManager.Instance.AppSettings.Putting.OnlyLaunchWhenPutting;
            KeepPuttingCamOnTop = SettingsManager.Instance.AppSettings.Putting.KeepPuttingCamOnTop;
            LaunchBallTracker = SettingsManager.Instance.AppSettings.Putting.LaunchBallTracker;
            WebcamIndex = SettingsManager.Instance.AppSettings.Putting.WebcamIndex;
            BallColor = SettingsManager.Instance.AppSettings.Putting.BallColor;
            CamPreviewWidth = SettingsManager.Instance.AppSettings.Putting.CamPreviewWidth;
            ExecutablePath = SettingsManager.Instance.AppSettings.Putting.ExePath;
            ExecutableName = Path.GetFileName(ExecutablePath);
            AdditionalExeArgs = SettingsManager.Instance.AppSettings.Putting.AdditionalExeArgs;
            HideExeLogs = SettingsManager.Instance.AppSettings.Putting.HideExeLogs;

            StartListening();

            if (LaunchBallTracker && CheckBallTrackingExists() && !OnlyLaunchWhenPutting)
            {
                LaunchProcess();
                if (KeepPuttingCamOnTop)
                    FocusProcess();
            }

            _connectionManager.ClubChanged += (o, e) =>
            {
                if (e.Club == OpenConnectClub.PT)
                {
                    if (!PuttingEnabled)
                        StartPutting();
                }
                else
                {
                    if (PuttingEnabled)
                        StopPutting();
                }
            };
        }

        private void StartListening()
        {
            // Start listening for incoming connections
            _listener.BindServiceNameAsync(SettingsManager.Instance.AppSettings.Putting.PuttingPort.ToString());
        }

        private async void OnConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            try
            {
                // Read request data
                using (var stream = args.Socket.InputStream.AsStreamForRead())
                using (var reader = new StreamReader(stream))
                {
                    string request = await reader.ReadToEndAsync();
                    HttpRequest httpRequest = JsonConvert.DeserializeObject<HttpRequest>(request);

                    // Process request
                    if (httpRequest != null)
                        await ProcessRequest(httpRequest);
                }
            }
            catch (Exception ex)
            {
                PuttingLogger.LogPuttError($"Error processing request: {ex.Message}");
            }
        }

        private async Task ProcessRequest(HttpRequest request)
        {
            try
            {
                if (request.Method == "POST" || request.Method == "PUT")
                {
                    string key = request.Url;
                    string value = request.Body;
                    PuttingDataMessage message = JsonConvert.DeserializeObject<PuttingDataMessage>(value);
                    PuttingLogger.LogPuttIncoming(request.Body);

                    if (message != null)
                    {
                        if (PuttingEnabled)
                            _connectionManager.SendShot(BallDataFromPuttingBallData(message?.PuttingBallData), null);
                        else
                            PuttingLogger.LogPuttInfo("Not sending Putt because selected club is not putter");
                    }
                }

                // Send response
                await SendResponseAsync(Response.MakeOkResponse());
            }
            catch (Exception ex)
            {
                PuttingLogger.LogPuttError($"Error processing request: {ex.Message}");
                await SendResponseAsync(Response.MakeErrorResponse());
            }
        }

        private async Task SendResponseAsync(string response)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new StreamWriter(stream))
                {
                    await writer.WriteAsync(response);
                    await writer.FlushAsync();

                    // Send response data
                    await _listener.OutputStream.WriteAsync(stream.GetWindowsRuntimeBuffer());
                }
            }
        }

        private bool CheckBallTrackingExists()
        {
            return File.Exists(ExecutablePath);
        }

        private void LaunchProcess()
        {
            if (CheckBallTrackingExists())
            {
                ProcessStartInfo startInfo = new ProcessStartInfo(ExecutablePath);
                startInfo.Arguments = $"-w {WebcamIndex} -c {BallColor} -r {CamPreviewWidth}";
                startInfo.WorkingDirectory = Path.GetDirectoryName(ExecutablePath);
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
                startInfo.CreateNoWindow = false;
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

                PuttingLogger.LogPuttInfo($"Starting putting camera: '{startInfo.FileName} {startInfo.Arguments}' ");
                PuttingProcess = Process.Start(startInfo);

                if (PuttingProcess == null)
                {
                    PuttingLogger.LogPuttError("Error opening putting process");
                    return;
                }
                Logger.Log(PuttingProcess.BasePriority);

                PuttingProcess.EnableRaisingEvents = true;
                PuttingProcess.OutputDataReceived += OnBallTrackerLogs;
                PuttingProcess.ErrorDataReceived += OnBallTrackerErrors;
                PuttingProcess.BeginOutputReadLine();
                PuttingProcess.BeginErrorReadLine();

                PuttingProcess.Exited += OnPuttingProcessClosed;

                int attempts = 0;
                while (((int)PuttingProcess.MainWindowHandle) == 0)
                {
                    if (attempts % 5 == 0)
                    {
                        PuttingLogger.LogPuttInfo("Waiting for main window to launch...");
                    }
                    Task.Delay(1000).Wait();
                    attempts += 1;
                }
                PuttingLogger.LogPuttInfo("Main Window launched");
            }
        }

        private void OnBallTrackerLogs(object _, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Data) && !HideExeLogs)
                PuttingLogger.LogPuttInfo($"[{ExecutableName}] {args.Data}");
        }

        private void OnBallTrackerErrors(object _, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Data) && !HideExeLogs)
                PuttingLogger.LogPuttError($"[{ExecutableName}] {args.Data}");
        }
        private void OnPuttingProcessClosed(object? _, EventArgs? args)
        {
            PuttingProcess = null;
            if ((PuttingEnabled || (!OnlyLaunchWhenPutting)) && !mDisposing)
            {
                PuttingLogger.LogPuttError($"{ExecutableName} closed unexpectedly. Reopening...");
                StartPutting();
            }
            else
            {
                PuttingLogger.LogPuttInfo($"{ExecutableName} closed");
            }
        }

        private void FocusProcess()
        {
            if (PuttingProcess != null)
            {
                IntPtr handle = PuttingProcess.MainWindowHandle;
                if (handle != IntPtr.Zero)
                {
                    SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, TOPMOST_FLAGS);
                    BringWindowToTop(handle);
                    SetForegroundWindow(handle);
                }
            }
        }

        private void KillProcess()
        {
            PuttingLogger.LogPuttInfo("Shutting down putting camera");
            PuttingProcess?.Kill();
            PuttingProcess = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (!mDisposed)
            {
                if (disposing)
                {
                    mDisposing = true;
                    StopPutting(force: true);
                    base.Dispose(disposing);
                }
                bool mDisposed = true; // Set the flag to indicate disposal
            }
        }
    }

    public static class PuttingLogger
    {
        public static void LogPuttInfo(string message) => Logger.Log("INFO: " + message);
        public static void LogPuttError(string message) => Logger.Log("ERROR: " + message);
        public static void LogPuttOutgoing(string message) => Logger.Log("PUTT_OUT: " + message);
        public static void LogPuttIncoming(string message) => Logger.Log("PUTT_IN: " + message);
    }
}
*/