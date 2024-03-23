using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Diagnostics;
using MLM2PRO_BT_APP.util;
using MaterialDesignThemes.Wpf;
using System.Windows.Threading;
using System.Windows.Input;
using static MLM2PRO_BT_APP.util.GitHubReleaseChecker;

namespace MLM2PRO_BT_APP
{
    public partial class MainWindow
    {
        private static Popup? DebugConsolePopup { get; set; }
        private static TextBox? DebugConsoleTextBox { get; set; }
        private readonly PaletteHelper _paletteHelper = new();
        private readonly Theme _theme;
        private readonly DispatcherTimer _pressHoldTimer;
        private bool _isPressAndHold;
        private string _updateUrl = "";

        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            _theme = _paletteHelper.GetTheme();
            // Create the debug console TextBox
            DebugConsoleTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = false,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(_theme.Background),
                Foreground = new SolidColorBrush(_theme.Foreground)
            };

            // Create the Popup to host the debug console
            DebugConsolePopup = new Popup
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 600,
                Height = 500,
                Child = DebugConsoleTextBox,
                IsOpen = false
            };

            Logger.Initialize(DebugConsoleTextBox);
            Logger.Log("Application Started");
            Logger.Log("CurrentKey: " + ByteConversionUtils.ByteArrayToHexString((Application.Current as App)?.GetBtKey()));
            MainContentFrame.Navigate(new HomeMenu());
            SetAppTheme();

            _pressHoldTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _pressHoldTimer.Tick += PressHoldTimer_Tick!;
            CheckForGitHubUpdates();
        }

        private void Button_Toggle_DebugConsole(object sender, RoutedEventArgs e)
        {
            if (!_isPressAndHold)
            {
                ToggleDebugConsole();
            }
            _isPressAndHold = false;

        }
        private async void CheckForGitHubUpdates()
        {
            try
            {
                var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString();
                Logger.Log($"Current version: {currentVersion}");
                var releaseChecker = new GitHubReleaseChecker("DuwayneF", "MLM2PRO-BT-APP");
                if (currentVersion != null)
                {
                    GitHubRelease? currentRelease = await releaseChecker.CheckForUpdateAsync(currentVersion);
                    if(currentRelease != null)
                    {
                        _updateUrl = currentRelease.HtmlUrl ?? "";
                        UpdateAvailableBadge.Visibility = Visibility.Visible;
                        UpdateAvailableSeperator.Visibility = Visibility.Visible;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            EventAggregator.Instance.PublishSnackBarMessage("Application update available, click button on menu bar", 5);
                        });
                    } 
                    else
                    {
                        Logger.Log("No updates found.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error checking for updates: {ex.Message}");
            }
        }
        private static void ToggleDebugConsole()
        {
            if (DebugConsolePopup != null) DebugConsolePopup.IsOpen = !DebugConsolePopup.IsOpen;
        }
        private static void OpenAdvancedDebugWindow()
        {
            var advancedDebug = new AdvancedDebug
            {
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            advancedDebug.Show();
        }

        private void PressHoldTimer_Tick(object sender, EventArgs e)
        {
            _isPressAndHold = true;
            _pressHoldTimer.Stop();
            OpenAdvancedDebugWindow();
        }

        private void Button_DebugConsole_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _pressHoldTimer.Start();
        }

        private void Button_DebugConsole_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _pressHoldTimer.Stop();
        }
        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Navigate(new SettingsPage());
        }
        private void About_Click(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Navigate(new AboutPage());
        }
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void Home_Click(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Navigate(new HomeMenu());
        }
        private void Coffee_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/D1D8VL7RV",
                UseShellExecute = true
            });
        }
        private void Button_UpdatesAvailable_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _updateUrl,
                UseShellExecute = true
            });
        }
        private static void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DebugConsolePopup is { IsOpen: true })
            {
                DebugConsolePopup.IsOpen = false;
            }
        }
        private void SetAppTheme()
        {
            if (SettingsManager.Instance.Settings?.ApplicationSettings?.DarkTheme ?? true)
            {
                _theme.SetBaseTheme(BaseTheme.Dark);
            }
            else
            {
                _theme.SetBaseTheme(BaseTheme.Light);
            }
            _paletteHelper.SetTheme(_theme);
        }

        private void ChangeAppTheme()
        {
            _theme.SetBaseTheme(!SettingsManager.Instance.Settings?.ApplicationSettings!.DarkTheme ?? true ? BaseTheme.Dark : BaseTheme.Light);
            _paletteHelper.SetTheme(_theme);
            if (SettingsManager.Instance.Settings?.ApplicationSettings != null)
                SettingsManager.Instance.Settings.ApplicationSettings.DarkTheme = !SettingsManager.Instance.Settings.ApplicationSettings.DarkTheme;
            SettingsManager.Instance.SaveSettings();
        }

        private void Button_Toggle_ToggleDarkMode(object sender, RoutedEventArgs e)
        {
            ChangeAppTheme();
        }

        private void ReportBug_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Duwaynef/MLM2PRO-BT-APP/issues",
                UseShellExecute = true
            });
        }
    }
}
