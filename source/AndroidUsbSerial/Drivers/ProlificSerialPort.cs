using System;
using System.Diagnostics;
using Android.Hardware.Usb;
using Android.Util;
using AndroidUsbSerial.Internal;
using AndroidUsbSerial.Constants;
using Java.Lang;
using Array = System.Array;
using Exception = System.Exception;
using Math = System.Math;
using Thread = System.Threading.Thread;

namespace AndroidUsbSerial.Drivers
{
    internal class ProlificSerialPort : CommonUsbSerialPort
    {
        private readonly ProlificSerialDriver _driver;

        // ReSharper disable InconsistentNaming
        internal static readonly int USB_READ_TIMEOUT_MILLIS = 1000;
        internal static readonly int USB_WRITE_TIMEOUT_MILLIS = 5000;
        internal static readonly int USB_RECIP_INTERFACE = 0x01;
        internal static readonly int PROLIFIC_VENDOR_READ_REQUEST = 0x01;
        internal static readonly int PROLIFIC_VENDOR_WRITE_REQUEST = 0x01;
        internal static readonly int PROLIFIC_VENDOR_OUT_REQTYPE = ExtendedUsbConstants.USB_DIR_OUT | ExtendedUsbConstants.USB_TYPE_VENDOR;
        internal static readonly int PROLIFIC_VENDOR_IN_REQTYPE = ExtendedUsbConstants.USB_DIR_IN | ExtendedUsbConstants.USB_TYPE_VENDOR;
        internal static readonly int PROLIFIC_CTRL_OUT_REQTYPE = ExtendedUsbConstants.USB_DIR_OUT | ExtendedUsbConstants.USB_TYPE_CLASS | USB_RECIP_INTERFACE;
        internal static readonly int WRITE_ENDPOINT = 0x02;
        internal static readonly int READ_ENDPOINT = 0x83;
        internal static readonly int INTERRUPT_ENDPOINT = 0x81;
        internal static readonly int FLUSH_RX_REQUEST = 0x08;
        internal static readonly int FLUSH_TX_REQUEST = 0x09;
        internal static readonly int SET_LINE_REQUEST = 0x20;
        internal static readonly int SET_CONTROL_REQUEST = 0x22;
        internal static readonly int CONTROL_DTR = 0x01;
        internal static readonly int CONTROL_RTS = 0x02;
        internal static readonly int STATUS_FLAG_CD = 0x01;
        internal static readonly int STATUS_FLAG_DSR = 0x02;
        internal static readonly int STATUS_FLAG_RI = 0x08;
        internal static readonly int STATUS_FLAG_CTS = 0x80;
        internal static readonly int STATUS_BUFFER_SIZE = 10;
        internal static readonly int STATUS_BYTE_IDX = 8;
        internal static readonly int DEVICE_TYPE_HX = 0;
        internal static readonly int DEVICE_TYPE_0 = 1;
        internal static readonly int DEVICE_TYPE_1 = 2;
        // ReSharper restore InconsistentNaming

        private int _deviceType = DEVICE_TYPE_HX;

        private UsbEndpoint _readEndpoint;
        private UsbEndpoint _writeEndpoint;
        private UsbEndpoint _interruptEndpoint;

        private int _controlLinesValue = 0;

        private int _baudRate = -1;

        private DataBits _dataBits = DataBits.Unknown;
        private StopBits _stopBits = StopBits.Unknown;
        private Parity _parity = Parity.Undefined;

        private int _status = 0;
        private volatile Thread _readStatusThread = null;
        private readonly object _readStatusThreadLock = new object();
        private bool _stopReadStatusThread = false;
        private UsbSerialException _readStatusException = null;

        public ProlificSerialPort(ProlificSerialDriver driver, UsbDevice device, int portNumber) : base(device, portNumber)
        {
            _driver = driver;
        }

        public override IUsbSerialDriver Driver => _driver;

        internal byte[] InControlTransfer(int requestType, int request, int value, int index, int length)
        {
            byte[] buffer = new byte[length];
            // ReSharper disable once PossibleInvalidCastException
            int result = _connection.ControlTransfer(UsbAddressingExtensions.GetAddressing(requestType), 
                request, value, index, buffer, length, USB_READ_TIMEOUT_MILLIS);
            if (result != length)
            {
                throw new UsbSerialException($"ControlTransfer with value 0x{value:x} failed: {result:D}");
            }
            return buffer;
        }

        internal void OutControlTransfer(int requestType, int request, int value, int index, byte[] data)
        {
            int length = data?.Length ?? 0;
            int result = _connection.ControlTransfer(UsbAddressingExtensions.GetAddressing(requestType), 
                request, value, index, data, length, USB_WRITE_TIMEOUT_MILLIS);
            if (result != length)
            {
                throw new UsbSerialException($"ControlTransfer with value 0x{value:x} failed: {result:D}");
            }
        }

        internal byte[] VendorIn(int value, int index, int length) => 
            InControlTransfer(PROLIFIC_VENDOR_IN_REQTYPE, PROLIFIC_VENDOR_READ_REQUEST, value, index, length);

        internal void VendorOut(int value, int index, byte[] data) => 
            OutControlTransfer(PROLIFIC_VENDOR_OUT_REQTYPE, PROLIFIC_VENDOR_WRITE_REQUEST, value, index, data);

        internal virtual void ResetDevice() => PurgeHardwareBuffers(true, true);

        internal void CtrlOut(int request, int value, int index, byte[] data) => 
            OutControlTransfer(PROLIFIC_CTRL_OUT_REQTYPE, request, value, index, data);

        internal virtual void PerformInitializationSequence()
        {
            VendorIn(0x8484, 0, 1);
            VendorOut(0x0404, 0, null);
            VendorIn(0x8484, 0, 1);
            VendorIn(0x8383, 0, 1);
            VendorIn(0x8484, 0, 1);
            VendorOut(0x0404, 1, null);
            VendorIn(0x8484, 0, 1);
            VendorIn(0x8383, 0, 1);
            VendorOut(0, 1, null);
            VendorOut(1, 0, null);
            VendorOut(2, (_deviceType == DEVICE_TYPE_HX) ? 0x44 : 0x24, null);
        }

        internal virtual int ControlLines
        {
            set
            {
                CtrlOut(SET_CONTROL_REQUEST, value, 0, null);
                _controlLinesValue = value;
            }
        }

        //TODO: Need to clean up this whole status thread section

        internal void ReadStatusThreadFunction()
        {
            try
            {
                while (!_stopReadStatusThread)
                {
                    byte[] buffer = new byte[STATUS_BUFFER_SIZE];
                    // ReSharper disable once PossibleInvalidCastException
                    int readBytesCount = _connection.BulkTransfer(_interruptEndpoint, buffer, STATUS_BUFFER_SIZE, 500);
                    if (readBytesCount > 0)
                    {
                        if (readBytesCount == STATUS_BUFFER_SIZE)
                        {
                            _status = buffer[STATUS_BYTE_IDX] & 0xff;
                        }
                        else
                        {
                            throw new UsbSerialException(
                                $"Invalid CTS / DSR / CD / RI status buffer received, expected {STATUS_BUFFER_SIZE:D} bytes, but received {readBytesCount:D}");
                        }
                    }
                }
            }
            catch (UsbSerialException e)
            {
                _readStatusException = e;
            }
        }

        internal int Status
        {
            get
            {
                if ((_readStatusThread == null) && (_readStatusException == null))
                {
                    lock (_readStatusThreadLock)
                    {
                        if (_readStatusThread == null)
                        {
                            byte[] buffer = new byte[STATUS_BUFFER_SIZE];
                            // ReSharper disable once PossibleInvalidCastException
                            int readBytes = _connection.BulkTransfer(_interruptEndpoint, buffer, STATUS_BUFFER_SIZE, 100);
                            if (readBytes != STATUS_BUFFER_SIZE)
                            {
                                Log.Warn(nameof(ProlificSerialDriver), "Could not read initial CTS / DSR / CD / RI status");
                            }
                            else
                            {
                                _status = buffer[STATUS_BYTE_IDX] & 0xff;
                            }

                            _readStatusThread = new Thread(ReadStatusThreadFunction);
                            _readStatusThread.IsBackground = true;
                            _readStatusThread.Start();
                        }
                    }
                }

                UsbSerialException readStatusException = _readStatusException;
                if (_readStatusException != null)
                {
                    _readStatusException = null;
                    // ReSharper disable once PossibleNullReferenceException
                    throw readStatusException;
                }

                return _status;
            }
        }

        internal bool TestStatusFlag(int flag) => ((Status & flag) == flag);

        public override void Open(UsbDeviceConnection connection)
        {
            if (_connection != null)
            {
                throw new UsbSerialException("Already open");
            }

            UsbInterface usbInterface = _driver.Device.GetInterface(0);

            if (!connection.ClaimInterface(usbInterface, true))
            {
                throw new UsbSerialException("Error claiming Prolific interface 0");
            }

            _connection = connection;
            bool opened = false;
            try
            {
                for (int i = 0; i < usbInterface.EndpointCount; ++i)
                {
                    UsbEndpoint currentEndpoint = usbInterface.GetEndpoint(i);

                    if (currentEndpoint.Address.IsEqualTo(READ_ENDPOINT))
                    {
                        _readEndpoint = currentEndpoint;
                    }
                    else if (currentEndpoint.Address.IsEqualTo(WRITE_ENDPOINT))
                    {
                        _writeEndpoint = currentEndpoint;
                    }
                    else if (currentEndpoint.Address.IsEqualTo(INTERRUPT_ENDPOINT))
                    {
                        _interruptEndpoint = currentEndpoint;
                    }
                }

                if ((int)_driver.Device.DeviceClass == 0x02)
                {
                    _deviceType = DEVICE_TYPE_0;
                }
                else
                {
                    try
                    {
                        byte[] rawDescriptors = _connection.GetRawDescriptors();
                        byte maxPacketSize0 = rawDescriptors[7];
                        if (maxPacketSize0 == 64)
                        {
                            _deviceType = DEVICE_TYPE_HX;
                        }
                        else if ((_driver.Device.DeviceClass == 0x00) || ((int)_driver.Device.DeviceClass == 0xff))
                        {
                            _deviceType = DEVICE_TYPE_1;
                        }
                        else
                        {
                            Log.Warn(nameof(ProlificSerialDriver), "Could not detect PL2303 subtype, " + "Assuming that it is a HX device");
                            _deviceType = DEVICE_TYPE_HX;
                        }
                    }
                    catch (NoSuchMethodException)
                    {
                        Log.Warn(nameof(ProlificSerialDriver), "Method UsbDeviceConnection.getRawDescriptors, " + "required for PL2303 subtype detection, not " + "available! Assuming that it is a HX device");
                        _deviceType = DEVICE_TYPE_HX;
                    }
                    catch (Exception e)
                    {
                        Log.Warn(nameof(ProlificSerialDriver), "An unexpected exception occured while trying " + "to detect PL2303 subtype", e);
                    }
                }

                ControlLines = _controlLinesValue;
                ResetDevice();

                PerformInitializationSequence();
                opened = true;
            }
            finally
            {
                if (!opened)
                {
                    _connection = null;
                    connection.ReleaseInterface(usbInterface);
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
                _stopReadStatusThread = true;
                lock (_readStatusThreadLock)
                {
                    if (_readStatusThread != null)
                    {
                        try
                        {
                            _readStatusThread.Join();
                        }
                        catch (Exception e)
                        {
                            Log.Warn(nameof(ProlificSerialDriver), "An error occured while waiting for status read thread", e);
                        }
                    }
                }
                ResetDevice();
            }
            finally
            {
                try
                {
                    _connection.ReleaseInterface(_driver.Device.GetInterface(0));
                }
                finally
                {
                    _connection = null;
                }
            }
        }

        public override int Read(byte[] dest, int timeoutMilliseconds)
        {
            lock (_readBufferLock)
            {
                int readAmt = Math.Min(dest.Length, _readBuffer.Length);
                int numBytesRead = _connection.BulkTransfer(_readEndpoint, _readBuffer, readAmt, timeoutMilliseconds);
                if (numBytesRead < 0)
                {
                    return 0;
                }
                Array.Copy(_readBuffer, 0, dest, 0, numBytesRead);
                return numBytesRead;
            }
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
                    try
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
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.ToString());
                        Debugger.Break();
                        throw;
                    }
                }

                if (amtWritten <= 0)
                {
                    throw new UsbSerialException(
                        $"Error writing {writeLength} bytes at offset {offset} length={src.Length}");
                }

                offset += amtWritten;
            }
            return offset;
        }

        public override void SetParameters(int baudRate, DataBits dataBits, StopBits stopBits, Parity parity)
        {
            if ((_baudRate == baudRate) && (_dataBits == dataBits) && (_stopBits == stopBits) && (_parity == parity))
            {
                return;
            }

            byte[] lineRequestData = new byte[7];

            lineRequestData[0] = unchecked((byte)(baudRate & 0xff));
            lineRequestData[1] = unchecked((byte)((baudRate >> 8) & 0xff));
            lineRequestData[2] = unchecked((byte)((baudRate >> 16) & 0xff));
            lineRequestData[3] = unchecked((byte)((baudRate >> 24) & 0xff));

            switch (stopBits)
            {
                case StopBits.One:
                    lineRequestData[4] = 0;
                    break;

                case StopBits.OnePointFive:
                    lineRequestData[4] = 1;
                    break;

                case StopBits.Two:
                    lineRequestData[4] = 2;
                    break;

                default:
                    throw new ArgumentException($"Unknown stopBits value: {stopBits}");
            }

            switch (parity)
            {
                case Parity.None:
                    lineRequestData[5] = 0;
                    break;

                case Parity.Odd:
                    lineRequestData[5] = 1;
                    break;

                case Parity.Even:
                    lineRequestData[5] = 2;
                    break;

                case Parity.Mark:
                    lineRequestData[5] = 3;
                    break;

                case Parity.Space:
                    lineRequestData[5] = 4;
                    break;

                default:
                    throw new ArgumentException($"Unknown parity value: {parity}");
            }

            lineRequestData[6] = (byte)dataBits;

            CtrlOut(SET_LINE_REQUEST, 0, 0, lineRequestData);

            ResetDevice();

            _baudRate = baudRate;
            _dataBits = dataBits;
            _stopBits = stopBits;
            _parity = parity;
        }

        public override bool CD => TestStatusFlag(STATUS_FLAG_CD);

        public override bool CTS => TestStatusFlag(STATUS_FLAG_CTS);

        public override bool DSR => TestStatusFlag(STATUS_FLAG_DSR);

        public override bool DTR
        {
            get => ((_controlLinesValue & CONTROL_DTR) == CONTROL_DTR);
            set
            {
                int newControlLinesValue;
                if (value)
                {
                    newControlLinesValue = _controlLinesValue | CONTROL_DTR;
                }
                else
                {
                    newControlLinesValue = _controlLinesValue & ~CONTROL_DTR;
                }
                ControlLines = newControlLinesValue;
            }
        }

        public override bool RI => TestStatusFlag(STATUS_FLAG_RI);

        public override bool RTS
        {
            get => ((_controlLinesValue & CONTROL_RTS) == CONTROL_RTS);
            set
            {
                int newControlLinesValue;
                if (value)
                {
                    newControlLinesValue = _controlLinesValue | CONTROL_RTS;
                }
                else
                {
                    newControlLinesValue = _controlLinesValue & ~CONTROL_RTS;
                }
                ControlLines = newControlLinesValue;
            }
        }

        public override bool PurgeHardwareBuffers(bool purgeReadBuffers, bool purgeWriteBuffers)
        {
            if (purgeReadBuffers)
            {
                VendorOut(FLUSH_RX_REQUEST, 0, null);
            }

            if (purgeWriteBuffers)
            {
                VendorOut(FLUSH_TX_REQUEST, 0, null);
            }

            return purgeReadBuffers || purgeWriteBuffers;
        }
    }
}
