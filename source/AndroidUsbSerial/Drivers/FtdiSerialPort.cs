using System;
using Android.Hardware.Usb;
using Android.Util;
using AndroidUsbSerial.Internal;
using AndroidUsbSerial.Constants;
using AndroidUsbSerial.Helpers;
using Java.Nio;

namespace AndroidUsbSerial.Drivers
{
    internal enum FtdiDeviceType
    {
        // ReSharper disable InconsistentNaming
        TYPE_BM,
        TYPE_AM,
        TYPE_2232C,
        TYPE_R,
        TYPE_2232H,
        TYPE_4232H
        // ReSharper restore InconsistentNaming
    }

    internal class FtdiSerialPort : CommonUsbSerialPort
    {
        private readonly FtdiSerialDriver _driver;

        // ReSharper disable InconsistentNaming
        public static readonly int USB_TYPE_STANDARD = 0x00 << 5;
        public static readonly int USB_TYPE_CLASS = 0x00 << 5;
        public static readonly int USB_TYPE_VENDOR = 0x00 << 5;
        public static readonly int USB_TYPE_RESERVED = 0x00 << 5;

        public static readonly int USB_RECIP_DEVICE = 0x00;
        public static readonly int USB_RECIP_INTERFACE = 0x01;
        public static readonly int USB_RECIP_ENDPOINT = 0x02;
        public static readonly int USB_RECIP_OTHER = 0x03;

        public static readonly int USB_ENDPOINT_IN = 0x80;
        public static readonly int USB_ENDPOINT_OUT = 0x00;

        public static readonly int USB_WRITE_TIMEOUT_MILLIS = 5000;
        public static readonly int USB_READ_TIMEOUT_MILLIS = 5000;

        internal static readonly int SIO_RESET_REQUEST = 0;
        internal static readonly int SIO_MODEM_CTRL_REQUEST = 1;
        internal static readonly int SIO_SET_FLOW_CTRL_REQUEST = 2;
        internal static readonly int SIO_SET_BAUD_RATE_REQUEST = 3;
        internal static readonly int SIO_SET_DATA_REQUEST = 4;

        internal static readonly int SIO_RESET_SIO = 0;
        internal static readonly int SIO_RESET_PURGE_RX = 1;
        internal static readonly int SIO_RESET_PURGE_TX = 2;

        public static readonly int FTDI_DEVICE_OUT_REQTYPE = ExtendedUsbConstants.USB_TYPE_VENDOR | USB_RECIP_DEVICE | USB_ENDPOINT_OUT;
        public static readonly int FTDI_DEVICE_IN_REQTYPE = ExtendedUsbConstants.USB_TYPE_VENDOR | USB_RECIP_DEVICE | USB_ENDPOINT_IN;

        internal static readonly int MODEM_STATUS_HEADER_LENGTH = 2;
        internal static readonly bool ENABLE_ASYNC_READS = false;
        // ReSharper restore InconsistentNaming

        private FtdiDeviceType _deviceType;
        //private int _interface = 0;
        //private int _maxPacketSize = 64;

        public FtdiSerialPort(FtdiSerialDriver driver, UsbDevice device, int portNumber) : base(device, portNumber)
        {
            _driver = driver;
        }

        public override IUsbSerialDriver Driver => _driver;

        internal int FilterStatusBytes(byte[] src, byte[] dest, int totalBytesRead, int maxPacketSize)
        {
            int packetsCount = totalBytesRead / maxPacketSize + (totalBytesRead % maxPacketSize == 0 ? 0 : 1);
            for (int packetIdx = 0; packetIdx < packetsCount; ++packetIdx)
            {
                int count = (packetIdx == (packetsCount - 1)) ? (totalBytesRead % maxPacketSize) - MODEM_STATUS_HEADER_LENGTH : maxPacketSize - MODEM_STATUS_HEADER_LENGTH;
                if (count > 0)
                {
                    Array.Copy(src, packetIdx * maxPacketSize + MODEM_STATUS_HEADER_LENGTH, dest, packetIdx * (maxPacketSize - MODEM_STATUS_HEADER_LENGTH), count);
                }
            }

            return totalBytesRead - (packetsCount * 2);
        }

        public virtual void Reset()
        {
            int result = _connection.ControlTransfer(UsbAddressingExtensions.GetAddressing(FTDI_DEVICE_OUT_REQTYPE), 
                SIO_RESET_REQUEST, SIO_RESET_SIO, 0, null, 0, USB_WRITE_TIMEOUT_MILLIS);
            if (result != 0)
            {
                throw new UsbSerialException("Reset failed: result=" + result);
            }

            _deviceType = FtdiDeviceType.TYPE_R;
        }

        public override void Open(UsbDeviceConnection connection)
        {
            if (_connection != null)
            {
                throw new UsbSerialException("Already open");
            }
            _connection = connection;

            bool opened = false;
            try
            {
                for (int i = 0; i < _driver.Device.InterfaceCount; i++)
                {
                    if (connection.ClaimInterface(_driver.Device.GetInterface(i), true))
                    {
                        Log.Debug(nameof(FtdiSerialDriver), "claimInterface " + i + " SUCCESS");
                    }
                    else
                    {
                        throw new UsbSerialException("Error claiming interface " + i);
                    }
                }
                Reset();
                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    Close();
                    _connection = null;
                }
            }
        }

        public override void Close()
        {
            if (_connection == null)
            {
                throw new UsbSerialException("Already closed");
            }
            try
            {
                _connection.Close();
            }
            finally
            {
                _connection = null;
            }
        }

        public override int Read(byte[] dest, int timeoutMilliseconds)
        {
            UsbEndpoint endpoint = _driver.Device.GetInterface(0).GetEndpoint(0);

            if (ENABLE_ASYNC_READS)
            {
                int readAmt;
                lock (_readBufferLock)
                {
                    readAmt = Math.Min(dest.Length, _readBuffer.Length);
                }

                var request = new UsbRequest();
                request.Initialize(_connection, endpoint);

                var buf = ByteBuffer.Wrap(dest);
                if (!request.Queue(buf, readAmt)) //TODO: Must fix this
                {
                    throw new UsbSerialException("Error queueing request.");
                }

                UsbRequest response = _connection.RequestWait();
                if (response == null)
                {
                    throw new UsbSerialException("Null response");
                }

                int payloadBytesRead = buf.Position() - MODEM_STATUS_HEADER_LENGTH;
                if (payloadBytesRead > 0)
                {
                    Log.Debug(nameof(FtdiSerialDriver), HexDump.DumpHexString(dest, 0, Math.Min(32, dest.Length)));
                    return payloadBytesRead;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                lock (_readBufferLock)
                {
                    int readAmt = Math.Min(dest.Length, _readBuffer.Length);
                    int totalBytesRead = _connection.BulkTransfer(endpoint, _readBuffer, readAmt, timeoutMilliseconds);

                    if (totalBytesRead < MODEM_STATUS_HEADER_LENGTH)
                    {
                        throw new UsbSerialException($"Expected at least {MODEM_STATUS_HEADER_LENGTH} bytes");
                    }

                    return FilterStatusBytes(_readBuffer, dest, totalBytesRead, endpoint.MaxPacketSize);
                }
            }
        }

        public override int Write(byte[] src, int timeoutMilliseconds)
        {
            UsbEndpoint endpoint = _driver.Device.GetInterface(0).GetEndpoint(1);
            int offset = 0;

            while (offset < src.Length)
            {
                int writeLength;
                int amtWritten;

                lock (_writeBufferLock)
                {
                    byte[] writeBuffer;

                    writeLength = Math.Min(src.Length - offset, _writeBuffer.Length);
                    if (offset == 0)
                    {
                        writeBuffer = src;
                    }
                    else
                    {
                        Array.Copy(src, offset, _writeBuffer, 0, writeLength);
                        writeBuffer = _writeBuffer;
                    }

                    amtWritten = _connection.BulkTransfer(endpoint, writeBuffer, writeLength, timeoutMilliseconds);
                }

                if (amtWritten <= 0)
                {
                    throw new UsbSerialException(
                        $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                }

                Log.Debug(nameof(FtdiSerialDriver), $"Wrote amtWritten={amtWritten} attempted={writeLength}");
                offset += amtWritten;
            }
            return offset;
        }

        internal virtual int SetBaudRate(int baudRate)
        {
            long[] vals = ConvertBaudrate(baudRate);
            long actualBaudrate = vals[0];
            long index = vals[1];
            long value = vals[2];
            int result = _connection.ControlTransfer(UsbAddressingExtensions.GetAddressing(FTDI_DEVICE_OUT_REQTYPE), 
                SIO_SET_BAUD_RATE_REQUEST, (int)value, (int)index, null, 0, USB_WRITE_TIMEOUT_MILLIS);
            if (result != 0)
            {
                throw new UsbSerialException($"Setting baudrate failed: result={result}");
            }
            return (int)actualBaudrate;
        }

        public override void SetParameters(int baudRate, DataBits dataBits, StopBits stopBits, Parity parity)
        {
            SetBaudRate(baudRate);

            int config = (int)dataBits;

            switch (parity)
            {
                case Parity.None:
                    config |= (0x00 << 8);
                    break;
                case Parity.Odd:
                    config |= (0x01 << 8);
                    break;
                case Parity.Even:
                    config |= (0x02 << 8);
                    break;
                case Parity.Mark:
                    config |= (0x03 << 8);
                    break;
                case Parity.Space:
                    config |= (0x04 << 8);
                    break;
                default:
                    throw new ArgumentException("Unknown parity value: " + parity);
            }

            switch (stopBits)
            {
                case StopBits.One:
                    config |= (0x00 << 11);
                    break;
                case StopBits.OnePointFive:
                    config |= (0x01 << 11);
                    break;
                case StopBits.Two:
                    config |= (0x02 << 11);
                    break;
                default:
                    throw new ArgumentException("Unknown stopBits value: " + stopBits);
            }

            int result = _connection.ControlTransfer(UsbAddressingExtensions.GetAddressing(FTDI_DEVICE_OUT_REQTYPE), 
                SIO_SET_DATA_REQUEST, config, 0, null, 0, USB_WRITE_TIMEOUT_MILLIS);
            if (result != 0)
            {
                throw new UsbSerialException("Setting parameters failed: result=" + result);
            }
        }

        internal virtual long[] ConvertBaudrate(int baudrate)
        {
            int divisor = 24000000 / baudrate;
            int bestDivisor = 0;
            int bestBaud = 0;
            int bestBaudDiff = 0;
            int[] fracCode = { 0, 3, 2, 4, 1, 5, 6, 7 };

            for (int i = 0; i < 2; i++)
            {
                int tryDivisor = divisor + i;
                int baudEstimate;
                int baudDiff;

                if (tryDivisor <= 8)
                {
                    tryDivisor = 8;
                }
                else if (_deviceType != FtdiDeviceType.TYPE_AM && tryDivisor < 12)
                {
                    tryDivisor = 12;
                }
                else if (divisor < 16)
                {
                    tryDivisor = 16;
                }
                else if (_deviceType != FtdiDeviceType.TYPE_AM && tryDivisor > 0x1FFFF)
                {
                    tryDivisor = 0x1FFFF;
                }

                baudEstimate = (24000000 + (tryDivisor / 2)) / tryDivisor;

                if (baudEstimate < baudrate)
                {
                    baudDiff = baudrate - baudEstimate;
                }
                else
                {
                    baudDiff = baudEstimate - baudrate;
                }

                if (i == 0 || baudDiff < bestBaudDiff)
                {
                    bestDivisor = tryDivisor;
                    bestBaud = baudEstimate;
                    bestBaudDiff = baudDiff;
                    if (baudDiff == 0)
                    {
                        break;
                    }
                }
            }

            long encodedDivisor = (bestDivisor >> 3) | (fracCode[bestDivisor & 7] << 14);
            if (encodedDivisor == 1)
            {
                encodedDivisor = 0;
            }
            else if (encodedDivisor == 0x4001)
            {
                encodedDivisor = 1;
            }

            long value = encodedDivisor & 0xFFFF;
            long index;
            if (_deviceType == FtdiDeviceType.TYPE_2232C || _deviceType == FtdiDeviceType.TYPE_2232H || _deviceType == FtdiDeviceType.TYPE_4232H)
            {
                index = (encodedDivisor >> 8) & 0xffff;
                index &= 0xFF00;
                index |= 0;
            }
            else
            {
                index = (encodedDivisor >> 16) & 0xffff;
            }

            return new [] { bestBaud, index, value };
        }

        public override bool CD => false;

        public override bool CTS => false;

        public override bool DSR => false;

        public override bool DTR
        {
            get => false;
            set
            {
                //Not doing anything here
            }
        }

        public override bool RI => false;

        public override bool RTS
        {
            get => false;
            set
            {
                //Not doing anything here
            }
        }

        public override bool PurgeHardwareBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            if (purgeReadBuffers)
            {
                int result = _connection.ControlTransfer(UsbAddressingExtensions.GetAddressing(FTDI_DEVICE_OUT_REQTYPE), 
                    SIO_RESET_REQUEST, SIO_RESET_PURGE_RX, 0, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 0)
                {
                    throw new UsbSerialException($"Flushing RX failed: result={result}");
                }
            }

            if (purgeWriteBuffers)
            {
                int result = _connection.ControlTransfer(UsbAddressingExtensions.GetAddressing(FTDI_DEVICE_OUT_REQTYPE), 
                    SIO_RESET_REQUEST, SIO_RESET_PURGE_TX, 0, null, 0, USB_WRITE_TIMEOUT_MILLIS);
                if (result != 0)
                {
                    throw new UsbSerialException($"Flushing RX failed: result={result}");
                }
            }
            return true;
        }
    }
}
