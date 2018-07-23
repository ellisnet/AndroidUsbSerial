using Android.Hardware.Usb;

namespace AndroidUsbSerial.Internal
{
    internal abstract class CommonUsbSerialPort : IUsbSerialPort
    {
        // ReSharper disable InconsistentNaming

        public static readonly int DEFAULT_READ_BUFFER_SIZE = 4 * 1024; //16 * 1024;
        public static readonly int DEFAULT_WRITE_BUFFER_SIZE = 4 * 1024; //16 * 1024;

        protected internal readonly UsbDevice _device;
        protected internal readonly int _portNumber;

        protected internal UsbDeviceConnection _connection = null;

        protected internal readonly object _readBufferLock = new object();
        protected internal readonly object _writeBufferLock = new object();

        protected internal byte[] _readBuffer;

        protected internal byte[] _writeBuffer;

        // ReSharper restore InconsistentNaming

        public CommonUsbSerialPort(UsbDevice device, int portNumber)
        {
            _device = device;
            _portNumber = portNumber;

            _readBuffer = new byte[DEFAULT_READ_BUFFER_SIZE];
            _writeBuffer = new byte[DEFAULT_WRITE_BUFFER_SIZE];
        }

        public override string ToString() => $"<{GetType().Name} device_name={_device.DeviceName} device_id={_device.DeviceId} port_number={_portNumber}>";

        public UsbDevice Device => _device;

        public abstract IUsbSerialDriver Driver { get; }

        public virtual int PortNumber => _portNumber;

        public virtual string Serial => _connection.Serial;

        public int ReadBufferSize
        {
            set
            {
                lock (_readBufferLock)
                {
                    if (value == _readBuffer.Length)
                    {
                        return;
                    }
                    _readBuffer = new byte[value];
                }
            }
        }

        public int WriteBufferSize
        {
            set
            {
                lock (_writeBufferLock)
                {
                    if (value == _writeBuffer.Length)
                    {
                        return;
                    }
                    _writeBuffer = new byte[value];
                }
            }
        }

        public abstract void Open(UsbDeviceConnection connection);

        public abstract void Close();

        public abstract int Read(byte[] dest, int timeoutMilliseconds);

        public abstract int Write(byte[] src, int timeoutMilliseconds);

        public abstract void SetParameters(int baudRate, DataBits dataBits, StopBits stopBits, Parity parity);

        public abstract bool CD { get; }

        public abstract bool CTS { get; }

        public abstract bool DSR { get; }

        public abstract bool DTR { get; set; }

        public abstract bool RI { get; }

        public abstract bool RTS { get; set; }

        public virtual bool PurgeHardwareBuffers(bool flushReadBuffers, bool flushWriteBuffers) => !flushReadBuffers && !flushWriteBuffers;
    }
}
