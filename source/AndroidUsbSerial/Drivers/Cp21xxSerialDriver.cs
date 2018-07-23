using System;
using System.Collections.Generic;
using Android.Hardware.Usb;
using AndroidUsbSerial.Constants;

namespace AndroidUsbSerial.Drivers
{
    public class Cp21xxSerialDriverFactory : IUsbSerialDriverFactory
    {
        public IUsbSerialDriver Create(UsbDevice usbDevice) => new Cp21xxSerialDriver(usbDevice);
    }

    public class Cp21xxSerialDriver : IUsbSerialDriver
    {
        private readonly IUsbSerialPort _port;

        public Cp21xxSerialDriver(UsbDevice device)
        {
            Device = device;
            _port = new Cp21xxSerialPort(this, Device, 0);
        }

        public UsbDevice Device { get; }

        public IList<IUsbSerialPort> Ports => new[] { _port };

        public string DriverType => GetType().Name;

        public static IDictionary<int, int[]> GetSupportedDevices() => new Dictionary<int, int[]>
            {
                [Convert.ToInt32(UsbIdentifiers.VENDOR_SILABS)] = new int[]
                {
                    UsbIdentifiers.SILABS_CP2102,
                    UsbIdentifiers.SILABS_CP2105,
                    UsbIdentifiers.SILABS_CP2108,
                    UsbIdentifiers.SILABS_CP2110
                }
            };
    }
}
