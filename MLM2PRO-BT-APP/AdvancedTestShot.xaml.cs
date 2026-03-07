using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MLM2PRO_BT_APP.connections;
using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP
{
    public partial class AdvancedTestShot
    {
        private static class ClubSpecs
        {
            public static readonly Dictionary<string, (double BallSpeed, double SpinAxis, double TotalSpin, double BackSpin, double SideSpin, double Hla, double Vla, double ClubSpeed)> Specs = new()
            {
                { "Driver", (160, 0, 2500, 2500, 0, 0, 15, 115) },
                { "3W", (150, 0, 3500, 3500, 0, 0, 16, 110) },
                { "5W", (145, 0, 4000, 4000, 0, 0, 17, 105) },
                { "5I", (125, 0, 6500, 6500, 0, 0, 19, 90) },
                { "6I", (122, 0, 6800, 6800, 0, 0, 20, 88) },
                { "7I", (118, 0, 7100, 7100, 0, 0, 21, 85) },
                { "8I", (115, 0, 7400, 7400, 0, 0, 22, 82) },
                { "9I", (110, 0, 7800, 7800, 0, 0, 24, 80) },
                { "PW", (105, 0, 8500, 8500, 0, 0, 28, 75) },
                { "SW", (92, 0, 10000, 10000, 0, 0, 32, 65) },
                { "LW", (88, 0, 11000, 11000, 0, 0, 35, 60) }
            };
        }

        private static class DistanceSpecs
        {
            public static readonly Dictionary<int, (double BallSpeed, double SpinAxis, double TotalSpin, double BackSpin, double SideSpin, double Hla, double Vla, double ClubSpeed)> Specs = new()
            {
                { 20, (30, 0, 3000, 3000, 0, 0, 35, 45) },
                { 30, (39, 0, 4500, 4500, 0, 0, 32, 55) },
                { 40, (46, 0, 6000, 6000, 0, 0, 28, 65) },
                { 50, (56, 0, 7000, 7000, 0, 0, 25, 75) }
            };
        }

        public AdvancedTestShot()
        {
            InitializeComponent();
            AttachSliderValueChangeHandlers();
        }

        private void AttachSliderValueChangeHandlers()
        {
            BallSpeedSlider.ValueChanged += (s, e) => UpdateSliderValue(BallSpeedValue, BallSpeedSlider);
            SpinAxisSlider.ValueChanged += (s, e) => UpdateSliderValue(SpinAxisValue, SpinAxisSlider, "0.0");
            TotalSpinSlider.ValueChanged += (s, e) => UpdateSliderValue(TotalSpinValue, TotalSpinSlider, "0");
            BackSpinSlider.ValueChanged += (s, e) => UpdateSliderValue(BackSpinValue, BackSpinSlider, "0");
            SideSpinSlider.ValueChanged += (s, e) => UpdateSliderValue(SideSpinValue, SideSpinSlider, "0");
            HlaSlider.ValueChanged += (s, e) => UpdateSliderValue(HlaValue, HlaSlider, "0.0");
            VlaSlider.ValueChanged += (s, e) => UpdateSliderValue(VlaValue, VlaSlider, "0.0");
            ClubSpeedSlider.ValueChanged += (s, e) => UpdateSliderValue(ClubSpeedValue, ClubSpeedSlider);
        }

        private static void UpdateSliderValue(TextBlock textBlock, Slider slider, string format = "0")
        {
            textBlock.Text = slider.Value.ToString(format);
        }

        private void Club_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string clubTag)
            {
                if (ClubSpecs.Specs.TryGetValue(clubTag, out var specs))
                {
                    SetSliderValues(specs);
                    SendShot();
                }
            }
        }

        private void Distance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string distanceTag && int.TryParse(distanceTag, out var distance))
            {
                if (DistanceSpecs.Specs.TryGetValue(distance, out var specs))
                {
                    SetSliderValues(specs);
                    SendShot();
                }
            }
        }

        private void SetSliderValues((double BallSpeed, double SpinAxis, double TotalSpin, double BackSpin, double SideSpin, double Hla, double Vla, double ClubSpeed) specs)
        {
            BallSpeedSlider.Value = specs.BallSpeed;
            SpinAxisSlider.Value = specs.SpinAxis;
            TotalSpinSlider.Value = specs.TotalSpin;
            BackSpinSlider.Value = specs.BackSpin;
            SideSpinSlider.Value = specs.SideSpin;
            HlaSlider.Value = specs.Hla;
            VlaSlider.Value = specs.Vla;
            ClubSpeedSlider.Value = specs.ClubSpeed;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            await SendShot();
        }

        private async Task SendShot()
        {
            OpenConnectApiMessage.Instance.ShotNumber++;

            var message = new OpenConnectApiMessage
            {
                ShotNumber = OpenConnectApiMessage.Instance.ShotNumber,
                BallData = new BallData
                {
                    Speed = BallSpeedSlider.Value,
                    SpinAxis = SpinAxisSlider.Value,
                    TotalSpin = TotalSpinSlider.Value,
                    BackSpin = BackSpinSlider.Value,
                    SideSpin = SideSpinSlider.Value,
                    Hla = HlaSlider.Value,
                    Vla = VlaSlider.Value
                },
                ClubData = new ClubData
                {
                    Speed = ClubSpeedSlider.Value
                },
                ShotDataOptions = new ShotDataOptions
                {
                    ContainsBallData = true,
                    ContainsClubData = true,
                    LaunchMonitorIsReady = true,
                    IsHeartBeat = false
                }
            };

            try
            {
                if (Application.Current is App app)
                {
                    await app.SendShotData(message);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error sending advanced test shot: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
