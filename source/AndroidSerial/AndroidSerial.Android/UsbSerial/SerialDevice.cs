using System;
using System.Collections.Generic;
using System.Linq;
using AndroidSerial.Models;
using AndroidUsbSerial;

namespace AndroidSerial.Droid.UsbSerial
{
    public class SerialDevice : ISerialDevice
    {
        private readonly IUsbSerialDriver _driver;

        public int VendorId => _driver.Device?.VendorId ?? -1;
        public int DeviceId => _driver.Device?.DeviceId ?? -1;
        public string DeviceName => _driver.Device?.DeviceName ?? "Unknown";
        public string DriverType => _driver.DriverType ?? "Unknown";
        public IUsbSerialDriver Driver => _driver;

        public IList<int> PortNumbers =>
            (_driver.Ports ?? new IUsbSerialPort[] { }).Select(s => s.PortNumber).ToArray();

        public SerialDevice(IUsbSerialDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public override string ToString() => 
            $"Vendor ID: 0x{VendorId:X}"
             + $"\nDevice ID: 0x{DeviceId:X}"
             + $"\nDevice name: {DeviceName}"
             + $"\nDriver type: {DriverType}"
             + $"\nPorts: {String.Join(",", PortNumbers)}";
    }
}
