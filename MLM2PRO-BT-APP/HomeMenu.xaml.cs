using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using MaterialDesignThemes.Wpf;
using MLM2PRO_BT_APP.connections;
using MLM2PRO_BT_APP.util;
using static System.DateTime;

namespace MLM2PRO_BT_APP;

public partial class HomeMenu
{
    public HomeMenu()
    {
        InitializeComponent();
        EventAggregator.Instance.SnackBarMessagePublished += OnSnackBarMessagePublished;
        this.Unloaded += HomeMenu_Unloaded;
        DataContext = App.SharedVm;
        ShotDataDataGrid.ItemsSource = SharedViewModel.Instance.ShotDataCollection;
        DataGridSnackBar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(2000));
    }
    private void OnSnackBarMessagePublished(string message, int duration)
    {
        Logger.Log("SnackBarMessagePublished: " + message);
        HomeMenuSnackBarMessage(message, duration);
    }
    private async void GSPro_Connect_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.ConnectGsProButton();
        });        
    }
    private async void GSPro_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.DisconnectGsPro();
        });        
    }
    private async void GSPro_Send_TestShot_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.SendTestShotData();
        });        
    } 
    
    /* interesting code to hold on to
    private async Task Write05(object sender, RoutedEventArgs e)
    {
        byte[] byteArray = byteConversionUtils.StringToByteArray("010000000001488384561d45c4f21a079de55c24df9c7167f10d1964f2179bc8170dfbe2453a");
        byte[] byteKeyOutput = await manager.ConvertAuthRequest(byteArray);
        Logger.Log("Key: " + byteConversionUtils.ByteArrayToHexString(byteKeyOutput));

        // second decrypt attempt
        byte[] byteKeyOutput2 = byteConversionUtils.StringToByteArray("488384561D45C4F21A079DE55C24DF9C7167F10D1964F2179BC8170DFBE2453A");
        byte[] byteArray2 = byteConversionUtils.StringToByteArray("1b001070acd281e3ffff000000000900010100040082032b000000012e2700230004001b1100d5d19ddaf553e26e3d2ad9b478beff33e4baf6b7477854bb4e8354f92c8a3328");
        byte[] outputByteArr = btEncryption.DecryptKnownKey(byteArray2, byteKeyOutput2);
        Logger.Log("Decrypted Bytes: " + byteConversionUtils.ByteArrayToHexString(outputByteArr));
    }
    private void DecryptTextBox(object sender, RoutedEventArgs e)
    {
        String textBoxInput = "";//ConsoleInputTextBox.Text;
        String keyTextBoxInput = "";// KeyTextBox.Text;
        byte[] byteArray = byteConversionUtils.StringToByteArray(textBoxInput);
        byte[] byteArray2 = byteConversionUtils.StringToByteArray(keyTextBoxInput);

        byte[] outputByteArr = btEncryption.DecryptKnownKey(byteArray, byteArray2);
        Logger.Log("Decrypted Bytes: " + byteConversionUtils.ByteArrayToHexString(outputByteArr));
    }
    */
    
    public class ShotData
    {
        public int ShotNumber { get; init; }
        public string Result { get; init; } = "";
        public double SmashFactor { get; set; }
        public string Club { get; init; } = "";
        public double ClubSpeed { get; init; }
        public double BallSpeed { get; init; } 
        public double SpinAxis { get; init; }
        public double SpinRate { get; init; }
        public double Hla { get; init; }
        public double Vla { get; init; }
        public double BackSpin { get; set; }
        public double SideSpin { get; set; }

        //public double ClubPath { get; set; }
        //public double ImpactAngle { get; set; }
    }

    private static (bool success, string path) ExportShotDataToCsv(ObservableCollection<ShotData> shotDataCollection)
    {
        try
        {
            string folderPath = string.IsNullOrWhiteSpace(SettingsManager.Instance.Settings?.LaunchMonitor?.CustomExportPath) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export") : SettingsManager.Instance.Settings.LaunchMonitor.CustomExportPath;
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string fileName = $"ShotData_{Now:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(folderPath, fileName);

            var csvContent = new StringBuilder();
            csvContent.AppendLine("ShotNumber,Result,Club,Smash,ClubSpeed,BallSpeed,SpinAxis,SpinRate,HLA,VLA,BackSpin,SideSpin");

            foreach (var shotData in shotDataCollection)
            {
                csvContent.AppendLine($"{shotData.ShotNumber},{shotData.Result},{shotData.Club},{shotData.SmashFactor},{shotData.ClubSpeed},{shotData.BallSpeed},{shotData.SpinAxis},{shotData.SpinRate},{shotData.Hla},{shotData.Vla},{shotData.BackSpin},{shotData.SideSpin}");
            }
            File.WriteAllText(fullPath, csvContent.ToString());

            return (true, fullPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to export shot data to CSV: {ex.Message}");
            return (false, string.Empty);
        }
    }


    private async void LM_WebApiTest_Click(object sender, RoutedEventArgs e)
    {
        if (App.SharedVm != null)App.SharedVm.LmStatus = "TESTING WEBAPI";

        if (SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiUserId == 0)
        {
            if (App.SharedVm != null) App.SharedVm.LmStatus = "FIRST DEVICE CONNECTION REQUIRED";
            return;
        }

        var webApiClient = new WebApiClient();
        Logger.Log("WebApiTest_Click: UserToken: " + SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiToken);
        Logger.Log("WebApiTest_Click: UserId: " + SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiUserId);
        var response = await webApiClient.SendRequestAsync(SettingsManager.Instance?.Settings?.WebApiSettings?.WebApiUserId ?? 0);

        if (response is { Success: true } && SettingsManager.Instance?.Settings?.WebApiSettings != null)
        {
            if (response.User != null)
            {
                SettingsManager.Instance.Settings.WebApiSettings.WebApiDeviceId = response.User.Id;
                SettingsManager.Instance.Settings.WebApiSettings.WebApiToken = response.User.Token;
                SettingsManager.Instance.Settings.WebApiSettings.WebApiExpireDate = response.User.ExpireDate;
                SettingsManager.Instance.SaveSettings();
                Logger.Log($"User ID: {response.User.Id}, Token: {response.User.Token}, Expire Date: {response.User.ExpireDate}");
            }
            if (App.SharedVm != null) App.SharedVm.LmStatus = "WEBAPI SUCCESS";
        }
        else
        {
            if (App.SharedVm != null) App.SharedVm.LmStatus = "WEBAPI FAILED";
            Logger.Log("Failed to get a valid response.");
        }
    }
    private async void LaunchMonitor_Connect_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.ConnectAndSetupBluetooth();
        });
    }
    private async void LM_ARMButton_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.LmArmDevice();
        });
    }
    private async void LM_DISARMButton_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.LmDisarmDevice();
        });
    }
    private async void LaunchMonitor_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.LmDisconnect();
        });
    }
    private async void LM_ReSub_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.BtManagerReSub();
        });
    }
    private async void Putting_Connect_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.PuttingEnable();
        });
    }
    private async void Putting_Disconnect_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.PuttingDisable();
        });
    }

    private void HomeMenuSnackBarMessage(string message, int duration = 2)
    {
        DataGridSnackBar.MessageQueue?.Enqueue(
            message,
            null,
            null,
            null,
            false,
            true,
            TimeSpan.FromSeconds(duration));
    }

    private async void GSPro_Launch_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.StartGsPro();
        });
    }

    private async void ShotData_Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            (bool success, string path) = ExportShotDataToCsv(SharedViewModel.Instance.ShotDataCollection);
            if (success)
            {
                ExportIcon.Kind = PackIconKind.Check;
                HomeMenuSnackBarMessage($"Saved successfully to {path}", 2);
            }
            else
            {
                ExportIcon.Kind = PackIconKind.Close;
                HomeMenuSnackBarMessage("Failed to save data.", 2);
            }

            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            ExportIcon.Kind = PackIconKind.Close;
            HomeMenuSnackBarMessage($"Export failed: {ex.Message}", 3);
        }
        finally
        {
            ExportIcon.Kind = PackIconKind.TableArrowRight;
        }
        
    }
    private void HomeMenu_Unloaded(object sender, RoutedEventArgs e)
    {
        EventAggregator.Instance.SnackBarMessagePublished -= OnSnackBarMessagePublished;
    }

    private async void Putting_ToggleAutoClose_Click(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            (Application.Current as App)?.PuttingToggleAutoClose();
        });
    }
}
