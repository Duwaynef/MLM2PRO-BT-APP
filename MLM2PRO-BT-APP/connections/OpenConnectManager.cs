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
        private bool _isDeviceArmed = false;
        public OpenConnectTcpClient() : base(SettingsManager.Instance?.Settings?.OpenConnect?.GSProIp ?? "127.0.0.1", SettingsManager.Instance?.Settings?.OpenConnect?.GSProPort ?? 931) 
        {
            
        }
        public void DisconnectAndStop()
        {
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
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
            }

        }
        protected override void OnError(System.Net.Sockets.SocketError error)
        {
            Logger.Log($"OpenConnectTCPClient: Socket error: {error}");
            Application.Current.Dispatcher.Invoke(() =>
            {
                if(App.SharedVm != null) App.SharedVm.GSProStatus = "SERVER ERROR";
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
                        if (App.SharedVm != null) App.SharedVm.GSProClub = "PT";
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
                        DeviceManager.Instance.ClubSelection = playerClub.Value.ToString();
                        if (App.SharedVm != null) App.SharedVm.GSProClub = playerClub.Value.ToString();
                        if (_isPutting == true)
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
        private static OpenConnectApiMessage? _instance;
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
        public BallData? BallData { get; set; }
        public ClubData? ClubData { get; set; }
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
                    HLA = input.BallData?.HLA ?? 0,
                    VLA = input.BallData?.VLA ?? 0
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
        public int CalculateBackSpin(double totalSpin, double spinAxis)
        {
            return (int)Math.Round(totalSpin * Math.Cos(DegreesToRadians(spinAxis)));
        }

        public int CalculateSideSpin(double totalSpin, double spinAxis)
        {
            return (int)Math.Round(totalSpin * Math.Sin(DegreesToRadians(spinAxis)));
        }

        private double DegreesToRadians(double degrees)
        {
            return (Math.PI / 180) * degrees;
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
            double BackSpin = CalculateBackSpin(totalSpin, spinAxis);
            double SideSpin = CalculateSideSpin(totalSpin, spinAxis);
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
                    BackSpin = BackSpin,
                    SideSpin = SideSpin,
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
        public double BackSpin { get; set; }
        public double SideSpin { get; set; }
        public double HLA { get; set; }
        public double VLA { get; set; }
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
