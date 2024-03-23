using MLM2PRO_BT_APP.connections;

namespace MLM2PRO_BT_APP.devices
{
    class MeasurementData
    {
        private static MeasurementData? _instance;
        public static MeasurementData Instance
        {
            get => _instance ??= new MeasurementData();
        }
        private double ClubHeadSpeed { get; set; }
        private double BallSpeed { get; set; }
        private double Vla { get; set; }
        private double Hla { get; set; }
        private double SpinAxis { get; set; }
        private int TotalSpin { get; set; }
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }

        private static int CalculateBackSpin(int totalSpin, double spinAxis)
        {
            return (int)Math.Round(totalSpin * Math.Cos(DegreesToRadians(spinAxis)));
        }

        private static int CalculateSideSpin(int totalSpin, double spinAxis)
        {
            return (int)Math.Round(totalSpin * Math.Sin(DegreesToRadians(spinAxis)));
        }

        private static double DegreesToRadians(double degrees)
        {
            return (Math.PI / 180) * degrees;
        }

        public static double CalculateSmashFactor(double ballSpeed, double clubHeadSpeed)
        {
            if (clubHeadSpeed == 0) return 0;
            return Math.Round(ballSpeed / clubHeadSpeed, 2);
        }

        public OpenConnectApiMessage ConvertHexToMeasurementData(string? hexData)
        {
            const double multiplier = 2.2375;
            byte[] bytes = Enumerable.Range(0, hexData?.Length ?? 0)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hexData?.Substring(x, 2), 16))
                             .ToArray();

            ClubHeadSpeed = Math.Round(BitConverter.ToInt16(bytes, 0) / 10.0 * multiplier, 2); // Round to 2 decimal places
            BallSpeed = Math.Round(BitConverter.ToInt16(bytes, 2) / 10.0 * multiplier, 2); // Round to 2 decimal places
            Hla = BitConverter.ToInt16(bytes, 4) / 10.0;
            Vla = BitConverter.ToInt16(bytes, 6) / 10.0;
            SpinAxis = BitConverter.ToInt16(bytes, 8) / 10.0;
            TotalSpin = BitConverter.ToUInt16(bytes, 10);
            Unknown1 = BitConverter.ToUInt16(bytes, 12); // carry distance?
            Unknown2 = BitConverter.ToUInt16(bytes, 14); // total distance? both seem lower than AG, but not crazy off...
            // Serialize MeasurementData instance to JSON string

            double backSpin = CalculateBackSpin(TotalSpin, SpinAxis);
            double sideSpin = CalculateSideSpin(TotalSpin, SpinAxis);
            OpenConnectApiMessage.Instance.ShotNumber++;
            return new OpenConnectApiMessage()
            {
                ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
                BallData = new BallData()
                {
                    Speed = BallSpeed,
                    SpinAxis = SpinAxis,
                    TotalSpin = TotalSpin,
                    BackSpin = backSpin,
                    SideSpin = sideSpin,
                    Hla = Hla,
                    Vla = Vla,
                },
                ClubData = new ClubData()
                {
                    Speed = ClubHeadSpeed
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
}