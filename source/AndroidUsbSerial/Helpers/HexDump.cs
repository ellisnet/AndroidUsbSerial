using System;
using System.Text;

namespace AndroidUsbSerial.Helpers
{
    public static class HexDump
    {
        // ReSharper disable once InconsistentNaming
        private static readonly char[] HexDigits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

        private static string GetUtf8String(byte[] bytes, int index, int count) => Encoding.UTF8.GetString(bytes, index, count);

        public static string DumpHexString(byte[] array) => DumpHexString(array, 0, array.Length);

        public static string DumpHexString(byte[] array, int offset, int length)
        {
            var result = new StringBuilder();

            var line = new byte[16];
            int lineIndex = 0;

            result.Append("\n0x");
            result.Append(ToHexString(offset));

            for (int i = offset; i < offset + length; i++)
            {
                if (lineIndex == 16)
                {
                    result.Append(" ");

                    for (int j = 0; j < 16; j++)
                    {
                        if (line[j] > (byte)' ' && line[j] < (byte)'~')
                        {
                            result.Append(GetUtf8String(line, j, 1));
                        }
                        else
                        {
                            result.Append(".");
                        }
                    }

                    result.Append("\n0x");
                    result.Append(ToHexString(i));
                    lineIndex = 0;
                }

                byte b = array[i];
                result.Append(" ");
                result.Append(HexDigits[((int)((uint)b >> 4)) & 0x0F]);
                result.Append(HexDigits[b & 0x0F]);

                line[lineIndex++] = b;
            }

            if (lineIndex != 16)
            {
                int count = (16 - lineIndex) * 3;
                count++;
                for (int i = 0; i < count; i++)
                {
                    result.Append(" ");
                }

                for (int i = 0; i < lineIndex; i++)
                {
                    if (line[i] > (byte)' ' && line[i] < (byte)'~')
                    {
                        result.Append(GetUtf8String(line, i, 1));
                    }
                    else
                    {
                        result.Append(".");
                    }
                }
            }

            return result.ToString();
        }

        public static string ToHexString(byte b) => ToHexString(ToByteArray(b));

        public static string ToHexString(byte[] array) => ToHexString(array, 0, array.Length);

        public static string ToHexString(byte[] array, int offset, int length)
        {
            var buf = new char[length * 2];

            int bufIndex = 0;
            for (int i = offset; i < offset + length; i++)
            {
                byte b = array[i];
                buf[bufIndex++] = HexDigits[((int)((uint)b >> 4)) & 0x0F];
                buf[bufIndex++] = HexDigits[b & 0x0F];
            }

            return new string(buf);
        }

        public static string ToHexString(int i) => ToHexString(ToByteArray(i));

        public static string ToHexString(short i) => ToHexString(ToByteArray(i));

        public static byte[] ToByteArray(byte b)
        {
            var array = new byte[1];
            array[0] = b;
            return array;
        }

        public static byte[] ToByteArray(int i)
        {
            var array = new byte[4];

            array[3] = unchecked((byte)(i & 0xFF));
            array[2] = unchecked((byte)((i >> 8) & 0xFF));
            array[1] = unchecked((byte)((i >> 16) & 0xFF));
            array[0] = unchecked((byte)((i >> 24) & 0xFF));

            return array;
        }

        public static byte[] ToByteArray(short i)
        {
            var array = new byte[2];

            array[1] = unchecked((byte)(i & 0xFF));
            array[0] = unchecked((byte)((i >> 8) & 0xFF));

            return array;
        }

        private static int ToByte(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return (c - '0');
            }
            if (c >= 'A' && c <= 'F')
            {
                return (c - 'A' + 10);
            }
            if (c >= 'a' && c <= 'f')
            {
                return (c - 'a' + 10);
            }

            throw new Exception("Invalid hex char '" + c + "'");
        }

        public static byte[] HexStringToByteArray(string hexString)
        {
            int length = hexString.Length;
            var buffer = new byte[length / 2];

            for (int i = 0; i < length; i += 2)
            {
                buffer[i / 2] = (byte)((ToByte(hexString[i]) << 4) | ToByte(hexString[i + 1]));
            }

            return buffer;
        }
    }
}
