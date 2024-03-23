using MLM2PRO_BT_APP.util;
using System.Windows;

namespace MLM2PRO_BT_APP
{
    /// <summary>
    /// Interaction logic for AdvancedDebug.xaml
    /// </summary>
    public partial class AdvancedDebug
    {
        private readonly ByteConversionUtils _byteConversionUtils = new();
        private readonly Encryption _btEncryption = new();
        public AdvancedDebug()
        {
            InitializeComponent();
        }

        private void AdvancedDebug_Decrypt_Button_Click(object sender, RoutedEventArgs e)
        {
            string keyTextBoxInput = AdvancedDebugKey.Text;
            if (AdvancedDebugGetKeyCheckbox.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(keyTextBoxInput)) return;
                byte[]? outputByteArr = (Application.Current as App)?.GetEncryptedKeyFromHex(_byteConversionUtils.StringToByteArray(keyTextBoxInput));
                AdvancedDebugOutput.Text += "Key: " + ByteConversionUtils.ByteArrayToHexString(outputByteArr);
                AdvancedDebugOutput.Text += "\n";
            }
            else
            {
                string[] lines = AdvancedDebugInput.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    try
                    {
                        byte[] byteArray = _byteConversionUtils.StringToByteArray(line);
                        byte[] byteArray2 = _byteConversionUtils.StringToByteArray(keyTextBoxInput);
                        byte[] outputByteArr = _btEncryption.DecryptKnownKey(byteArray, byteArray2);
                        Logger.Log("Decrypted Bytes: " + ByteConversionUtils.ByteArrayToHexString(outputByteArr));
                        AdvancedDebugOutput.Text += ByteConversionUtils.ByteArrayToHexString(outputByteArr);
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
}
