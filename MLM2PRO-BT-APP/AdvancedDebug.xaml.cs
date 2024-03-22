using MLM2PRO_BT_APP.util;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace MLM2PRO_BT_APP
{
    /// <summary>
    /// Interaction logic for AdvancedDebug.xaml
    /// </summary>
    public partial class AdvancedDebug : Window
    {

        ByteConversionUtils byteConversionUtils = new ByteConversionUtils();
        Encryption _btEncryption = new Encryption();
        public AdvancedDebug()
        {
            InitializeComponent();
        }

        private void AdvancedDebug_Decrypt_Button_Click(object sender, RoutedEventArgs e)
        {
            var lines = AdvancedDebugInput.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            String keyTextBoxInput = AdvancedDebugKey.Text;

            foreach (var line in lines)
            {
                try
                {
                    byte[] byteArray = byteConversionUtils.StringToByteArray(line);
                    byte[] byteArray2 = byteConversionUtils.StringToByteArray(keyTextBoxInput);
                    byte[] outputByteArr = _btEncryption.DecryptKnownKey(byteArray, byteArray2);
                    Logger.Log("Decrypted Bytes: " + byteConversionUtils.ByteArrayToHexString(outputByteArr));
                    AdvancedDebugOutput.Text += byteConversionUtils.ByteArrayToHexString(outputByteArr);
                    AdvancedDebugOutput.Text += "\n";
                }
                catch (Exception ex) when (ex.Message == "Error decrypting data: Padding is invalid and cannot be removed.")
                {
                    Logger.Log("Stopped processing due to padding error.");
                }
                catch (Exception ex)
                {
                    Logger.Log($"An error occurred: {ex.Message}");
                }
            }
        }
    }
}
