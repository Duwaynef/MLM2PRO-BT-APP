using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Diagnostics;
using MLM2PRO_BT_APP.util;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace MLM2PRO_BT_APP
{
    public partial class MainWindow
    {
        private static Popup? DebugConsolePopup { get; set; }
        private static TextBox? DebugConsoleTextBox { get; set; }
        private readonly ByteConversionUtils _byteConversionUtils = new();
        private PaletteHelper paletteHelper = new PaletteHelper();
        private Theme _theme;

        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            _theme = paletteHelper.GetTheme();
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
            Logger.Log("CurrentKey: " + _byteConversionUtils.ByteArrayToHexString((Application.Current as App)?.GetBtKey()));
            MainContentFrame.Navigate(new HomeMenu());
            setAppTheme();
        }

        public class CustomTextWriter(Action<string> logAction) : TextWriter
        {
            public override void Write(char value)
            {
                Write(value.ToString());
            }

            public override void Write(string? value)
            {
                if (value != null) logAction.Invoke(value);
            }

            public override void WriteLine(string? value)
            {
                Write(value + Environment.NewLine);
            }

            public override Encoding Encoding => Encoding.UTF8;
        }

        private void Button_Toggle_DebugConsole(object sender, RoutedEventArgs e)
        {
            ToggleDebugConsole();
        }

        private static void ToggleDebugConsole()
        {
            if (DebugConsolePopup != null) DebugConsolePopup.IsOpen = !DebugConsolePopup.IsOpen;
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
                FileName = "https://ko-fi.com/duwayne",
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

        public void setAppTheme()
        {
            if (SettingsManager.Instance.Settings.ApplicationSettings.DarkTheme)
            {
                _theme.SetBaseTheme(BaseTheme.Dark);
            }
            else
            {
                _theme.SetBaseTheme(BaseTheme.Light);
            }
            paletteHelper.SetTheme(_theme);
        }

        public void changeAppTheme()
        {
            _theme.SetBaseTheme(!SettingsManager.Instance.Settings.ApplicationSettings.DarkTheme ? BaseTheme.Dark : BaseTheme.Light);
            paletteHelper.SetTheme(_theme);
            SettingsManager.Instance.Settings.ApplicationSettings.DarkTheme = !SettingsManager.Instance.Settings.ApplicationSettings.DarkTheme;
            SettingsManager.Instance.SaveSettings();
        }

        private void Button_Toggle_ToggleDarkMode(object sender, RoutedEventArgs e)
        {
            changeAppTheme();
        }
    }
}
