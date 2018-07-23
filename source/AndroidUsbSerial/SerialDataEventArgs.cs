using System;

namespace AndroidUsbSerial
{
    public class SerialDataEventArgs : EventArgs
    {
        public byte[] Data { get; }

        public SerialDataEventArgs(byte[] data)
        {
            Data = data;
        }
    }
}
