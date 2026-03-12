using MLM2PRO_BT_APP.devices;
using MLM2PRO_BT_APP.util;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using TcpClient = NetCoreServer.TcpClient;

namespace MLM2PRO_BT_APP.connections
{
    internal class OpenConnectTcpClient() : TcpClient(SettingsManager.Instance?.Settings?.OpenConnect?.GsProIp ?? "127.0.0.1", SettingsManager.Instance?.Settings?.OpenConnect?.GsProPort ?? 921)
    {
        private long _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
        private long _howRecentlyTakenShot = DateTimeOffset.Now.ToUnixTimeSeconds();
        private bool _isPutting;
        private bool _isDeviceArmed;
        private readonly StringBuilder _messageBuffer = new StringBuilder();
        private CancellationTokenSource? _squareGolfSpoofCts;
        private Task? _squareGolfSpoofTask;

        public void DisconnectAndStop()
        {
            StopSquareGolfSpoofLoop();
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }
        protected override void OnConnected()
        {
            Logger.Log("OpenConnectTCPClient: Connected to server.");
            StartSquareGolfSpoofLoop();
            // Update shared view model status
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (App.SharedVm != null) App.SharedVm.GsProStatus = "CONNECTED";
            });
        }
        protected override void OnDisconnected()
        {
            StopSquareGolfSpoofLoop();
            Logger.Log("OpenConnectTCPClient: Disconnected from server.");
            // Update shared view model status
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (App.SharedVm != null) App.SharedVm.GsProStatus = "DISCONNECTED";
            });

        }

        private void StartSquareGolfSpoofLoop()
        {
            StopSquareGolfSpoofLoop();
            _squareGolfSpoofCts = new CancellationTokenSource();
            _squareGolfSpoofTask = Task.Run(() => RunSquareGolfSpoofLoop(_squareGolfSpoofCts.Token));
        }

        private void StopSquareGolfSpoofLoop()
        {
            if (_squareGolfSpoofCts == null) return;
            _squareGolfSpoofCts.Cancel();
            _squareGolfSpoofCts.Dispose();
            _squareGolfSpoofCts = null;
            _squareGolfSpoofTask = null;
        }

        private async Task RunSquareGolfSpoofLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                    if (!IsConnected) continue;
                    if (SettingsManager.Instance.Settings?.OpenConnect?.PretendSquareGolf != true) continue;

                    var keepAliveJson = CreateSquareGolfSpoofMessage();
                    bool sent = SendAsync(Encoding.UTF8.GetBytes(keepAliveJson));
                    bool logHeartbeat = SettingsManager.Instance.Settings?.OpenConnect?.LogSquareGolfHeartbeatMessages ?? true;

                    if (sent)
                    {
                        if (logHeartbeat)
                        {
                            Logger.Log("OpenConnectTCPClient: SquareGolf keepalive sent.");
                            Logger.Log(keepAliveJson);
                        }
                    }
                    else
                    {
                        if (logHeartbeat)
                        {
                            Logger.Log("OpenConnectTCPClient: Failed to send SquareGolf spoof message.");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Log($"OpenConnectTCPClient: SquareGolf spoof loop error: {ex.Message}");
            }
        }

        private static string CreateSquareGolfSpoofMessage()
        {
            return "{\"DeviceID\":\"SquareGolf\",\"Units\":\"Yards\",\"ShotNumber\":2,\"APIversion\":\"1\",\"BallData\":{\"Speed\":0.0,\"SpinAxis\":0.0,\"TotalSpin\":0.0,\"BackSpin\":0.0,\"SideSpin\":0.0,\"HLA\":0.0,\"VLA\":0.0,\"CarryDistance\":0.0},\"ClubData\":{\"Speed\":0.0,\"AngleOfAttack\":0.0,\"FaceToTarget\":0.0,\"Lie\":0.0,\"Loft\":0.0,\"Path\":0.0,\"SpeedAtImpact\":0.0,\"VerticalFaceImpact\":0.0,\"HorizontalFaceImpact\":0.0,\"ClosureRate\":0.0},\"ShotDataOptions\":{\"ContainsBallData\":false,\"ContainsClubData\":false,\"LaunchMonitorIsReady\":true,\"LaunchMonitorBallDetected\":true,\"IsHeartBeat\":false}}";
        }

        private static string CreateSquareGolfShotMessage(OpenConnectApiMessage message)
        {
            var payload = new
            {
                DeviceID = "SquareGolf",
                Units = "Yards",
                ShotNumber = message.ShotNumber,
                APIversion = "1",
                BallData = new
                {
                    Speed = message.BallData?.Speed ?? 0.0,
                    SpinAxis = message.BallData?.SpinAxis ?? 0.0,
                    TotalSpin = message.BallData?.TotalSpin ?? 0.0,
                    BackSpin = message.BallData?.BackSpin ?? 0.0,
                    SideSpin = message.BallData?.SideSpin ?? 0.0,
                    HLA = message.BallData?.Hla ?? 0.0,
                    VLA = message.BallData?.Vla ?? 0.0,
                    CarryDistance = 200.0
                },
                ClubData = new
                {
                    Speed = message.ClubData?.Speed ?? 100.0,
                    AngleOfAttack = 0.1,
                    FaceToTarget = 0.6,
                    Lie = 50.0,
                    Loft = 12.5,
                    Path = 1.1,
                    SpeedAtImpact = 110.0,
                    VerticalFaceImpact = 0.1,
                    HorizontalFaceImpact = -0.1,
                    ClosureRate = 1.4
                },
                ShotDataOptions = new
                {
                    ContainsBallData = true,
                    ContainsClubData = true,
                    LaunchMonitorIsReady = true,
                    LaunchMonitorBallDetected = true,
                    IsHeartBeat = true
                }
            };

            return JsonConvert.SerializeObject(payload);
        }

        public string BuildPayloadForSend(OpenConnectApiMessage message)
        {
            return SettingsManager.Instance.Settings?.OpenConnect?.PretendSquareGolf == true
                ? CreateSquareGolfShotMessage(message)
                : JsonConvert.SerializeObject(message);
        }

        public Task<bool> SendDataAsync(OpenConnectApiMessage message)
        {
            var jsonMessage = BuildPayloadForSend(message);

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
            var data = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            _messageBuffer.Append(data);
            
            var fullMessage = _messageBuffer.ToString();
            var messages = fullMessage.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Check if the last message is complete (ends with } and we have more data in buffer than just the messages we split)
            bool lastMessageIncomplete = !fullMessage.TrimEnd().EndsWith("}");
            
            // Process all complete messages
            int messagesToProcess = lastMessageIncomplete ? messages.Length - 1 : messages.Length;
            
            for (int i = 0; i < messagesToProcess; i++)
            {
                var message = messages[i].Trim();
                if (string.IsNullOrWhiteSpace(message)) continue;
                
                try
                {
                    var response = JsonConvert.DeserializeObject<OpenConnectApiResponse>(message);
                    if (response != null)
                    {
                        Logger.Log($"OpenConnectTCPClient: Processing message:");
                        Logger.Log(message);
                        var fiveSecondsAgo = DateTimeOffset.Now.ToUnixTimeSeconds() - 5;
                        var delayAfterShotForDisarm = DateTimeOffset.Now.ToUnixTimeSeconds() - 20;
                        bool autoDisarmEnabled = SettingsManager.Instance?.Settings?.LaunchMonitor?.AutoDisarm ?? false;
                        (Application.Current as App)?.SendOpenConnectServerMessage(message);
                        switch (response.Code)
                        {
                            case 200:
                                {
                                    Logger.Log($"OpenConnectTCPClient: Received 200:");
                                    Logger.Log(message);
                                    _howRecentlyTakenShot = DateTimeOffset.Now.ToUnixTimeSeconds();
                                    break;
                                }
                            case 201 when response.Player?.DistanceToTarget != 0:
                                {
                                    Logger.Log($"OpenConnectTCPClient: Received 201:");
                                    Logger.Log(message);
                                    ProcessResponse(response);
                                    if (!_isDeviceArmed)
                                    {
                                        _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        _isDeviceArmed = true;
                                        Task.Run(() =>
                                        {
                                            (Application.Current as App)?.LmArmDevice();
                                        });


                                    }
                                    else if (!_isDeviceArmed)
                                    {
                                        _isDeviceArmed = true;
                                        Task.Run(() =>
                                        {
                                            (Application.Current as App)?.LmArmDeviceWithDelay();
                                        });
                                        Task.Run(() =>
                                        {
                                        });
                                    }
                                    break;
                                }
                            case 203 when _howRecentlyTakenShot <= delayAfterShotForDisarm && autoDisarmEnabled:
                                {
                                    Logger.Log($"OpenConnectTCPClient: Received 203:");
                                    Logger.Log(message);
                                    if (_howRecentlyArmedOrDisarmed < fiveSecondsAgo && _isDeviceArmed)
                                    {

                                        Logger.Log("OpenConnectTCPClient: Sending disarm message");
                                        _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        _isDeviceArmed = false;
                                        Task.Run(() =>
                                        {
                                            (Application.Current as App)?.LmDisarmDevice();
                                        });

                                    }
                                    else if (_isDeviceArmed)
                                    {
                                        Logger.Log("OpenConnectTCPClient: Sending disarm message");
                                        _howRecentlyArmedOrDisarmed = DateTimeOffset.Now.ToUnixTimeSeconds();
                                        _isDeviceArmed = false;
                                        Task.Run(() =>
                                        {
                                            (Application.Current as App)?.LmDisarmDeviceWithDelay();
                                        });

                                    }
                                    break;
                                }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Logger.Log($"Error deserializing JSON: {ex.Message}");
                    Logger.Log($"Problematic message: {message}");
                }
            }
            
            // Clear buffer and keep only the incomplete message (if any)
            _messageBuffer.Clear();
            if (lastMessageIncomplete && messages.Length > 0)
            {
                _messageBuffer.Append(messages[messages.Length - 1]);
            }
        }
        protected override void OnError(System.Net.Sockets.SocketError error)
        {
            Logger.Log($"OpenConnectTCPClient: Socket error: {error}");
            Application.Current.Dispatcher.Invoke(() =>
            {
                if(App.SharedVm != null) App.SharedVm.GsProStatus = "SERVER ERROR";
            });
        }
        // Implement your logic for processing the response
        private void ProcessResponse(OpenConnectApiResponse response)
        {
            if (response.Player != null)
            {
                var playerHanded = response.Player.Handed;
                if (App.SharedVm != null)
                {
                    App.SharedVm.GsProHanded = playerHanded switch
                    {
                        Handed.Rh => "RIGHT",
                        Handed.Lh => "LEFT",
                        _ => ""
                    };
                }

                var playerClub = response.Player.Club;
                if (playerClub.HasValue)
                {
                    Logger.Log($"OpenConnectTCPClient: Response: {playerClub.Value}");
                    if (playerClub == Club.Pt)
                    {
                        Logger.Log("OpenConnectTCPClient: Club selection is a putter");
                        if (DeviceManager.Instance != null) DeviceManager.Instance.ClubSelection = "PT";
                        if (App.SharedVm != null) App.SharedVm.GsProClub = "PT";
                        if (_isPutting == false)
                        {
                            _isPutting = true;
                            Task.Run(() =>
                            {
                                (Application.Current as App)?.StartPutting();
                            });
                            
                        }
                    }
                    else
                    {
                        Logger.Log($"OpenConnectTCPClient: Club selection is NOT a putter, it is {playerClub.Value}");
                        if (DeviceManager.Instance != null) DeviceManager.Instance.ClubSelection = playerClub.Value.ToString();
                        if (App.SharedVm != null) App.SharedVm.GsProClub = playerClub.Value.ToString().ToUpperInvariant();
                        if (_isPutting)
                        {
                            _isPutting = false; 
                            Task.Run(() =>
                            {
                                (Application.Current as App)?.StopPutting();
                                if (App.SharedVm != null) App.SharedVm.PuttingStatus = "CONNECTED, PUTTER NOT SELECTED";
                            });
                            
                        }
                    }
                }
                else
                {
                    Logger.Log("OpenConnectTCPClient: No club information available");
                    if (DeviceManager.Instance != null) DeviceManager.Instance.ClubSelection = "";
                    if (App.SharedVm != null) App.SharedVm.GsProClub = "";
                }
            }
            else
            {
                Logger.Log("OpenConnectTCPClient: Invalid response or no player information");
                if (DeviceManager.Instance != null) DeviceManager.Instance.ClubSelection = "";
                if (App.SharedVm != null)
                {
                    App.SharedVm.GsProClub = "";
                    App.SharedVm.GsProHanded = "";
                }
            }
        }

    }

    public class OpenConnectApiMessage
    {
        private static OpenConnectApiMessage? _instance;
        public static OpenConnectApiMessage Instance
        {
            get
            {
                return _instance ??= new OpenConnectApiMessage();
            }
        }

        public string DeviceId => SettingsManager.Instance.Settings?.OpenConnect?.PretendSquareGolf == true ? "SquareGolf" : "GSPRO-MLM2PRO"; // GSPRO-MLM2PRO SquareGolf
        public string Units { get => "Yards"; }
        public int ShotNumber { get; set; }
        public string ApiVersion { get => "1"; }
        public BallData? BallData { get; init; }
        public ClubData? ClubData { get; init; }
        public ShotDataOptions? ShotDataOptions { get; set; }
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
                    Speed = input.BallData?.Speed ?? 0,
                    SpinAxis = input.BallData?.SpinAxis ?? 0,
                    TotalSpin = input.BallData?.TotalSpin ?? 0,
                    BackSpin = input.BallData?.BackSpin ?? 0,
                    SideSpin = input.BallData?.SideSpin ?? 0,
                    Hla = input.BallData?.Hla ?? 0,
                    Vla = input.BallData?.Vla ?? 0
                },
                ClubData = new ClubData()
                {
                    Speed = input.ClubData?.Speed ?? 0
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
        private int CalculateBackSpin(double totalSpin, double spinAxis)
        {
            return (int)Math.Round(totalSpin * Math.Cos(DegreesToRadians(spinAxis)));
        }

        private int CalculateSideSpin(double totalSpin, double spinAxis)
        {
            return (int)Math.Round(totalSpin * Math.Sin(DegreesToRadians(spinAxis)));
        }

        private static double DegreesToRadians(double degrees)
        {
            return (Math.PI / 180) * degrees;
        }
        public OpenConnectApiMessage TestShot()
        {
            Instance.ShotNumber++;
            // Create a Random instance
            var random = new Random();

            // Generate random values within the desired range
            double speed = Math.Round(random.NextDouble() * (160 - 30) + 30, 1);
            double spinAxis = Math.Round(random.NextDouble() * (20 - -20) + 0, 1);
            double totalSpin = Math.Round(random.NextDouble() * (13000 - 2000) + 2000, 0);
            double backSpin = CalculateBackSpin(totalSpin, spinAxis);
            double sideSpin = CalculateSideSpin(totalSpin, spinAxis);
            double hla = Math.Round(random.NextDouble() * (5.0 - -5.0) + 0.0, 1);
            double vla = Math.Round(random.NextDouble() * (40 - 10) + 10, 1);
            double clubSpeed = Math.Round(random.NextDouble() * (160 - 30) + 30, 1);

            return new OpenConnectApiMessage()
            {
                ShotNumber = ShotNumber,
                BallData = new BallData()
                {
                    Speed = speed,
                    SpinAxis = spinAxis,
                    TotalSpin = totalSpin,
                    BackSpin = backSpin,
                    SideSpin = sideSpin,
                    Hla = hla,
                    Vla = vla
                },
                ClubData = new ClubData()
                {
                    Speed = clubSpeed
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
        public double BackSpin { get; set; }
        public double SideSpin { get; set; }
        public double Hla { get; set; }
        public double Vla { get; set; }
        // public double CarryDistance { get; set; }

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
        [EnumMember(Value = "RH")]
        Rh,
        [EnumMember(Value = "LH")]
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
        [EnumMember(Value = "1I")]
        _1I,
        [EnumMember(Value = "2I")]
        _2I,
        [EnumMember(Value = "3I")]
        _3I,
        [EnumMember(Value = "4I")]
        _4I,
        [EnumMember(Value = "5I")]
        _5I,
        [EnumMember(Value = "6I")]
        _6I,
        [EnumMember(Value = "7I")]
        _7I,
        [EnumMember(Value = "8I")]
        _8I,
        [EnumMember(Value = "9I")]
        _9I,
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
