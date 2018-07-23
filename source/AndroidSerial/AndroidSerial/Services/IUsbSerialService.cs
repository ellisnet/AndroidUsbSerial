using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AndroidSerial.Models;

namespace AndroidSerial.Services
{
    public interface IUsbSerialService
    {
        bool IsDeviceConnected { get; }

        Task<IList<ISerialDevice>> FindSerialDevices();
        Task<bool> ConnectDevice(ISerialDevice device, Action<byte[]> receivedDataAction);
        Task DisconnectDevice();
        Task<bool> SendData(byte[] data);
        Task<bool> SendTextLine(string text);
        void SetDeviceAsDisconnected();
        void SetReceivedDataAction(Action<byte[]> receivedDataAction);
    }
}
