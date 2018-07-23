namespace AndroidUsbSerial.Constants
{
    public static class UsbIdentifiers
    {
        // ReSharper disable InconsistentNaming
        public static readonly int VENDOR_FTDI = 0x0403;
        public static readonly int FTDI_FT232R = 0x6001;
        public static readonly int FTDI_FT231X = 0x6015;

        public static readonly int VENDOR_ATMEL = 0x03EB;
        public static readonly int ATMEL_LUFA_CDC_DEMO_APP = 0x2044;

        public static readonly int VENDOR_ARDUINO = 0x2341;
        public static readonly int ARDUINO_UNO = 0x0001;
        public static readonly int ARDUINO_MEGA_2560 = 0x0010;
        public static readonly int ARDUINO_SERIAL_ADAPTER = 0x003b;
        public static readonly int ARDUINO_MEGA_ADK = 0x003f;
        public static readonly int ARDUINO_MEGA_2560_R3 = 0x0042;
        public static readonly int ARDUINO_UNO_R3 = 0x0043;
        public static readonly int ARDUINO_MEGA_ADK_R3 = 0x0044;
        public static readonly int ARDUINO_SERIAL_ADAPTER_R3 = 0x0044;
        public static readonly int ARDUINO_LEONARDO = 0x8036;
        public static readonly int ARDUINO_MICRO = 0x8037;

        public static readonly int VENDOR_VAN_OOIJEN_TECH = 0x16c0;
        public static readonly int VAN_OOIJEN_TECH_TEENSYDUINO_SERIAL = 0x0483;

        public static readonly int VENDOR_LEAFLABS = 0x1eaf;
        public static readonly int LEAFLABS_MAPLE = 0x0004;

        public static readonly int VENDOR_SILABS = 0x10c4;
        public static readonly int SILABS_CP2102 = 0xea60;
        public static readonly int SILABS_CP2105 = 0xea70;
        public static readonly int SILABS_CP2108 = 0xea71;
        public static readonly int SILABS_CP2110 = 0xea80;

        public static readonly int VENDOR_PROLIFIC = 0x067b;
        public static readonly int PROLIFIC_PL2303 = 0x2303;

        public static readonly int VENDOR_QINHENG = 0x1a86;
        public static readonly int QINHENG_HL340 = 0x7523;

        public static readonly int VENDOR_YTAI = 0x1b4f;
        public static readonly int IOIO_OTG = 0x0008;
        // ReSharper restore InconsistentNaming
    }
}
