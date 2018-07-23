using System;

namespace AndroidUsbSerial
{
    public enum DataBits : int
    {
        Unknown = 0,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
    }

    [Flags]
    public enum FlowControl : int
    {
        None = 0,
        RtsCtsIn = 1,
        RtsCtsOut = 2,
        XonXoffIn = 4,
        XonXoffOut = 8,
    }

    public enum Parity : int
    {
        Undefined = -1,
        None = 0,
        Odd = 1,
        Even = 2,
        Mark = 3,
        Space = 4,
    }

    public enum StopBits : int
    {
        Unknown = 0,
        One = 1,
        Two = 2,
        OnePointFive = 3,
    }
}
