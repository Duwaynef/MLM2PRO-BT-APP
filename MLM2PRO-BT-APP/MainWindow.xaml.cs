using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Newtonsoft.Json;
using System.Diagnostics;
using MLM2PRO_BT_APP.util;
using Windows.ApplicationModel.VoiceCommands;

namespace MLM2PRO_BT_APP
{
    public partial class MainWindow : Window
    {
        public static Popup DebugConsolePopup { get; private set; }
        public static TextBox DebugConsoleTextBox { get; private set; }
        ByteConversionUtils byteConversionUtils = new ByteConversionUtils();

        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            // Create the debug console TextBox
            DebugConsoleTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = false,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.DarkSlateGray),
                Foreground = new SolidColorBrush(Colors.White)
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
            Logger.Log("CurrentKey: " + byteConversionUtils.ByteArrayToHexString((App.Current as App)?.GetBTKey()));
            MainContentFrame.Navigate(new HomeMenu());
        }

        public class CustomTextWriter : TextWriter
        {
            private Action<string> _logAction;

            public CustomTextWriter(Action<string> logAction)
            {
                _logAction = logAction;
            }

            public override void Write(char value)
            {
                Write(value.ToString());
            }

            public override void Write(string value)
            {
                _logAction.Invoke(value);
            }

            public override void WriteLine(string value)
            {
                Write(value + Environment.NewLine);
            }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }
        }

        private void Button_Toggle_DebugConsole(object sender, RoutedEventArgs e)
        {
            ToggleDebugConsole();
        }

        public static void ToggleDebugConsole()
        {
            DebugConsolePopup.IsOpen = !DebugConsolePopup.IsOpen;
        }

        private void Quit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void HomeMenu_Click(object sender, RoutedEventArgs e)
        {
            MainContentFrame.Navigate(new HomeMenu());
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
            // Launching a website
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://ko-fi.com/duwayne",
                UseShellExecute = true
            });
        }
        // Define the event handler
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Check if the debug console popup is open
            if (DebugConsolePopup.IsOpen)
            {
                // Close the debug console popup
                DebugConsolePopup.IsOpen = false;
            }
        }

    }
}
