using System;
using System.Linq;

namespace MLM2PRO_BT_APP
{
    class DeviceManager
    {

        private static DeviceManager instance;
        public static DeviceManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new DeviceManager();
                }
                return instance;
            }
        }
        public string UserToken { get; set; } = "0";

        ByteConversionUtils byteConversionUtils = new ByteConversionUtils();
        public string DeviceStatus { get; set; } = "NOTCONNECTED";
        public string ClubSelection { get; set; } = "NONE";
        public byte? Handedness { get; set; } = 1; // Default value of right?
        public byte? BallType { get; set; } = 2; // Default value of rct?
        public byte? Environment { get; set; } = 0; // Default value of outdoor?
        public double? AltitudeMetres { get; set; } = 0.0; // Default value of 0.0
        public double? TemperatureCelcius { get; set; } = 20.0; // Default value of 0.0
        public byte? QuitEvent { get; set; } = 0; // Default value of 0
        public byte? PowerMode { get; set; } = 0; // Default value of 0

        // DeviceInfo fields
        public string SerialNumber { get; set; } = "";
        public string Model { get; set; } = "";
        public int Battery { get; set; } = 0;
        public int[] ResponseMessage { get; set; } = null;
        public int[] Events { get; set; } = null;
        public int[] Measurement { get; set; } = null;
        private bool infoComplete = false;

        public bool DeviceInfoComplete()
        {
            return infoComplete;
        }

        public void ResetDeviceInfo()
        {
            SerialNumber = "";
            Model = "";
            Battery = 0;
            ResponseMessage = null;
            Events = null;
            Measurement = null;
            infoComplete = false;
        }

        private void UpdateInfoComplete()
        {
            if (!string.IsNullOrEmpty(SerialNumber) && !string.IsNullOrEmpty(Model)
                && Battery != null && Battery > 0)
            {
                infoComplete = true;
            }
            else
            {
                infoComplete = false;
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

        public void UpdateEvents(byte[] events)
        {
            if (events != null)
            {
                Events = byteConversionUtils.ArrayByteToInt(events);
            }
        }

        public void UpdateResponseMessage(byte[] responseMessage)
        {
            if (responseMessage != null)
            {
                ResponseMessage = byteConversionUtils.ArrayByteToInt(responseMessage);
            }
        }

        public void UpdateMeasurement(byte[] measurement)
        {
            if (measurement != null)
            {
                Measurement = byteConversionUtils.ArrayByteToInt(measurement);
            }
        }

        public byte[] GetInitialParameters(string tokenInput)
        {
            UserToken = tokenInput;
            Logger.Log("GetInitialParameters: UserToken: " + UserToken);


            // Generate required byte arrays
            byte[] airPressureBytes = byteConversionUtils.GetAirPressureBytes(0.0);
            byte[] temperatureBytes = byteConversionUtils.GetTemperatureBytes(15.0);
            byte[] longToUintToByteArray = byteConversionUtils.LongToUintToByteArray(long.Parse(UserToken), true);

            // Concatenate all byte arrays
            byte[] concatenatedBytes = new byte[] { 1, 2, 0, 0 }.Concat(airPressureBytes)
             .Concat(temperatureBytes)
             .Concat(longToUintToByteArray)
             .Concat(new byte[] { 0, 0 })
             .ToArray();

            Logger.Log("GetInitialParameters: ByteArrayReturned: " + byteConversionUtils.ByteArrayToHexString(concatenatedBytes));
            return concatenatedBytes;
        }

        public byte[] GetParametersFromSettings()
        {
            if (string.IsNullOrEmpty(UserToken))
            {
                return null;
            }

            double altitude = AltitudeMetres ?? 0.0;
            double temperature = TemperatureCelcius ?? 15.0;

            byte[] handednessBytes = Handedness != null ? new byte[] { Handedness.Value } : new byte[] { 1 };
            byte[] ballTypeBytes = BallType != null ? new byte[] { BallType.Value } : new byte[] { 2 };
            byte[] environmentBytes = Environment != null ? new byte[] { Environment.Value } : new byte[] { 0 };
            byte[] quitEventBytes = QuitEvent != null ? new byte[] { QuitEvent.Value } : new byte[] { 0 };
            byte[] powerModeBytes = PowerMode != null ? new byte[] { PowerMode.Value } : new byte[] { 0 };

            byte[] airPressureBytes = byteConversionUtils.GetAirPressureBytes(altitude);
            byte[] temperatureBytes = byteConversionUtils.GetTemperatureBytes(temperature);
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
