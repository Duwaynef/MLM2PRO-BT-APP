using System.Diagnostics;
using System.Windows;

namespace MLM2PRO_BT_APP
{
    public partial class AboutPage
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        private void GitHub_Link_Click(object sender, RoutedEventArgs e)
        {
            // Launching a website
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/Duwaynef/MLM2PRO-BT-APP",
                UseShellExecute = true
            });
        }
    }
}