using System.Collections.Generic;
using Android.Hardware.Usb;

namespace AndroidUsbSerial
{
    public interface IUsbSerialDriverFactory
    {
        IUsbSerialDriver Create(UsbDevice usbDevice);
    }

    public interface IUsbSerialDriver
    {
        string DriverType { get; }
        UsbDevice Device { get; }
        IList<IUsbSerialPort> Ports { get; }
    }
}
