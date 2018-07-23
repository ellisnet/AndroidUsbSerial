using System;
using System.Collections.Generic;
using Android.Hardware.Usb;
using AndroidUsbSerial.Constants;

namespace AndroidUsbSerial.Drivers
{
    public class CdcAcmSerialDriverFactory : IUsbSerialDriverFactory
    {
        public IUsbSerialDriver Create(UsbDevice usbDevice) => new CdcAcmSerialDriver(usbDevice);
    }

    public class CdcAcmSerialDriver : IUsbSerialDriver
    {
        private readonly IUsbSerialPort _port;

        public CdcAcmSerialDriver(UsbDevice device)
        {
            Device = device;
            _port = new CdcAcmSerialPort(this, device, 0);
        }

        public UsbDevice Device { get; }

        public IList<IUsbSerialPort> Ports => new [] {_port};

        public string DriverType => GetType().Name;

        public static IDictionary<int, int[]> GetSupportedDevices() =>
            new Dictionary<int, int[]>
                {
                    [Convert.ToInt32(UsbIdentifiers.VENDOR_ARDUINO)] = new[]
                    {
                        UsbIdentifiers.ARDUINO_UNO, UsbIdentifiers.ARDUINO_UNO_R3, UsbIdentifiers.ARDUINO_MEGA_2560,
                        UsbIdentifiers.ARDUINO_MEGA_2560_R3, UsbIdentifiers.ARDUINO_SERIAL_ADAPTER,
                        UsbIdentifiers.ARDUINO_SERIAL_ADAPTER_R3, UsbIdentifiers.ARDUINO_MEGA_ADK,
                        UsbIdentifiers.ARDUINO_MEGA_ADK_R3, UsbIdentifiers.ARDUINO_LEONARDO,
                        UsbIdentifiers.ARDUINO_MICRO
                    },
                    [Convert.ToInt32(UsbIdentifiers.VENDOR_VAN_OOIJEN_TECH)] = new[]
                        {UsbIdentifiers.VAN_OOIJEN_TECH_TEENSYDUINO_SERIAL},
                    [Convert.ToInt32(UsbIdentifiers.VENDOR_ATMEL)] = new[] { UsbIdentifiers.ATMEL_LUFA_CDC_DEMO_APP },
                    [Convert.ToInt32(UsbIdentifiers.VENDOR_LEAFLABS)] = new[] { UsbIdentifiers.LEAFLABS_MAPLE },
                    [Convert.ToInt32(UsbIdentifiers.VENDOR_YTAI)] = new[] { UsbIdentifiers.IOIO_OTG },
            };
    }
}
