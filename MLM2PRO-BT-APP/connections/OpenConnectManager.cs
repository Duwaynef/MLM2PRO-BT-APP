using System.Text;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TcpClient = NetCoreServer.TcpClient;

namespace MLM2PRO_BT_APP
{
    class OpenConnectTCPClient : TcpClient
    {
        PlayerInfo playerInfo;
        public OpenConnectTCPClient(string serverHost, int serverPort) : base("127.0.0.1", 921) { }

        protected override void OnConnected()
        {
            Logger.Log("OpenConnectTCPClient: Connected to server.");
            // Update shared view model status
            Application.Current.Dispatcher.Invoke(() =>
            {
                App.SharedVM.GSProStatus = "CONNECTED";
            });
        }

        protected override void OnDisconnected()
        {
            Logger.Log("OpenConnectTCPClient: Disconnected from server.");
            // Update shared view model status
            Application.Current.Dispatcher.Invoke(() =>
            {
                App.SharedVM.GSProStatus = "DISCONNECTED";
            });
            
        }

        public async Task<bool> SendDataAsync(OpenConnectApiMessage message)
        {
            string jsonMessage = JsonConvert.SerializeObject(message);
            byte[] data = Encoding.UTF8.GetBytes(jsonMessage);
            return SendAsync(data);
        }

        public async Task<bool> SendDirectJsonAsync(string json)
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            return SendAsync(data);
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            string message = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
            var response = JsonConvert.DeserializeObject<OpenConnectApiResponse>(message);
            if (response.Code == 201)
            {
                playerInfo = response.Player;
                ProcessResponse(response);
            }
        }

        protected override void OnError(System.Net.Sockets.SocketError error)
        {
            Logger.Log($"OpenConnectTCPClient: Socket error: {error}");
            Application.Current.Dispatcher.Invoke(() =>
            {
                App.SharedVM.GSProStatus = "SERVER ERROR";
            });
        }

        // Implement your logic for processing the response
        private void ProcessResponse(OpenConnectApiResponse response)
        {
            String clubSeclection = JsonConvert.SerializeObject(playerInfo.Club);
            Logger.Log($"OpenConnectTCPClient: Response: {clubSeclection}");
            if (clubSeclection == "26")
            {
                Logger.Log("OpenConnectTCPClient: Club selection is a putter");
            }
            else
            {
                Logger.Log("OpenConnectTCPClient: Club selection is NOT a putter");
            }
        }
    }

    public class OpenConnectApiMessage
    {
        private static OpenConnectApiMessage instance;
        public static OpenConnectApiMessage Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new OpenConnectApiMessage();
                }
                return instance;
            }
        }

        public string DeviceID { get { return "GSPRO-MLM2PRO"; } }
        public string Units { get { return "Yards"; } }
        public int ShotNumber { get; set; }
        public int ShotCounter { get; set; } = 0;
        public string APIVersion { get { return "1"; } }
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

        public static OpenConnectApiMessage CreateShotData(BallData ballData, ClubData clubData, int ShotCounter)
        {
            double speed = ballData.Speed;
            double spinAxis = ballData.SpinAxis;
            double totalSpin = ballData.TotalSpin;
            // double sideSpin = ballData.SideSpin;
            // double backSpin = ballData.BackSpin;
            double hla = ballData.HLA;
            double vla = ballData.VLA;
            double clubspeed = clubData.Speed;

            return new OpenConnectApiMessage()
            {
                ShotNumber = ShotCounter,
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

        public static OpenConnectApiMessage TestShot(int ShotCounter)
        {
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
                ShotNumber = ShotCounter,
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
        public int Code { get; set; }
        public string Message { get; set; }
        public PlayerInfo Player { get; set; }
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
        RH,
        LH
    }

    public enum Club
    {
        unknown,
        DR,
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
        PW,
        GW,
        SW,
        LW,
        PT
    }
}
