using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using AndroidUsbSerial.Constants;

namespace AndroidUsbSerial.Extensions
{
    public static class UsbManagerExtensions
    {
        public static Task<bool> RequestPermissionAsync(this UsbManager manager, UsbDevice device, Context context)
        {
            if (manager == null) { throw new ArgumentNullException(nameof(manager));}
            if (device == null) { throw new ArgumentNullException(nameof(device)); }
            if (context == null) { throw new ArgumentNullException(nameof(context)); }

            string permission = String.Format(ExtendedUsbConstants.ACTION_USB_PERMISSION_FORMAT, context.PackageName);

            var usbPermissionReceiver = new UsbBroadcastReceiver<bool>((c, i) => i.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false));
            usbPermissionReceiver.Register(context, permission, true);

            PendingIntent intent = PendingIntent.GetBroadcast(context, 0, new Intent(permission), 0);
            manager.RequestPermission(device, intent);

            return usbPermissionReceiver.ReceiveTask;
        }

        public static Task<IList<IUsbSerialDriver>> FindAllDriversAsync(this UsbManager usbManager, IList<ProductDefinition> customProducts = null)
        {
            if (usbManager == null) { throw new ArgumentNullException(nameof(usbManager)); }

            var table = UsbSerialProber.DefaultProbeTable;

            if (customProducts != null)
            {
                foreach (ProductDefinition product in customProducts)
                {
                    table.AddProduct(product);
                }
            }

            return (new UsbSerialProber(table)).FindAllDriversAsync(usbManager);
        }
    }
}
