using System;
using System.Collections.Generic;
using Android.Hardware.Usb;
using AndroidUsbSerial.Constants;

namespace AndroidUsbSerial.Drivers
{
    public class ProlificSerialDriverFactory : IUsbSerialDriverFactory
    {
        public IUsbSerialDriver Create(UsbDevice usbDevice) => new ProlificSerialDriver(usbDevice);
    }

    public class ProlificSerialDriver : IUsbSerialDriver
    {
        private readonly IUsbSerialPort _port;

        public ProlificSerialDriver(UsbDevice device)
        {
            Device = device;
            _port = new ProlificSerialPort(this, Device, 0);
        }

        public UsbDevice Device { get; }

        public IList<IUsbSerialPort> Ports => new[] { _port };

        public string DriverType => GetType().Name;

        public static IDictionary<int, int[]> GetSupportedDevices() => new Dictionary<int, int[]>
            {
                [Convert.ToInt32(UsbIdentifiers.VENDOR_PROLIFIC)] = new int[]
                {
                    UsbIdentifiers.PROLIFIC_PL2303
                }
            };
    }
}
