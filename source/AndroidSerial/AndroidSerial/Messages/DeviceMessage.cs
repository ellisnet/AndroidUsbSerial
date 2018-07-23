using System;
using AndroidSerial.Models;

namespace AndroidSerial.Messages
{
    public class DeviceMessage
    {
        public static string DeviceFound => "DeviceFound";
        public static string DeviceDetached => "DeviceDetached";
        public static string ConnectedToComputer => "ConnectedToComputer";
        public static string DatabaseSent => "DatabaseSent";
        public static string DatabaseReceived => "DatabaseReceived";

        public string DeviceName { get; }

        public ISerialDevice Device { get; }

        public DeviceMessage(string deviceName = null, ISerialDevice device = null)
        {
            DeviceName = deviceName ?? "(no name)";
            Device = device;
        }
    }
}
