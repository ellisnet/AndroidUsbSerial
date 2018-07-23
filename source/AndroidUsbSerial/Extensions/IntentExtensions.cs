using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Java.Interop;

namespace AndroidUsbSerial.Extensions
{
    public interface IUsbSerialPortInfo
    {
        int VendorId { get; }

        int DeviceId { get; }

        int PortNumber { get; }
    }

    public sealed class ParcelablePortInfo : Java.Lang.Object, IParcelable, IUsbSerialPortInfo
    {
        public static readonly string ExtraName = "UsbSerialPortInfo";
        private static readonly IParcelableCreator creator = new ParcelableCreator();

        [ExportField("CREATOR")]
        public static IParcelableCreator GetCreator() => creator;

        //public ParcelablePortInfo()
        //{
        //}

        internal ParcelablePortInfo(IUsbSerialPort port)
        {
            var device = port?.Driver?.Device ?? throw new ArgumentNullException(nameof(port));
            VendorId = device.VendorId;
            DeviceId = device.DeviceId;
            PortNumber = port.PortNumber;
        }

        private ParcelablePortInfo(Parcel parcel)
        {
            VendorId = parcel.ReadInt();
            DeviceId = parcel.ReadInt();
            PortNumber = parcel.ReadInt();
        }

        public int VendorId { get; }

        public int DeviceId { get; }

        public int PortNumber { get; }

        public int DescribeContents() => 0;

        public void WriteToParcel(Parcel dest, ParcelableWriteFlags flags)
        {
            dest.WriteInt(VendorId);
            dest.WriteInt(DeviceId);
            dest.WriteInt(PortNumber);
        }

        #region ParcelableCreator implementation

        public sealed class ParcelableCreator : Java.Lang.Object, IParcelableCreator
        {
            public Java.Lang.Object CreateFromParcel(Parcel parcel) => new ParcelablePortInfo(parcel);

            // ReSharper disable once CoVariantArrayConversion
            public Java.Lang.Object[] NewArray(int size) => new ParcelablePortInfo[size];
        }

        #endregion
    }

    public static class IntentExtensions
    {
        public static IUsbSerialPortInfo GetExtraPortInfo(this Intent intent)
        {
            if (intent == null) { throw new ArgumentNullException(nameof(intent));}
            return intent.GetParcelableExtra(ParcelablePortInfo.ExtraName) as ParcelablePortInfo;
        }

        public static void PutExtraPortInfo(this Intent intent, IUsbSerialPort port)
        {
            if (intent == null) { throw new ArgumentNullException(nameof(intent)); }
            if (port == null) { throw new ArgumentNullException(nameof(port)); }

            intent.PutExtra(ParcelablePortInfo.ExtraName, new ParcelablePortInfo(port));
        }
    }
}
