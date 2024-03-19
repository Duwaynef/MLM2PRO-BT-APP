using System.Text;
using System.Windows;
using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TcpClient = NetCoreServer.TcpClient;

namespace MLM2PRO_BT_APP.connections
{
    internal class OpenConnectTcpClient : TcpClient
    {
        private long _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
        private long _howRecentlyTakenShot = DateTimeOffset.Now.ToUnixTimeSeconds();
        private bool _isPutting;
        private readonly List<string?> _messages = [];
        private readonly Timer _processTimer;
        private const int QuietPeriod = 10;
        private readonly object _lock = new object();
        private DateTime _lastReceivedTime = DateTime.MinValue;
        private bool _isDeviceArmed = false;
        public OpenConnectTcpClient() : base(SettingsManager.Instance.Settings.OpenConnect.GSProIp, SettingsManager.Instance.Settings.OpenConnect.GSProPort) 
        {
            _processTimer = new Timer(ProcessLastMessage, null, Timeout.Infinite, Timeout.Infinite);
        }
        protected override void OnConnected()
        {
            Logger.Log("OpenConnectTCPClient: Connected to server.");
            // Update shared view model status
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (App.SharedVm != null) App.SharedVm.GSProStatus = "CONNECTED";
            });
        }
        protected override void OnDisconnected()
        {
            Logger.Log("OpenConnectTCPClient: Disconnected from server.");
            // Update shared view model status
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (App.SharedVm != null) App.SharedVm.GSProStatus = "DISCONNECTED";
            });

        }
        public Task<bool> SendDataAsync(OpenConnectApiMessage message)
        {
            var jsonMessage = JsonConvert.SerializeObject(message);
            var data = Encoding.UTF8.GetBytes(jsonMessage);
            return Task.FromResult(SendAsync(data));
        }
        public Task<bool>? SendDirectJsonAsync(string? json)
        {
            if (json == null) return null;
            var data = Encoding.UTF8.GetBytes(json);
            return Task.FromResult(SendAsync(data));
        }
        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            var message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            lock (_lock)
            {
                _messages.Add(message);
                _lastReceivedTime = DateTime.UtcNow;
            }
            _processTimer.Change(QuietPeriod, Timeout.Infinite);
        }
        private void ProcessLastMessage(object? state)
        {
            lock (_lock)
            {
                if ((DateTime.UtcNow - _lastReceivedTime).TotalMilliseconds < QuietPeriod)
                {
                    return;
                }
        
                if (_messages.Count > 0)
                {
                    // Process the last message
                    var lastMessage = _messages[^1];
                    Logger.Log($"Processing last message of burst: {lastMessage}");
                    try
                    {
                        var response = JsonConvert.DeserializeObject<OpenConnectApiResponse>(lastMessage);
                        if (response != null)
                        {
                            Logger.Log($"OpenConnectTCPClient: Processing last received message: {lastMessage}");
                            var fiveSecondsAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 5;
                            var delayAfterShotForDisarm = DateTimeOffset.Now.ToUnixTimeSeconds() - 15;
                            switch (response.Code)
                            {
                                case 200:
                                {
                                    Logger.Log($"OpenConnectTCPClient: Received 200: " + response);
                                    _howRecentlyTakenShot = DateTimeOffset.Now.ToUnixTimeSeconds();
                                    break;
                                }
                                case 201 when response.Player?.DistanceToTarget != 0:
                                {
                                    Logger.Log($"OpenConnectTCPClient: Received 201: " + response);
                                    ProcessResponse(response);
                                    if (_howRecentlyArmedOrDisarmed < fiveSecondsAgo && !_isDeviceArmed)
                                    {
                                        _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        _isDeviceArmed = true;
                                        (Application.Current as App)?.LmArmDevice();
                                        (Application.Current as App)?.SendOpenConnectServerMessage(lastMessage);
                                    }
                                    else if (!_isDeviceArmed)
                                    {
                                        _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        _isDeviceArmed = true;
                                        (Application.Current as App)?.LmArmDeviceWithDelay();
                                        (Application.Current as App)?.SendOpenConnectServerMessage(lastMessage);
                                    }
                                    break;
                                }
                                case 203 when _howRecentlyTakenShot <= delayAfterShotForDisarm:
                                {
                                    Logger.Log($"OpenConnectTCPClient: Received 203: " + response);
                                    Logger.Log("OpenConnectTCPClient: Sending disarm message");
                                    if (_howRecentlyArmedOrDisarmed < fiveSecondsAgo && _isDeviceArmed)
                                    {
                                        _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        _isDeviceArmed = false;
                                        (Application.Current as App)?.LmDisarmDevice();
                                    }
                                    else if (_isDeviceArmed)
                                    {
                                        _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        _isDeviceArmed = false;
                                        (Application.Current as App)?.LmDisarmDeviceWithDelay();
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Logger.Log($"Error deserializing JSON: {ex.Message}");
                    }
                    _messages.Clear(); // Clear the list after processing
                }
            }
        }
        protected override void OnError(System.Net.Sockets.SocketError error)
        {
            Logger.Log($"OpenConnectTCPClient: Socket error: {error}");
            Application.Current.Dispatcher.Invoke(() =>
            {
                App.SharedVm.GSProStatus = "SERVER ERROR";
            });
        }
        // Implement your logic for processing the response
        private void ProcessResponse(OpenConnectApiResponse response)
        {
            if (response?.Player != null)
            {
                var playerClub = response.Player.Club;
                if (playerClub.HasValue)
                {
                    Logger.Log($"OpenConnectTCPClient: Response: {playerClub.Value}");
                    if (playerClub == Club.Pt)
                    {
                        Logger.Log("OpenConnectTCPClient: Club selection is a putter");
                        DeviceManager.Instance.ClubSelection = "PT";
                        App.SharedVm.GSProClub = "PT";
                        if (_isPutting == false)
                        {
                            _isPutting = true;
                            (Application.Current as App)?.StartPutting();
                        }
                    }
                    else
                    {
                        Logger.Log($"OpenConnectTCPClient: Club selection is NOT a putter, it is {playerClub.Value}");
                        DeviceManager.Instance.ClubSelection = playerClub.Value.ToString();
                        App.SharedVm.GSProClub = playerClub.Value.ToString();
                        if (_isPutting == true)
                        {
                            _isPutting = false;
                            (Application.Current as App)?.StopPutting();
                        }
                    }
                }
                else
                {
                    Logger.Log("OpenConnectTCPClient: No club information available");
                    DeviceManager.Instance.ClubSelection = "";
                }
            }
            else
            {
                Logger.Log("OpenConnectTCPClient: Invalid response or no player information");
                DeviceManager.Instance.ClubSelection = "";
            }
        }

    }

    public class OpenConnectApiMessage
    {
        private static OpenConnectApiMessage _instance;
        public static OpenConnectApiMessage Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new OpenConnectApiMessage();
                }
                return _instance;
            }
        }

        public string DeviceId { get { return "GSPRO-MLM2PRO"; } }
        public string Units { get { return "Yards"; } }
        public int ShotNumber { get; set; }
        public string ApiVersion { get { return "1"; } }
        public BallData BallData { get; set; }
        public ClubData ClubData { get; set; }
        public ShotDataOptions ShotDataOptions { get; set; }
        public static OpenConnectApiMessage CreateHeartbeat(bool launchMonitorReady = false)
        {
            return new OpenConnectApiMessage()
            {
                ShotNumber = 0,
                ShotDataOptions = new ShotDataOptions()
                {
                    ContainsBallData = false,
                    ContainsClubData = false,
                    LaunchMonitorIsReady = launchMonitorReady,
                    IsHeartBeat = true
                }
            };
        }
        public OpenConnectApiMessage CreateShotData(OpenConnectApiMessage input)
        {
            return new OpenConnectApiMessage()
            {
                ShotNumber = ShotNumber,
                BallData = new BallData()
                {
                    Speed = input.BallData.Speed,
                    SpinAxis = input.BallData.SpinAxis,
                    TotalSpin = input.BallData.TotalSpin,
                    // BackSpin = backSpin,
                    // SideSpin = sideSpin,
                    HLA = input.BallData.HLA,
                    VLA = input.BallData.VLA
                },
                ClubData = new ClubData()
                {
                    Speed = input.ClubData.Speed
                },
                ShotDataOptions = new ShotDataOptions()
                {
                    ContainsBallData = true,
                    ContainsClubData = true,
                    LaunchMonitorIsReady = true,
                    IsHeartBeat = false
                }
            };
        }
        public OpenConnectApiMessage TestShot()
        {
            Instance.ShotNumber++;
            // Create a Random instance
            Random random = new Random();

            // Generate random values within the desired range
            double speed = Math.Round(random.NextDouble() * (160 - 30) + 30, 1);
            double spinAxis = Math.Round(random.NextDouble() * (20 - -20) + 0, 1);
            double totalSpin = Math.Round(random.NextDouble() * (13000 - 2000) + 2000, 0);
            double sideSpin = Math.Round(random.NextDouble() * (1000 - 20) + 20, 0);
            double backSpin = Math.Round(random.NextDouble() * (13000 - 2000) + 2000, 0);
            double hla = Math.Round(random.NextDouble() * (5.0 - -5.0) + 0.0, 1);
            double vla = Math.Round(random.NextDouble() * (40 - 10) + 10, 1);
            double clubspeed = Math.Round(random.NextDouble() * (160 - 30) + 30, 1);

            return new OpenConnectApiMessage()
            {
                ShotNumber = ShotNumber,
                BallData = new BallData()
                {
                    Speed = speed,
                    SpinAxis = spinAxis,
                    TotalSpin = totalSpin,
                    // BackSpin = backSpin,
                    // SideSpin = sideSpin,
                    HLA = hla,
                    VLA = vla
                },
                ClubData = new ClubData()
                {
                    Speed = clubspeed
                },
                ShotDataOptions = new ShotDataOptions()
                {
                    ContainsBallData = true,
                    ContainsClubData = true,
                    LaunchMonitorIsReady = true,
                    IsHeartBeat = false
                }
            };
        }
    }

    public class ShotDataOptions
    {
        public bool ContainsBallData { get; set; }
        public bool ContainsClubData { get; set; }
        public bool LaunchMonitorIsReady { get; set; } = true;
        // public bool? LaunchMonitorBallDetected { get; set; }
        public bool IsHeartBeat { get; set; }
    }
    public class BallData
    {
        public double Speed { get; set; }
        public double SpinAxis { get; set; }
        public double TotalSpin { get; set; }
        // public double BackSpin { get; set; }
        // public double SideSpin { get; set; }
        public double HLA { get; set; }
        public double VLA { get; set; }
        // public double CarryDistance { get; set; }

        /* how mlm2pro connector gets back and side spin. not sure if actually needed.
        self.back_spin = round(
            self.total_spin * math.cos(math.radians(self.spin_axis)))
        self.side_spin = round(
            self.total_spin * math.sin(math.radians(self.spin_axis)))
        */

    }
    public class ClubData
    {
        public double Speed { get; set; }
        // public double AngleOfAttack { get; set; }
        // public double FaceToTarget { get; set; }
        // public double Lie { get; set; }
        // public double Loft { get; set; }
        // public double Path { get; set; }
        // public double SpeedAtImpact { get; set; }
        // public double VerticalFaceImpact { get; set; }
        // public double HorizontalFaceImpact { get; set; }
        // public double ClosureRate { get; set; }

    }
    public class OpenConnectApiResponse
    {
        public OpenConnectApiResponse(string message, int code)
        {
            Code = code;
        }

        public int Code { get; }
        public PlayerInfo? Player { get; set; }
    }
    public class PlayerInfo
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Handed? Handed { get; set; }
        [JsonConverter(typeof(StringEnumConverter))]
        public Club? Club { get; set; }
        public float? DistanceToTarget { get; set; }
    }
    public enum Handed
    {
        Rh,
        Lh
    }
    public enum Club
    {
        Unknown,
        Dr,
        W2,
        W3,
        W4,
        W5,
        W6,
        W7,
        I1,
        I2,
        I3,
        I4,
        I5,
        I6,
        I7,
        I8,
        I9,
        H2,
        H3,
        H4,
        H5,
        H6,
        H7,
        Pw,
        Gw,
        Sw,
        Lw,
        Pt
    }
}
