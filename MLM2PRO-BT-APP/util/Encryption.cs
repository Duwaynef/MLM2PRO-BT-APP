using System.Security.Cryptography;

namespace MLM2PRO_BT_APP.util
{
    public class Encryption
    {
        private readonly byte[] _ivParameter = [109, 46, 82, 19, 33, 50, 4, 69, 111, 44, 121, 72, 16, 101, 109, 66];
        private readonly byte[]? _encryptionKey;

        public static byte[] GetEncryptionTypeBytes()
        {
            return [0, 1];
        }

        public Encryption()
        {
            byte[] predeterminedKey = new byte[32] { 26, 24, 1, 38, 249, 154, 60, 63, 149, 185, 205, 150, 126, 160, 38, 61, 89, 199, 68, 140, 255, 21, 250, 131, 55, 165, 121, 250, 49, 121, 233, 21 };
            using var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = predeterminedKey;
            _encryptionKey = aes.Key;
        }

        public byte[]? GetKeyBytes()
        {
            return _encryptionKey;
        }

        public byte[] Encrypt(byte[]? input)
        {
            if (input == null)
            {
                // Handle the case where input is null
                Logger.Log("Encrypt received null input");
                return Array.Empty<byte>(); // Return an empty byte array or handle it according to your requirements
            }

            using var aes = Aes.Create();
            aes.Key = _encryptionKey ?? aes.Key;
            aes.IV = _ivParameter;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            return encryptor.TransformFinalBlock(input, 0, input.Length);
        }


        public byte[]? Decrypt(byte[]? input)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = _encryptionKey ?? aes.Key;
                aes.IV = _ivParameter;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decrypted = aes.CreateDecryptor(aes.Key, aes.IV);
                return input is { Length: > 0 } ? decrypted.TransformFinalBlock(input, 0, input.Length) : null;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error decrypting data: {ex.Message}");
                return null;
            }
        }

        public byte[] DecryptKnownKey(byte[] input, byte[] encryptionKeyInput)
        {
            try
            {
                using var aes = Aes.Create();
                aes.Key = encryptionKeyInput;
                aes.IV = _ivParameter;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decrypted = aes.CreateDecryptor(aes.Key, aes.IV);
                return decrypted.TransformFinalBlock(input, 0, input.Length);
            }
            catch (Exception ex)
            {
                Logger.Log($"Error decrypting data: {ex.Message}");
                return Array.Empty<byte>();
            }
        }
    }
}
