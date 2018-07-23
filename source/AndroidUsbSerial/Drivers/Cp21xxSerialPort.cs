using System;
using Android.Hardware.Usb;
using Android.Util;
using AndroidUsbSerial.Internal;
using AndroidUsbSerial.Constants;

namespace AndroidUsbSerial.Drivers
{
    internal class Cp21xxSerialPort : CommonUsbSerialPort
    {
        private readonly Cp21xxSerialDriver _driver;

        // ReSharper disable InconsistentNaming
        internal static readonly int DEFAULT_BAUD_RATE = 9600;
        internal static readonly int USB_WRITE_TIMEOUT_MILLIS = 5000;
        internal static readonly int REQTYPE_HOST_TO_DEVICE = 0x41;
        internal static readonly int SILABSER_IFC_ENABLE_REQUEST_CODE = 0x00;
        internal static readonly int SILABSER_SET_BAUDDIV_REQUEST_CODE = 0x01;
        internal static readonly int SILABSER_SET_LINE_CTL_REQUEST_CODE = 0x03;
        internal static readonly int SILABSER_SET_MHS_REQUEST_CODE = 0x07;
        internal static readonly int SILABSER_SET_BAUDRATE = 0x1E;
        internal static readonly int SILABSER_FLUSH_REQUEST_CODE = 0x12;
        internal static readonly int FLUSH_READ_CODE = 0x0a;
        internal static readonly int FLUSH_WRITE_CODE = 0x05;
        internal static readonly int UART_ENABLE = 0x0001;
        internal static readonly int UART_DISABLE = 0x0000;
        internal static readonly int BAUD_RATE_GEN_FREQ = 0x384000;
        internal static readonly int MCR_DTR = 0x0001;
        internal static readonly int MCR_RTS = 0x0002;
        internal static readonly int MCR_ALL = 0x0003;
        internal static readonly int CONTROL_WRITE_DTR = 0x0100;
        internal static readonly int CONTROL_WRITE_RTS = 0x0200;
        // ReSharper restore InconsistentNaming

        private UsbEndpoint _readEndpoint;
        private UsbEndpoint _writeEndpoint;

        public Cp21xxSerialPort(Cp21xxSerialDriver driver, UsbDevice device, int portNumber) : base(device, portNumber)
        {
            _driver = driver;
        }

        public override IUsbSerialDriver Driver => _driver;

        internal virtual int SetConfigSingle(int request, int value) => _connection.ControlTransfer(
            UsbAddressingExtensions.GetAddressing(REQTYPE_HOST_TO_DEVICE), request, value, 0, null, 0, USB_WRITE_TIMEOUT_MILLIS);

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
                    Log.Debug(nameof(Cp21xxSerialDriver),
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

                SetConfigSingle(SILABSER_IFC_ENABLE_REQUEST_CODE, UART_ENABLE);
                SetConfigSingle(SILABSER_SET_MHS_REQUEST_CODE, MCR_ALL | CONTROL_WRITE_DTR | CONTROL_WRITE_RTS);
                SetConfigSingle(SILABSER_SET_BAUDDIV_REQUEST_CODE, BAUD_RATE_GEN_FREQ / DEFAULT_BAUD_RATE);
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
                SetConfigSingle(SILABSER_IFC_ENABLE_REQUEST_CODE, UART_DISABLE);
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

                Log.Debug(nameof(Cp21xxSerialDriver), $"Wrote amt={amtWritten} attempted={writeLength}");
                offset += amtWritten;
            }
            return offset;
        }

        internal virtual int BaudRate
        {
            set
            {
                byte[] data =
                {
                    unchecked((byte)(value & 0xff)),
                    unchecked((byte)((value >> 8) & 0xff)),
                    unchecked((byte)((value >> 16) & 0xff)),
                    unchecked((byte)((value >> 24) & 0xff))
                };
                int ret = _connection.ControlTransfer(UsbAddressingExtensions.GetAddressing(REQTYPE_HOST_TO_DEVICE), 
                    SILABSER_SET_BAUDRATE, 0, 0, data, 4, USB_WRITE_TIMEOUT_MILLIS);
                if (ret < 0)
                {
                    throw new UsbSerialException("Error setting baud rate.");
                }
            }
        }

        public override void SetParameters(int baudRate, DataBits dataBits, StopBits stopBits, Parity parity)
        {
            BaudRate = baudRate;

            int configDataBits = 0;
            switch (dataBits)
            {
                case DataBits.Five:
                    configDataBits |= 0x0500;
                    break;
                case DataBits.Six:
                    configDataBits |= 0x0600;
                    break;
                case DataBits.Seven:
                    configDataBits |= 0x0700;
                    break;
                case DataBits.Eight:
                    configDataBits |= 0x0800;
                    break;
                default:
                    configDataBits |= 0x0800;
                    break;
            }

            switch (parity)
            {
                case Parity.Odd:
                    configDataBits |= 0x0010;
                    break;
                case Parity.Even:
                    configDataBits |= 0x0020;
                    break;
            }

            switch (stopBits)
            {
                case StopBits.One:
                    configDataBits |= 0;
                    break;
                case StopBits.Two:
                    configDataBits |= 2;
                    break;
            }
            SetConfigSingle(SILABSER_SET_LINE_CTL_REQUEST_CODE, configDataBits);
        }

        public override bool CD => false;

        public override bool CTS => false;

        public override bool DSR => false;

        public override bool DTR
        {
            get => true;
            set
            {
                //Not doing anything here
            }
        }

        public override bool RI => false;

        public override bool RTS
        {
            get => true;
            set
            {
                //Not doing anything here
            }
        }

        public override bool PurgeHardwareBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            int value = (purgeReadBuffers ? FLUSH_READ_CODE : 0) | (purgeWriteBuffers ? FLUSH_WRITE_CODE : 0);

            if (value != 0)
            {
                SetConfigSingle(SILABSER_FLUSH_REQUEST_CODE, value);
            }

            return true;
        }
    }
}
