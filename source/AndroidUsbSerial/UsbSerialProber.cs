using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Hardware.Usb;
using AndroidUsbSerial.Drivers;

namespace AndroidUsbSerial
{
    public class UsbSerialProber
    {
        private readonly ProbeTable _probeTable;

        public UsbSerialProber(ProbeTable probeTable)
        {
            _probeTable = probeTable;
        }

        public static UsbSerialProber DefaultProber => new UsbSerialProber(DefaultProbeTable);

        public static ProbeTable DefaultProbeTable
        {
            get
            {
                var probeTable = new ProbeTable();
                probeTable.AddDriver(DriverOption.CdcAcmSerialDriver, CdcAcmSerialDriver.GetSupportedDevices());
                probeTable.AddDriver(DriverOption.Cp21xxSerialDriver, Cp21xxSerialDriver.GetSupportedDevices());
                probeTable.AddDriver(DriverOption.FtdiSerialDriver, FtdiSerialDriver.GetSupportedDevices());
                probeTable.AddDriver(DriverOption.ProlificSerialDriver, ProlificSerialDriver.GetSupportedDevices());
                probeTable.AddDriver(DriverOption.Ch34xSerialDriver, Ch34xSerialDriver.GetSupportedDevices());
                return probeTable;
            }
        }

        public virtual IList<IUsbSerialDriver> FindAllDrivers(UsbManager usbManager)
        {
            var result = new List<IUsbSerialDriver>();

            foreach (UsbDevice usbDevice in usbManager.DeviceList.Values)
            {
                IUsbSerialDriver driver = ProbeDevice(usbDevice);
                if (driver != null)
                {
                    result.Add(driver);
                }
            }
            return result;
        }

        public Task<IList<IUsbSerialDriver>> FindAllDriversAsync(UsbManager usbManager)
        {
            var tcs = new TaskCompletionSource<IList<IUsbSerialDriver>>();
            Task.Run(() => {
                tcs.TrySetResult(FindAllDrivers(usbManager));
            });
            return tcs.Task;
        }

        public virtual IUsbSerialDriver ProbeDevice(UsbDevice usbDevice) 
            => _probeTable.FindDriverFactory(usbDevice.VendorId, usbDevice.ProductId)?.Create(usbDevice);
    }
}
