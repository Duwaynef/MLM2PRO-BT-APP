using System.Security.Cryptography;

namespace MLM2PRO_BT_APP.util
{
    public class Encryption
    {
        private byte[] IvParameter = { 109, 46, 82, 19, 33, 50, 4, 69, 111, 44, 121, 72, 16, 101, 109, 66 };
        private byte[]? encryptionKey;

        public byte[] GetEncryptionTypeBytes()
        {
            return new byte[] { 0, 1 };
        }

        public Encryption()
        {
            byte[] predeterminedKey = new byte[32] { 26, 24, 1, 38, 249, 154, 60, 63, 149, 185, 205, 150, 126, 160, 38, 61, 89, 199, 68, 140, 255, 21, 250, 131, 55, 165, 121, 250, 49, 121, 233, 21 };
            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Key = predeterminedKey;
                encryptionKey = aes.Key;
            }
        }

        public byte[]? GetKeyBytes()
        {
            return encryptionKey;
        }

        public byte[]? Encrypt(byte[]? input)
        {
            if (input == null)
            {
                // Handle the case where input is null
                Logger.Log("Encrypt receieved null input");
                return Array.Empty<byte>(); // Return an empty byte array or handle it according to your requirements
            }

            using (Aes aes = Aes.Create())
            {
                aes.Key = encryptionKey ?? aes.Key;
                aes.IV = IvParameter;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    return encryptor.TransformFinalBlock(input, 0, input.Length);
                }
            }
        }


        public byte[]? Decrypt(byte[]? input)
        {
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = encryptionKey ?? aes.Key;
                    aes.IV = IvParameter;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    {
                        return decryptor.TransformFinalBlock(input ?? new byte[0], 0, input.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error decrypting data: {ex.Message}");
                return null;
            }
        }

        public byte[] DecryptKnownKey(byte[] input, byte[] encryptionKeyinput)
        {
            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = encryptionKeyinput;
                    aes.IV = IvParameter;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    {
                        return decryptor.TransformFinalBlock(input, 0, input.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error decrypting data: {ex.Message}");
                return new byte[0];
            }
        }

    }
}
