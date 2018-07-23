using System;

namespace AndroidUsbSerial
{
    public class UsbSerialException : Exception
    {

        public UsbSerialException() : base() { }

        public UsbSerialException(string message, Exception innerException) : base(message, innerException) { }

        public UsbSerialException(string message) : base(message) { }

        public UsbSerialException(Exception innerException) : base("An unexpected USB serial device error occurred.", innerException) { }
    }
}
