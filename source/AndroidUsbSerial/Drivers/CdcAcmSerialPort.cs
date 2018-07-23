using System;
using Android.Hardware.Usb;
using Android.OS;
using Android.Util;
using AndroidUsbSerial.Internal;
using AndroidUsbSerial.Constants;
using Java.Nio;

namespace AndroidUsbSerial.Drivers
{
    internal class CdcAcmSerialPort : CommonUsbSerialPort
    {
        // ReSharper disable InconsistentNaming
      
        internal static readonly int USB_RECIP_INTERFACE = 0x01;
        internal static readonly int USB_RT_ACM = UsbConstants.UsbTypeClass | USB_RECIP_INTERFACE;

        internal static readonly int SET_LINE_CODING = 0x20;
        internal static readonly int GET_LINE_CODING = 0x21;
        internal static readonly int SET_CONTROL_LINE_STATE = 0x22;
        internal static readonly int SEND_BREAK = 0x23;

        // ReSharper restore InconsistentNaming

        private readonly CdcAcmSerialDriver _driver;

        private readonly bool _enableAsyncReads;
        private UsbInterface _controlInterface;
        private UsbInterface _dataInterface;

        private UsbEndpoint _controlEndpoint;
        private UsbEndpoint _readEndpoint;
        private UsbEndpoint _writeEndpoint;

        private bool _rts = false;
        private bool _dtr = false;

        public CdcAcmSerialPort(CdcAcmSerialDriver driver, UsbDevice device, int portNumber) : base(device, portNumber)
        {
            _driver = driver;
            _enableAsyncReads = (Build.VERSION.SdkInt >= BuildVersionCodes.JellyBeanMr1);
        }

        public override IUsbSerialDriver Driver => _driver;

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

                if (1 == _driver.Device.InterfaceCount)
                {
                    Log.Debug(nameof(CdcAcmSerialDriver), "device might be castrated ACM device, trying single interface logic");
                    OpenSingleInterface();
                }
                else
                {
                    Log.Debug(nameof(CdcAcmSerialDriver), "trying default interface logic");
                    OpenInterface();
                }

                Log.Debug(nameof(CdcAcmSerialDriver),
                    _enableAsyncReads ? "Async reads enabled" : "Async reads disabled.");

                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    _connection = null;
                    _controlEndpoint = null;
                    _readEndpoint = null;
                    _writeEndpoint = null;
                }
            }
        }

        internal virtual void OpenSingleInterface()
        {
            _controlInterface = _driver.Device.GetInterface(0);
            Log.Debug(nameof(CdcAcmSerialDriver), "Control iface=" + _controlInterface);

            _dataInterface = _driver.Device.GetInterface(0);
            Log.Debug(nameof(CdcAcmSerialDriver), "data iface=" + _dataInterface);

            if (!_connection.ClaimInterface(_controlInterface, true))
            {
                throw new UsbSerialException("Could not claim shared control/data interface.");
            }

            int endCount = _controlInterface.EndpointCount;

            if (endCount < 3)
            {
                Log.Debug(nameof(CdcAcmSerialDriver), "not enough endpoints - need 3. count=" + _controlInterface.EndpointCount);
                throw new UsbSerialException("Insufficient number of endpoints(" + _controlInterface.EndpointCount + ")");
            }

            _controlEndpoint = null;
            _readEndpoint = null;
            _writeEndpoint = null;
            for (int i = 0; i < endCount; ++i)
            {
                UsbEndpoint ep = _controlInterface.GetEndpoint(i);
                if ((ep.Direction.IsEqualTo(ExtendedUsbConstants.USB_DIR_IN)) && (ep.Type.IsEqualTo(ExtendedUsbConstants.USB_ENDPOINT_XFER_INT)))
                {
                    Log.Debug(nameof(CdcAcmSerialDriver), "Found controlling endpoint");
                    _controlEndpoint = ep;
                }
                else if ((ep.Direction.IsEqualTo(ExtendedUsbConstants.USB_DIR_IN)) && (ep.Type.IsEqualTo(ExtendedUsbConstants.USB_ENDPOINT_XFER_BULK)))
                {
                    Log.Debug(nameof(CdcAcmSerialDriver), "Found reading endpoint");
                    _readEndpoint = ep;
                }
                else if ((ep.Direction.IsEqualTo(ExtendedUsbConstants.USB_DIR_OUT)) && (ep.Type.IsEqualTo(ExtendedUsbConstants.USB_ENDPOINT_XFER_BULK)))
                {
                    Log.Debug(nameof(CdcAcmSerialDriver), "Found writing endpoint");
                    _writeEndpoint = ep;
                }


                if ((_controlEndpoint != null) && (_readEndpoint != null) && (_writeEndpoint != null))
                {
                    Log.Debug(nameof(CdcAcmSerialDriver), "Found all required endpoints");
                    break;
                }
            }

            if ((_controlEndpoint == null) || (_readEndpoint == null) || (_writeEndpoint == null))
            {
                Log.Debug(nameof(CdcAcmSerialDriver), "Could not establish all endpoints");
                throw new UsbSerialException("Could not establish all endpoints");
            }
        }

        internal virtual void OpenInterface()
        {
            Log.Debug(nameof(CdcAcmSerialDriver), "claiming interfaces, count=" + _driver.Device.InterfaceCount);

            _controlInterface = _driver.Device.GetInterface(0);
            Log.Debug(nameof(CdcAcmSerialDriver), "Control iface=" + _controlInterface);

            if (!_connection.ClaimInterface(_controlInterface, true))
            {
                throw new UsbSerialException("Could not claim control interface.");
            }

            _controlEndpoint = _controlInterface.GetEndpoint(0);
            Log.Debug(nameof(CdcAcmSerialDriver), "Control endpoint direction: " + _controlEndpoint.Direction);

            Log.Debug(nameof(CdcAcmSerialDriver), "Claiming data interface.");
            _dataInterface = _driver.Device.GetInterface(1);
            Log.Debug(nameof(CdcAcmSerialDriver), "data iface=" + _dataInterface);

            if (!_connection.ClaimInterface(_dataInterface, true))
            {
                throw new UsbSerialException("Could not claim data interface.");
            }
            _readEndpoint = _dataInterface.GetEndpoint(1);
            Log.Debug(nameof(CdcAcmSerialDriver), "Read endpoint direction: " + _readEndpoint.Direction);
            _writeEndpoint = _dataInterface.GetEndpoint(0);
            Log.Debug(nameof(CdcAcmSerialDriver), "Write endpoint direction: " + _writeEndpoint.Direction);
        }

        internal virtual int SendAcmControlMessage(int request, int value, byte[] buf) 
            => _connection.ControlTransfer((UsbAddressing)USB_RT_ACM, request, value, 0, buf, buf?.Length ?? 0, 5000);

        public override void Close()
        {
            if (_connection == null)
            {
                throw new UsbSerialException("Already closed");
            }
            _connection.Close();
            _connection = null;
        }

        public override int Read(byte[] dest, int timeoutMilliseconds)
        {
            if (_enableAsyncReads)
            {
                var request = new UsbRequest();
                try
                {
                    request.Initialize(_connection, _readEndpoint);

                    var buf = ByteBuffer.Wrap(dest);
                    if (!request.Queue(buf, dest.Length)) //TODO: Must fix this
                    {
                        throw new UsbSerialException("Error queueing request.");
                    }

                    UsbRequest response = _connection.RequestWait();
                    if (response == null)
                    {
                        throw new UsbSerialException("Null response");
                    }

                    int nread = buf.Position();
                    if (nread > 0)
                    {
                        return nread;
                    }
                    else
                    {
                        return 0;
                    }
                }
                finally
                {
                    request.Close();
                }
            }

            int numBytesRead;
            lock (_readBufferLock)
            {
                int readAmt = Math.Min(dest.Length, _readBuffer.Length);
                numBytesRead = _connection.BulkTransfer(_readEndpoint, _readBuffer, readAmt, timeoutMilliseconds);
                if (numBytesRead < 0)
                {
                    if (timeoutMilliseconds == int.MaxValue)
                    {
                        return -1;
                    }
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

                Log.Debug(nameof(CdcAcmSerialDriver), $"Wrote amt={amtWritten} attempted={writeLength}");
                offset += amtWritten;
            }
            return offset;
        }

        public override void SetParameters(int baudRate, DataBits dataBits, StopBits stopBits, Parity parity)
        {
            byte stopBitsByte;
            switch (stopBits)
            {
                case StopBits.One:
                    stopBitsByte = 0;
                    break;
                case StopBits.OnePointFive:
                    stopBitsByte = 1;
                    break;
                case StopBits.Two:
                    stopBitsByte = 2;
                    break;
                default:
                    throw new ArgumentException("Bad value for stopBits: " + stopBits);
            }

            byte parityBitesByte;
            switch (parity)
            {
                case Parity.None:
                    parityBitesByte = 0;
                    break;
                case Parity.Odd:
                    parityBitesByte = 1;
                    break;
                case Parity.Even:
                    parityBitesByte = 2;
                    break;
                case Parity.Mark:
                    parityBitesByte = 3;
                    break;
                case Parity.Space:
                    parityBitesByte = 4;
                    break;
                default:
                    throw new ArgumentException("Bad value for parity: " + parity);
            }

            byte[] msg =
            {
                unchecked((byte)(baudRate & 0xff)),
                unchecked((byte)((baudRate >> 8) & 0xff)),
                unchecked((byte)((baudRate >> 16) & 0xff)),
                unchecked((byte)((baudRate >> 24) & 0xff)),
                stopBitsByte,
                parityBitesByte,
                (byte)dataBits
            };
            SendAcmControlMessage(SET_LINE_CODING, 0, msg);
        }

        public override bool CD => false;

        public override bool CTS => false;

        public override bool DSR => false;

        public override bool DTR
        {
            get => _dtr;
            set
            {
                _dtr = value;
                SetDtrRts();
            }
        }

        public override bool RI => false;

        public override bool RTS
        {
            get => _rts;
            set
            {
                _rts = value;
                SetDtrRts();
            }
        }

        internal virtual void SetDtrRts()
        {
            int value = (_rts ? 0x2 : 0) | (_dtr ? 0x1 : 0);
            SendAcmControlMessage(SET_CONTROL_LINE_STATE, value, null);
        }
    }
}
