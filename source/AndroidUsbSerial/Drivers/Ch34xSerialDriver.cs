using System.Collections.Generic;
using Android.Hardware.Usb;
using AndroidUsbSerial.Constants;

namespace AndroidUsbSerial.Drivers
{
    public class Ch34xSerialDriverFactory : IUsbSerialDriverFactory
    {
        public IUsbSerialDriver Create(UsbDevice usbDevice) => new Ch34xSerialDriver(usbDevice);
    }

    public class Ch34xSerialDriver : IUsbSerialDriver
    {
        private readonly IUsbSerialPort _port;

        public Ch34xSerialDriver(UsbDevice device)
        {
            Device = device;
            _port = new Ch34xSerialPort(this, Device, 0);
        }

        public UsbDevice Device { get; }

        public IList<IUsbSerialPort> Ports => new[] { _port };

        public string DriverType => GetType().Name;

        public static IDictionary<int, int[]> GetSupportedDevices() => new Dictionary<int, int[]>
            {
                [UsbIdentifiers.VENDOR_QINHENG] = new int[] { UsbIdentifiers.QINHENG_HL340 }
            };
    }
}
