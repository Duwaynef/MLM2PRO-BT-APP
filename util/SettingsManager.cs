﻿using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace MLM2PRO_BT_APP.util
{
    public class SettingsManager
    {
        private static SettingsManager _instance = null!;
        public static SettingsManager Instance => _instance ??= new SettingsManager();
        public event EventHandler? SettingsUpdated;

        private readonly string _settingsFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Settings.json");

        public AppSettings? Settings { get; private set; }

        private SettingsManager()
        {
            LoadSettings(); // Optionally load settings upon instantiation
        }

        public void SaveSettings()
        {
            var settingsJson = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(_settingsFilePath, settingsJson);
            SettingsUpdated?.Invoke(this, EventArgs.Empty);
        }

        public void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string settingsJson = File.ReadAllText(_settingsFilePath);
                    Settings = JsonConvert.DeserializeObject<AppSettings>(settingsJson);
                    Debug.WriteLine("Settings loaded successfully.");
                }
                else
                {
                    InitializeDefaultSettings();
                    SaveSettings(); // Save the defaults for future use
                    Debug.WriteLine("Default settings initialized and saved.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading settings: {ex.Message}");
                InitializeDefaultSettings();
                SaveSettings(); // Attempt to save defaults after a failed load attempt
            }
        }
        
        public void ClearSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                File.Delete(_settingsFilePath);
            }
            InitializeDefaultSettings();
            SaveSettings();
            Debug.WriteLine("All settings cleared and reset to default.");
        }

        private void InitializeDefaultSettings()
        {
            Settings = new AppSettings
            {
                OpenConnect = new OpenConnectSettings(),
                WebApiSettings = new WebApiSettings(),
                LaunchMonitor = new LaunchMonitorSettings(),
                Putting = new PuttingSettings()
            };
        }

        public class OpenConnectSettings
        {
            public bool AutoStartGSPro { get; set; } = false;
            public bool SkipGSProLauncher { get; set; } = true;
            public string GSProIp { get; set; } = "127.0.0.1";
            public int GSProPort { get; set; } = 921;
            public string GSProEXE { get; set; } = "C:\\GSProV1\\Core\\GSP\\GSPro.exe";
            public bool EnableAPIRelay { get; set; } = false;
            public int APIRelayPort { get; set; } = 951;
        }
        

        public class WebApiSettings
        {
            public string WebApiURL { get; set; } = "https://mlm.rapsodo.com/api/simulator/user/";
            public string WebApiSecret { get; set; } = "";
            public string WebApiToken { get; set; } = "";
            public int WebApiUserId { get; set; } = 0;
            public long WebApiExpireDate { get; set; } = 0;
            public int WebApiDeviceId { get; set; } = 0;
        }

        public class LaunchMonitorSettings
        {
            public bool AutoStartLaunchMonitor { get; set; } = true;
            public string BluetoothDeviceName { get; set; } = "MLM2-";
            public int ReconnectInterval { get; set; } = 10;

            public bool AutoWake { get; set; } = true;

            //public bool DebugLogging { get; set; } = false;
            //public int Altitude { get; set; } = 0;
            //public double Humidity { get; set; } = 0.5;
            //public int Temperature { get; set; } = 60;
            //public double AirDensity { get; set; } = 1.225;
            //public byte? Handedness { get; set; } = 1; // Default value of right?
            //public byte? BallType { get; set; } = 2; // Default value of rct?
            //public byte? Environment { get; set; } = 0; // Default value of outdoor?
            //public double? AltitudeMetres { get; set; } = 0.0; // Default value of 0.0
            //public double? TemperatureCelsius { get; set; } = 20.0; // Default value of 0.0
            //public byte? QuitEvent { get; set; } = 0; // Default value of 0
            //public byte? PowerMode { get; set; } = 0; // Default value of 0
            //public string SerialNumber { get; set; } = "";
            //public string Model { get; set; } = "";
            //public int[] Battery { get; set; } = null;
            //public int[] ResponseMessage { get; set; } = null;
            //public int[] Events { get; set; } = null;
            //public int[] Measurement { get; set; } = null;
            private bool infoComplete = false;
        }

        public class PuttingSettings
        {
            public bool PuttingEnabled { get; set; } = false;
            public bool AutoStartPutting { get; set; } = true;
            public bool HideConsoleWindow { get; set; } = true;
            public int PuttingPort { get; set; } = 8888;
            public bool LaunchBallTracker { get; set; } = true;
            public bool OnlyLaunchWhenPutting { get; set; } = true;
            public bool KeepPuttingCamOnTop { get; set; } = true;
            public int WebcamIndex { get; set; } = 0;
            public string BallColor { get; set; } = "white";
            public int CamPreviewWidth { get; set; } = 450;
            public string ExePath { get; set; } = "./ball_tracking/ball_tracking.exe";
            public string AdditionalExeArgs { get; set; } = "";
            public bool HideExeLogs { get; set; } = true;
        }

        public class AppSettings
        {
            public OpenConnectSettings? OpenConnect { get; init; }
            public WebApiSettings? WebApiSettings { get; init; }
            public LaunchMonitorSettings? LaunchMonitor { get; init; }
            public PuttingSettings? Putting { get; init; }
        }

    }
}