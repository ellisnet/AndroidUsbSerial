using System;
using Android.Hardware.Usb;
using Android.Util;
using AndroidUsbSerial.Internal;
using AndroidUsbSerial.Constants;

namespace AndroidUsbSerial.Drivers
{
    internal class Ch34xSerialPort : CommonUsbSerialPort
    {
        private readonly Ch34xSerialDriver _driver;

        // ReSharper disable InconsistentNaming
        internal static readonly int USB_TIMEOUT_MILLIS = 5000;
        internal static readonly int DEFAULT_BAUD_RATE = 9600;
        internal static readonly int REQTYPE_HOST_TO_DEVICE_CTL_OUT = 0x41;
        internal static readonly int REQTYPE_HOST_TO_DEVICE_CTL_IN = ExtendedUsbConstants.USB_TYPE_VENDOR | ExtendedUsbConstants.USB_DIR_IN;
        // ReSharper restore InconsistentNaming

        private bool _dtr = false;
        private bool _rts = false;

        private UsbEndpoint _readEndpoint;
        private UsbEndpoint _writeEndpoint;

        public Ch34xSerialPort(Ch34xSerialDriver driver, UsbDevice device, int portNumber) : base(device, portNumber)
        {
            _driver = driver;
        }

        public override IUsbSerialDriver Driver => _driver;

        public override void Open(UsbDeviceConnection connection)
        {
            if (_connection != null)
            {
                throw new UsbSerialException("Already opened.");
            }

            _connection = connection;
            bool opened = false;
            try
            {
                for (int i = 0; i < _driver.Device.InterfaceCount; i++)
                {
                    UsbInterface usbIface = _driver.Device.GetInterface(i);
                    Log.Debug(nameof(Ch34xSerialDriver),
                        _connection.ClaimInterface(usbIface, true)
                            ? $"claimInterface {i} SUCCESS"
                            : $"claimInterface {i} FAIL");
                }

                UsbInterface dataIface = _driver.Device.GetInterface(_driver.Device.InterfaceCount - 1);
                for (int i = 0; i < dataIface.EndpointCount; i++)
                {
                    UsbEndpoint ep = dataIface.GetEndpoint(i);
                    if (ep.Type.IsEqualTo(ExtendedUsbConstants.USB_ENDPOINT_XFER_BULK))
                    {
                        if (ep.Direction.IsEqualTo(ExtendedUsbConstants.USB_DIR_IN))
                        {
                            _readEndpoint = ep;
                        }
                        else
                        {
                            _writeEndpoint = ep;
                        }
                    }
                }

                Initialize();
                BaudRate = DEFAULT_BAUD_RATE;

                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    try
                    {
                        Close();
                    }
                    catch (UsbSerialException)
                    {
                        //Swallowing this exception for now
                    }
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
            int numBytesRead;
            lock (_readBufferLock)
            {
                int readAmt = Math.Min(dest.Length, _readBuffer.Length);
                numBytesRead = _connection.BulkTransfer(_readEndpoint, _readBuffer, readAmt, timeoutMilliseconds);
                if (numBytesRead < 0)
                {
                    return 0;
                }
                Array.Copy(_readBuffer, 0, dest, 0, numBytesRead);
            }
            return numBytesRead;
        }

        public override int Write(byte[] src, int timeoutMilliseconds)
        {
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

                    amtWritten = _connection.BulkTransfer(_writeEndpoint, writeBuffer, writeLength, timeoutMilliseconds);
                }
                if (amtWritten <= 0)
                {
                    throw new UsbSerialException(
                        $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                }

                Log.Debug(nameof(Ch34xSerialDriver), $"Wrote amt={amtWritten} attempted={writeLength}");
                offset += amtWritten;
            }

            return offset;
        }

        internal virtual int ControlOut(int request, int value, int index) => _connection.ControlTransfer(
            UsbAddressingExtensions.GetAddressing(REQTYPE_HOST_TO_DEVICE_CTL_OUT), request, value, index, null, 0, USB_TIMEOUT_MILLIS);

        internal virtual int ControlIn(int request, int value, int index, byte[] buffer) => _connection.ControlTransfer(
            UsbAddressingExtensions.GetAddressing(REQTYPE_HOST_TO_DEVICE_CTL_IN), 
            request, value, index, buffer, buffer.Length, USB_TIMEOUT_MILLIS);

        internal virtual void CheckState(string msg, int request, int value, int[] expected)
        {
            byte[] buffer = new byte[expected.Length];
            int ret = ControlIn(request, value, 0, buffer);

            if (ret < 0)
            {
                throw new UsbSerialException($"Failed send cmd [{msg}]");
            }

            if (ret != expected.Length)
            {
                throw new UsbSerialException($"Expected {expected.Length} bytes, but get {ret} [{msg}]");
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] == -1)
                {
                    continue;
                }

                int current = buffer[i] & 0xff;
                if (expected[i] != current)
                {
                    throw new UsbSerialException($"Expected 0x{expected[i]:x} bytes, but get 0x{current:x} [{msg}]");
                }
            }
        }

        internal virtual void WriteHandshakeByte()
        {
            if (ControlOut(0xa4, ~((_dtr ? 1 << 5 : 0) | (_rts ? 1 << 6 : 0)), 0) < 0)
            {
                throw new UsbSerialException("Failed to set handshake byte");
            }
        }

        internal virtual void Initialize()
        {
            CheckState("init #1", 0x5f, 0, new int[] { -1, 0x00 });

            if (ControlOut(0xa1, 0, 0) < 0)
            {
                throw new UsbSerialException("init failed! #2");
            }

            BaudRate = DEFAULT_BAUD_RATE;

            CheckState("init #4", 0x95, 0x2518, new int[] { -1, 0x00 });

            if (ControlOut(0x9a, 0x2518, 0x0050) < 0)
            {
                throw new UsbSerialException("init failed! #5");
            }

            CheckState("init #6", 0x95, 0x0706, new int[] { 0xff, 0xee });

            if (ControlOut(0xa1, 0x501f, 0xd90a) < 0)
            {
                throw new UsbSerialException("init failed! #7");
            }

            BaudRate = DEFAULT_BAUD_RATE;

            WriteHandshakeByte();

            CheckState("init #10", 0x95, 0x0706, new int[] { -1, 0xee });
        }

        internal virtual int BaudRate
        {
            set
            {
                int[] baud = { 2400, 0xd901, 0x0038, 4800, 0x6402, 0x001f, 9600, 0xb202, 0x0013, 19200, 0xd902, 0x000d, 38400, 0x6403, 0x000a, 115200, 0xcc03, 0x0008 };

                for (int i = 0; i < baud.Length / 3; i++)
                {
                    if (baud[i * 3] == value)
                    {
                        int ret = ControlOut(0x9a, 0x1312, baud[i * 3 + 1]);
                        if (ret < 0)
                        {
                            throw new UsbSerialException("Error setting baud rate. #1");
                        }
                        ret = ControlOut(0x9a, 0x0f2c, baud[i * 3 + 2]);
                        if (ret < 0)
                        {
                            throw new UsbSerialException("Error setting baud rate. #1");
                        }

                        return;
                    }
                }


                throw new UsbSerialException("Baud rate " + value + " currently not supported");
            }
        }

        public override void SetParameters(int baudRate, DataBits dataBits, StopBits stopBits, Parity parity) => BaudRate = baudRate;

        public override bool CD => false;

        public override bool CTS => false;

        public override bool DSR => false;

        public override bool DTR
        {
            get => _dtr;
            set
            {
                _dtr = value;
                WriteHandshakeByte();
            }
        }

        public override bool RI => false;

        public override bool RTS
        {
            get => _rts;
            set
            {
                _rts = value;
                WriteHandshakeByte();
            }
        }

        public override bool PurgeHardwareBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            return true;
        }
    }
}
