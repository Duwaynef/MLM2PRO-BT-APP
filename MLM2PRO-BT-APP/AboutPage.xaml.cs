using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MLM2PRO_BT_APP
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class AboutPage : Page
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