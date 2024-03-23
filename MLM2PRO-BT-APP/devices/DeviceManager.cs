using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP.devices
{
    class DeviceManager
    {
        public static DeviceManager? Instance { get; } = new();
        private string UserToken { get; set; } = "0";

        private readonly ByteConversionUtils _byteConversionUtils = new ByteConversionUtils();
        public string DeviceStatus { get; set; } = "NOT CONNECTED";
        public string ClubSelection { get; set; } = "NONE";
        private byte? Handedness { get; set; } = 1; // Default value of right?
        private byte? BallType { get; set; } = 2; // Default value of rct?
        private byte? Environment { get; set; } = 0; // Default value of outdoor?
        private double? AltitudeMetres { get; set; } = 0.0; // Default value of 0.0
        private double? TemperatureCelsius { get; set; } = 20.0; // Default value of 0.0
        private byte? QuitEvent { get; set; } = 0; // Default value of 0
        private byte? PowerMode { get; set; } = 0; // Default value of 0

        // DeviceInfo fields
        private string SerialNumber { get; set; } = "";
        private string Model { get; set; } = "";
        private int Battery { get; set; } = 0;
        public int[]? ResponseMessage { get; set; } = null;
        public int[]? Events { get; set; } = null;
        public int[]? Measurement { get; set; } = null;
        private bool _infoComplete = false;

        public bool DeviceInfoComplete()
        {
            return _infoComplete;
        }

        public void ResetDeviceInfo()
        {
            SerialNumber = "";
            Model = "";
            Battery = 0;
            ResponseMessage = null;
            Events = null;
            Measurement = null;
            _infoComplete = false;
        }

        private void UpdateInfoComplete()
        {
            if (!string.IsNullOrEmpty(SerialNumber) && !string.IsNullOrEmpty(Model) && Battery > 0)
            {
                _infoComplete = true;
            }
            else
            {
                _infoComplete = false;
            }
        }

        public void UpdateSerialNumber(string serialNumber)
        {
            SerialNumber = serialNumber;
            UpdateInfoComplete();
        }

        public void UpdateModel(string model)
        {
            Model = model;
            UpdateInfoComplete();
        }

        public void UpdateBatteryLevel(int batteryLevel)
        {
            if (batteryLevel != 0)
            {
                Battery = batteryLevel;
                UpdateInfoComplete();
            }
        }

        public void UpdateEvents(byte[]? events)
        {
            if (events != null)
            {
                Events = ByteConversionUtils.ArrayByteToInt(events);
            }
        }

        public void UpdateResponseMessage(byte[]? responseMessage)
        {
            if (responseMessage != null)
            {
                ResponseMessage = ByteConversionUtils.ArrayByteToInt(responseMessage);
            }
        }

        public void UpdateMeasurement(byte[]? measurement)
        {
            if (measurement != null)
            {
                Measurement = ByteConversionUtils.ArrayByteToInt(measurement);
            }
        }

        public byte[]? GetInitialParameters(string tokenInput)
        {
            UserToken = tokenInput;
            Logger.Log("GetInitialParameters: UserToken: " + UserToken);


            // Generate required byte arrays
            byte[] airPressureBytes = _byteConversionUtils.GetAirPressureBytes(0.0) ?? [0];
            byte[] temperatureBytes = _byteConversionUtils.GetTemperatureBytes(15.0) ?? [0];
            byte[] longToUintToByteArray = _byteConversionUtils.LongToUintToByteArray(long.Parse(UserToken), true) ?? [0];

            // Concatenate all byte arrays
            byte[]? concatenatedBytes = new byte[] { 1, 2, 0, 0 }.Concat(airPressureBytes)
             .Concat(temperatureBytes)
             .Concat(longToUintToByteArray)
             .Concat(new byte[] { 0, 0 })
             .ToArray();

            Logger.Log("GetInitialParameters: ByteArrayReturned: " + _byteConversionUtils.ByteArrayToHexString(concatenatedBytes));
            return concatenatedBytes;
        }

        public byte[] GetParametersFromSettings()
        {
            if (string.IsNullOrEmpty(UserToken))
            {
                return new byte[0];
            }

            double altitude = AltitudeMetres ?? 0.0;
            double temperature = TemperatureCelsius ?? 15.0;

            byte[] handednessBytes = Handedness != null ? [Handedness.Value] : [1];
            byte[] ballTypeBytes = BallType != null ? [BallType.Value] : [2];
            byte[] environmentBytes = Environment != null ? [Environment.Value] : [0];
            byte[] quitEventBytes = QuitEvent != null ? [QuitEvent.Value] : [0];
            byte[] powerModeBytes = PowerMode != null ? [PowerMode.Value] : [0];

            byte[] airPressureBytes = _byteConversionUtils.GetAirPressureBytes(altitude) ?? [0];
            byte[] temperatureBytes = _byteConversionUtils.GetTemperatureBytes(temperature) ?? [0];
            byte[] userTokenBytes = BitConverter.GetBytes(long.Parse(UserToken));

            byte[] concatenatedBytes = handednessBytes
                .Concat(ballTypeBytes)
                .Concat(environmentBytes)
                .Concat(new byte[] { 0 })
                .Concat(airPressureBytes)
                .Concat(temperatureBytes)
                .Concat(userTokenBytes)
                .Concat(quitEventBytes)
                .Concat(powerModeBytes)
                .ToArray();

            return concatenatedBytes;
        }

    }
}
