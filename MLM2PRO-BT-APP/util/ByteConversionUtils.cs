using System.Text;
using Windows.Storage.Streams;

namespace MLM2PRO_BT_APP.util
{
    public enum WriteType
    {
        WithResponse = 2,
        WithoutResponse = 1,
        Signed = 4
    }

    public class WriteTypeProperties
    {
        public int Property { get; private set; }
        public int WriteType { get; private set; }

        private WriteTypeProperties(int writeType, int property)
        {
            WriteType = writeType;
            Property = property;
        }

        public static WriteTypeProperties? GetWriteTypeProperties(WriteType writeType)
        {
            int writeTypeValue = (int)writeType; // Convert enum to int
            switch (writeTypeValue)
            {
                case 2:
                    return new WriteTypeProperties(2, 8);
                case 1:
                    return new WriteTypeProperties(1, 4);
                case 4:
                    return new WriteTypeProperties(4, 64);
                default:
                    Logger.Log("Invalid WriteType");
                    return null;
            }
        }
    }


    public class ByteConversionUtils
    {
        private static byte[]? ShortToByteArray(short s, bool littleEndian)
        {
            return littleEndian ? new byte[] { (byte)s, (byte)(s >> 8) } : new byte[] { (byte)(s >> 8), (byte)s };
        }
        public static int[]? ArrayByteToInt(byte[]? byteArray)
        {
            if (byteArray == null)
            {
                // Handle the case where byteArray is null
                Logger.Log("ArrayByteToInt recieved null byteArray");
                return new int[0]; // Return an empty array or handle it according to your requirements
            }

            int length = byteArray.Length;
            int[]? intArray = new int[length];
            for (int i = 0; i < length; i++)
            {
                intArray[i] = byteArray[i] & 0xFF;
            }
            return intArray;
        }
        public byte[]? GetAirPressureBytes(double d)
        {
            double d2 = d * 0.0065;
            return ShortToByteArray((short)((int)((((Math.Pow(1.0 - (d2 / ((15.0 + d2) + 273.15)), 5.257) * 1013.25) * 0.1) - 50.0) * 1000.0)), true);

        }
        public byte[]? GetTemperatureBytes(double d)
        {
            return ShortToByteArray((short)((int)(d * 100.0d)), true);
        }
        public byte[]? LongToUintToByteArray(long j, bool littleEndian)
        {
            if (littleEndian)
            {
                return BitConverter.GetBytes(j);
            }
            return BitConverter.GetBytes(j);
        }
        public byte[]? IntToByteArray(int i, bool littleEndian)
        {
            if (littleEndian)
            {
                return new byte[] { (byte)i, (byte)(i >> 8), (byte)(i >> 16), (byte)(i >> 24) };
            }
            return new byte[] { (byte)(i >> 24), (byte)(i >> 16), (byte)(i >> 8), (byte)i };
        }
        public byte[]? ConvertIBufferToBytes(IBuffer buffer)
        {
            // Create a DataReader from the IBuffer
            DataReader reader = DataReader.FromBuffer(buffer);

            // Create a byte array with the same length as the buffer
            byte[]? bytes = new byte[buffer.Length];

            // Read the bytes from the buffer into the byte array
            reader.ReadBytes(bytes);

            return bytes;
        }
        public int ByteArrayToInt(byte[] byteArray, bool isLittleEndian)
        {
            if (byteArray == null)
            {
                Logger.Log("byteArray must be exactly 4 bytes long");
                return 0;
            }

            int result;
            if (isLittleEndian)
            {
                result = (byteArray[0] & 0xFF) | ((byteArray[1] & 0xFF) << 8) | ((byteArray[2] & 0xFF) << 16) | ((byteArray[3] & 0xFF) << 24);
            }
            else
            {
                Array.Reverse(byteArray); // Convert to big-endian if necessary
                result = (byteArray[0] & 0xFF) << 24 | (byteArray[1] & 0xFF) << 16 | (byteArray[2] & 0xFF) << 8 | byteArray[3] & 0xFF;
            }

            return result;
        }
        public byte[] StringToByteArray(string hex)
        {
            try
            {
                int numberChars = hex.Length;
                byte[] bytes = new byte[numberChars / 2];
                for (int i = 0; i < numberChars; i += 2)
                {
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                }
                return bytes;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Handle the exception by returning null or an empty byte array
                // return null;
                return Array.Empty<byte>();
            }
        }
        public string? ByteArrayToHexString(byte[]? bytes)
        {
            if (bytes == null)
            {
                return string.Empty; // Or handle the null case according to your needs
            }

            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                hex.AppendFormat("{0:X2}", b);
            }
            return hex.ToString();
        }
        public byte[]? HexStringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0)
            {
                Logger.Log("The hexadecimal string must have an even number of characters." + nameof(hex));
            }

            byte[]? bytes = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            }
            return bytes;
        }
        public string? IntArrayToString(int[] intArray)
        {
            return string.Join(", ", intArray);
        }
        public string? ByteArrayToString(byte[] byteArray)
        {
            return string.Join(", ", byteArray);
        }
    }
}
