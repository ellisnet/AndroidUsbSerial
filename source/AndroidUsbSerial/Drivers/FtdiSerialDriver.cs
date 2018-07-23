using System;
using System.Collections.Generic;
using Android.Hardware.Usb;
using AndroidUsbSerial.Constants;

namespace AndroidUsbSerial.Drivers
{
    public class FtdiSerialDriverFactory : IUsbSerialDriverFactory
    {
        public IUsbSerialDriver Create(UsbDevice usbDevice) => new FtdiSerialDriver(usbDevice);
    }

    public class FtdiSerialDriver : IUsbSerialDriver
    {
        private readonly IUsbSerialPort _port;

        public FtdiSerialDriver(UsbDevice device)
        {
            Device = device;
            _port = new FtdiSerialPort(this, Device, 0);
        }

        public UsbDevice Device { get; }

        public IList<IUsbSerialPort> Ports => new[] { _port };

        public string DriverType => GetType().Name;

        public static IDictionary<int, int[]> GetSupportedDevices() => new Dictionary<int, int[]>
            {
                [Convert.ToInt32(UsbIdentifiers.VENDOR_FTDI)] = new int[]
                {
                    UsbIdentifiers.FTDI_FT232R,
                    UsbIdentifiers.FTDI_FT231X
                }
            };
    }
}
