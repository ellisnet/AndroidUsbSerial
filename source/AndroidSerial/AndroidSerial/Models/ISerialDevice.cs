using System.Collections.Generic;

namespace AndroidSerial.Models
{
    public interface ISerialDevice
    {
        int VendorId { get; }
        int DeviceId { get; }
        string DeviceName { get; }
        IList<int> PortNumbers { get; }
        string DriverType { get; }
    }
}
