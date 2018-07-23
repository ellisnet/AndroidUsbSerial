using Android.Hardware.Usb;

namespace AndroidUsbSerial.Constants
{
    public static class UsbAddressingExtensions
    {
        public static bool IsEqualTo(this UsbAddressing addressing, int usbConstant) => ((int) addressing == usbConstant);
        public static UsbAddressing GetAddressing(int value) => (UsbAddressing) value;
    }

    public static class ExtendedUsbConstants
    {
        // ReSharper disable InconsistentNaming
        public static readonly int USB_ENDPOINT_DIR_MASK = 128;
        public static readonly int USB_DIR_OUT = 0;
        public static readonly int USB_DIR_IN = 128;
        public static readonly int USB_ENDPOINT_NUMBER_MASK = 15;
        public static readonly int USB_ENDPOINT_XFERTYPE_MASK = 3;
        public static readonly int USB_ENDPOINT_XFER_CONTROL = 0;
        public static readonly int USB_ENDPOINT_XFER_ISOC = 1;
        public static readonly int USB_ENDPOINT_XFER_BULK = 2;
        public static readonly int USB_ENDPOINT_XFER_INT = 3;
        public static readonly int USB_TYPE_MASK = 96;
        public static readonly int USB_TYPE_STANDARD = 0;
        public static readonly int USB_TYPE_CLASS = 32;
        public static readonly int USB_TYPE_VENDOR = 64;
        public static readonly int USB_TYPE_RESERVED = 96;
        public static readonly int USB_CLASS_PER_INTERFACE = 0;
        public static readonly int USB_CLASS_AUDIO = 1;
        public static readonly int USB_CLASS_COMM = 2;
        public static readonly int USB_CLASS_HID = 3;
        public static readonly int USB_CLASS_PHYSICA = 5;
        public static readonly int USB_CLASS_STILL_IMAGE = 6;
        public static readonly int USB_CLASS_PRINTER = 7;
        public static readonly int USB_CLASS_MASS_STORAGE = 8;
        public static readonly int USB_CLASS_HUB = 9;
        public static readonly int USB_CLASS_CDC_DATA = 10;
        public static readonly int USB_CLASS_CSCID = 11;
        public static readonly int USB_CLASS_CONTENT_SEC = 13;
        public static readonly int USB_CLASS_VIDEO = 14;
        public static readonly int USB_CLASS_WIRELESS_CONTROLLER = 224;
        public static readonly int USB_CLASS_MISC = 239;
        public static readonly int USB_CLASS_APP_SPEC = 254;
        public static readonly int USB_CLASS_VENDOR_SPEC = 255;
        public static readonly int USB_INTERFACE_SUBCLASS_BOOT = 1;
        public static readonly int USB_SUBCLASS_VENDOR_SPEC = 255;

        public static readonly string ACTION_USB_PERMISSION_FORMAT = "{0}.USB_PERMISSION";
        // ReSharper restore InconsistentNaming
    }
}
