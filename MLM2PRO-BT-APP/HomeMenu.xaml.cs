using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using MLM2PRO_BT_APP.connections;
using MLM2PRO_BT_APP.util;

namespace MLM2PRO_BT_APP;

public partial class HomeMenu : Page
{
    public HomeMenu()
    {
        InitializeComponent();
        this.DataContext = App.SharedVm;
        // ShotDataDataGrid.ItemsSource = App.SharedVM.ShotDataCollection;
        ShotDataDataGrid.ItemsSource = SharedViewModel.Instance?.ShotDataCollection;
        DataGridSnackBar.MessageQueue = new SnackbarMessageQueue(TimeSpan.FromMilliseconds(2000));
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
        public int ShotNumber { get; set; }
        public string Result { get; set; } = "";
        public double SmashFactor { get; set; } = 0;
        public string Club { get; set; } = "";
        public double ClubSpeed { get; set; }
        public double BallSpeed { get; set; } 
        public double SpinAxis { get; set; }
        public double SpinRate { get; set; }
        public double Hla { get; set; }
        public double Vla { get; set; }
        public double BackSpin { get; set; }
        public double SideSpin { get; set; }

        //public double ClubPath { get; set; }
        //public double ImpactAngle { get; set; }
    }

    private (bool success, string path) ExportShotDataToCsv(ObservableCollection<ShotData> shotDataCollection)
    {
        try
        {
            string folderPath = "";
            if (String.IsNullOrWhiteSpace(SettingsManager.Instance?.Settings?.LaunchMonitor?.CustomExportPath))
            {
                folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Export");
            }
            else
            {
                folderPath = SettingsManager.Instance.Settings.LaunchMonitor.CustomExportPath;
            }
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string fileName = $"ShotData_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string fullPath = Path.Combine(folderPath, fileName);

            StringBuilder csvContent = new StringBuilder();
            csvContent.AppendLine("ShotNumber,Result,Club,ClubSpeed,BallSpeed,SpinAxis,SpinRate,HLA,VLA");

            foreach (var shotData in shotDataCollection)
            {
                csvContent.AppendLine($"{shotData.ShotNumber},{shotData.Result},{shotData.Club},{shotData.ClubSpeed},{shotData.BallSpeed},{shotData.SpinAxis},{shotData.SpinRate},{shotData.Hla},{shotData.Vla}");
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
            (Application.Current as App)?.BtManagerResub();
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
            if (SharedViewModel.Instance != null && SharedViewModel.Instance?.ShotDataCollection != null)
            {
                (bool success, string path) = ExportShotDataToCsv(SharedViewModel.Instance?.ShotDataCollection!);
                if (success)
                {
                    ExportIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Check;
                    DataGridSnackBar.MessageQueue?.Enqueue(
                        $"Saved successfully to {path}",
                        null,
                        null,
                        null,
                        false,
                        true,
                        TimeSpan.FromSeconds(2));
                }
                else
                {
                    ExportIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Close;
                    DataGridSnackBar.MessageQueue?.Enqueue(
                        "Failed to save data.",
                        null,
                        null,
                        null,
                        false,
                        true,
                        TimeSpan.FromSeconds(2));
                }
            }

            await Task.Delay(2000);
        }
        catch (Exception ex)
        {
            ExportIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.Close;
            DataGridSnackBar.MessageQueue?.Enqueue(
                $"Export failed: {ex.Message}",
                null,
                null,
                null,
                false,
                true,
                TimeSpan.FromSeconds(3));
        }
        finally
        {
            ExportIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.TableArrowRight;
        }
    }


}
