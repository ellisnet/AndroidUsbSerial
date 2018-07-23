using Android.Hardware.Usb;

namespace AndroidUsbSerial
{
    public interface IUsbSerialPort
    {
        IUsbSerialDriver Driver { get; }

        int PortNumber { get; }

        string Serial { get; }

        void Open(UsbDeviceConnection connection);

        void Close();

        int Read(byte[] dest, int timeoutMilliseconds);

        int Write(byte[] src, int timeoutMilliseconds);

        void SetParameters(int baudRate, DataBits dataBits, StopBits stopBits, Parity parity);

        // ReSharper disable InconsistentNaming

        bool CD { get; }

        bool CTS { get; }

        bool DSR { get; }

        bool DTR { get; set; }

        bool RI { get; }

        bool RTS { get; set; }

        bool PurgeHardwareBuffers(bool flushRX, bool flushTX);

        // ReSharper restore InconsistentNaming
    }
}
