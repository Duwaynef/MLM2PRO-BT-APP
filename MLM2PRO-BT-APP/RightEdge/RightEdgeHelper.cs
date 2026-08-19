using MLM2PRO_BT_APP.util;
using RightEdge.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Windows.System;

namespace MLM2PRO_BT_APP.RightEdge
{
    public static class RightEdgeConstants
    {
        public static string WEBSITE_URL_RIGHTEDGEPUTTING_HOME = "http://www.rightedgeputting.com";
    }

    public enum Handedness
    {
        RIGHT,
        LEFT
    }

    public static class RightEdgeHelper
    {
        private static Border? _puttTrackerNotInstalledNoticeBorderControl = null;
        public static bool IsPuttTrackerInstalled
        {
            get
            {
                bool retVal = false;

                string reAppSettingsPathFile = REPaths.AppSettingsFile;
                if( File.Exists(reAppSettingsPathFile) )
                {
                    if( !RESettings.CurrentAppSettings.defaultDeviceName.Trim().Equals(String.Empty) )
                        retVal = true;
                }

                return( retVal );
            }
        }

        public static Border PuttTrackerNotInstalledNoticeBorderControl( MouseButtonEventHandler? url_click_event = null)
        {
            double fontSize = 12;

            if (_puttTrackerNotInstalledNoticeBorderControl != null)
                return (_puttTrackerNotInstalledNoticeBorderControl);

            _puttTrackerNotInstalledNoticeBorderControl = new Border();
            _puttTrackerNotInstalledNoticeBorderControl.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;

            TextBlock noticeTb1 = new TextBlock();
            TextBlock noticeTb2 = new TextBlock();
            TextBlock sitelinkTb = new TextBlock();

            noticeTb1.Text = "Right Edge Putt Tracker not installed on this computer.";
            noticeTb1.FontSize = fontSize;
            noticeTb1.TextWrapping = TextWrapping.Wrap;

            noticeTb2.Text = "Learn about the Right Edge Putt Tracker:";
            noticeTb2.FontSize = fontSize;

            sitelinkTb.Text = "www.rightedgeputting.com";
            sitelinkTb.FontSize = fontSize;
            sitelinkTb.Margin = new Thickness(5, 0, 0, 0);
            if (url_click_event != null)
                sitelinkTb.MouseDown += url_click_event;

            sitelinkTb = applyHyperlinkStyling(sitelinkTb);

            StackPanel learnLineSP = new StackPanel();
            learnLineSP.Orientation = Orientation.Horizontal;
            learnLineSP.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            learnLineSP.Children.Add(noticeTb2);
            learnLineSP.Children.Add(sitelinkTb);

            StackPanel outerSP = new StackPanel();
            outerSP.Orientation = Orientation.Vertical;
            outerSP.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            outerSP.Children.Add(noticeTb1);
            outerSP.Children.Add(learnLineSP);

            _puttTrackerNotInstalledNoticeBorderControl.Child = outerSP;

            return (_puttTrackerNotInstalledNoticeBorderControl);
        }

        public static TextBlock applyHyperlinkStyling(TextBlock inputTb )
        {
            SolidColorBrush linkColorBrush = System.Windows.Media.Brushes.Blue;
            SolidColorBrush linkHoverColorBrush = System.Windows.Media.Brushes.DarkBlue;

            if (SettingsManager.Instance.Settings?.ApplicationSettings != null)
            {
                if(SettingsManager.Instance.Settings.ApplicationSettings.DarkTheme)
                {
                    linkColorBrush = System.Windows.Media.Brushes.LightBlue;
                    linkHoverColorBrush = System.Windows.Media.Brushes.DodgerBlue;
                }
            }

            inputTb.Foreground = linkColorBrush;
            inputTb.TextDecorations = System.Windows.TextDecorations.Underline;
            inputTb.Cursor = System.Windows.Input.Cursors.Hand;

            // Optional: better UX (hover effect)
            inputTb.MouseEnter += (s, e) =>
            {
                inputTb.Foreground = linkHoverColorBrush;
            };

            inputTb.MouseLeave += (s, e) =>
            {
                inputTb.Foreground = linkColorBrush;
            };

            return( inputTb );
        }

        public static async void LaunchRightEdgePuttingHomePage()
        {
            await Launcher.LaunchUriAsync(new Uri(RightEdgeConstants.WEBSITE_URL_RIGHTEDGEPUTTING_HOME));
        }
    }
}
