using MLM2PRO_BT_APP.connections;

namespace MLM2PRO_BT_APP.devices
{
    class MeasurementData
    {
        private static MeasurementData instance;
        public static MeasurementData Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MeasurementData();
                }
                return instance;
            }
        }
        public double ClubHeadSpeed { get; set; }
        public double BallSpeed { get; set; }
        public double VLA { get; set; }
        public double HLA { get; set; }
        public double SpinAxis { get; set; }
        public int TotalSpin { get; set; }
        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }

        public int CalculateBackSpin(int totalSpin, double spinAxis)
        {
            return (int)Math.Round(totalSpin * Math.Cos(DegreesToRadians(spinAxis)));
        }

        public int CalculateSideSpin(int totalSpin, double spinAxis)
        {
            return (int)Math.Round(totalSpin * Math.Sin(DegreesToRadians(spinAxis)));
        }

        private double DegreesToRadians(double degrees)
        {
            return (Math.PI / 180) * degrees;
        }

        public static double CalculateSmashFactor(double ballSpeed, double clubheadSpeed)
        {
            if (clubheadSpeed == 0) return 0;
            return Math.Round(ballSpeed / clubheadSpeed, 2);
        }

        public OpenConnectApiMessage ConvertHexToMeasurementData(string? hexData)
        {
            double multiplier = 2.2375;
            byte[] bytes = Enumerable.Range(0, hexData.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hexData.Substring(x, 2), 16))
                             .ToArray();

            ClubHeadSpeed = Math.Round(BitConverter.ToInt16(bytes, 0) / 10.0 * multiplier, 2); // Round to 2 decimal places
            BallSpeed = Math.Round(BitConverter.ToInt16(bytes, 2) / 10.0 * multiplier, 2); // Round to 2 decimal places
            HLA = BitConverter.ToInt16(bytes, 4) / 10.0;
            VLA = BitConverter.ToInt16(bytes, 6) / 10.0;
            SpinAxis = BitConverter.ToInt16(bytes, 8) / 10.0;
            TotalSpin = BitConverter.ToUInt16(bytes, 10);
            Unknown1 = BitConverter.ToUInt16(bytes, 12); // carry disance?
            Unknown2 = BitConverter.ToUInt16(bytes, 14); // total distance? both seem lower than AG, but not crazy off...
            // Serialize MeasurementData instance to JSON string

            double BackSpin = CalculateBackSpin(TotalSpin, SpinAxis);
            double SideSpin = CalculateSideSpin(TotalSpin, SpinAxis);
            OpenConnectApiMessage.Instance.ShotNumber++;
            return new OpenConnectApiMessage()
            {
                ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
                BallData = new BallData()
                {
                    Speed = BallSpeed,
                    SpinAxis = SpinAxis,
                    TotalSpin = TotalSpin,
                    BackSpin = BackSpin,
                    SideSpin = SideSpin,
                    HLA = HLA,
                    VLA = VLA,
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